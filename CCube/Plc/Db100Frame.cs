// Db100Frame.cs
// -----------------------------------------------------------------------------
// Turns the raw bytes of the data block into meaningful values, and back.
//
// Think of it as a translator that sits on top of one 58-byte buffer:
//   * ReadInputs()  - reads the PLC-owned bytes into a tidy PlcInputs snapshot.
//   * WriteOutputs()- writes our PcOutputs into the PC-owned bytes.
//
// It uses S7Data (for the byte format) and Db100Layout (for the offsets). All
// the fiddly Siemens details are confined to those two files plus this one.
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public sealed class Db100Frame
{
    private readonly byte[] _buffer;

    /// <summary>Wrap an existing 58-byte buffer (no copy - it's a live view).</summary>
    public Db100Frame(byte[] buffer)
    {
        if (buffer.Length < Db100Layout.BlockSizeBytes)
        {
            throw new ArgumentException(
                $"PLC buffer must be at least {Db100Layout.BlockSizeBytes} bytes.", nameof(buffer));
        }
        _buffer = buffer;
    }

    /// <summary>Start a fresh, all-zero block (used when building a write).</summary>
    public static Db100Frame Empty() => new(new byte[Db100Layout.BlockSizeBytes]);

    /// <summary>The underlying bytes, e.g. to hand to the PLC library.</summary>
    public byte[] Bytes => _buffer;

    // --- Reading (PLC -> PC) ---------------------------------------------------

    public PlcInputs ReadInputs()
    {
        return new PlcInputs(
            SeqNo: S7Data.GetInt(_buffer, Db100Layout.SeqNo),
            Barcode: S7Data.GetString(_buffer, Db100Layout.Barcode, Db100Layout.BarcodeMaxLength),
            PlcHeartbeat: S7Data.GetInt(_buffer, Db100Layout.PlcHeartbeat),
            ScannerNoRead: S7Data.GetBool(_buffer, Db100Layout.FlagsIn, Db100Layout.ScannerNoReadBit),
            PlcReady: S7Data.GetBool(_buffer, Db100Layout.FlagsIn, Db100Layout.PlcReadyBit));
    }

    // --- Writing (PC -> PLC) ---------------------------------------------------

    /// <summary>
    /// Write our answer into the buffer. Order matters: we set the verdict fields
    /// first and AckSeqNo LAST, so the PLC (which waits for AckSeqNo == SeqNo)
    /// never reads a half-written answer.
    /// </summary>
    public void WriteOutputs(PcOutputs outputs)
    {
        S7Data.SetBool(_buffer, Db100Layout.FlagsOut, Db100Layout.ResultValidBit, outputs.ResultValid);
        S7Data.SetBool(_buffer, Db100Layout.FlagsOut, Db100Layout.ResultGoodBit, outputs.ResultGood);
        S7Data.SetInt(_buffer, Db100Layout.ResultCode, (short)outputs.ResultCode);
        S7Data.SetInt(_buffer, Db100Layout.PcHeartbeat, outputs.PcHeartbeat);
        S7Data.SetBool(_buffer, Db100Layout.FlagsOnline, Db100Layout.PcOnlineBit, outputs.PcOnline);

        // LAST: the commit signal.
        S7Data.SetInt(_buffer, Db100Layout.AckSeqNo, outputs.AckSeqNo);
    }

    // --- Helpers used by the Demo PLC to play the PLC's side -------------------

    /// <summary>(Demo only) Read back what the PC most recently wrote.</summary>
    public PcOutputs ReadOutputs()
    {
        return new PcOutputs
        {
            AckSeqNo = S7Data.GetInt(_buffer, Db100Layout.AckSeqNo),
            ResultValid = S7Data.GetBool(_buffer, Db100Layout.FlagsOut, Db100Layout.ResultValidBit),
            ResultGood = S7Data.GetBool(_buffer, Db100Layout.FlagsOut, Db100Layout.ResultGoodBit),
            ResultCode = (ResultCode)S7Data.GetInt(_buffer, Db100Layout.ResultCode),
            PcHeartbeat = S7Data.GetInt(_buffer, Db100Layout.PcHeartbeat),
            PcOnline = S7Data.GetBool(_buffer, Db100Layout.FlagsOnline, Db100Layout.PcOnlineBit),
        };
    }

    /// <summary>(Demo only) Play the PLC's side: present a new case.</summary>
    public void InjectCase(short seqNo, string barcode, bool scannerNoRead)
    {
        // Write the barcode and flags first...
        S7Data.SetString(_buffer, Db100Layout.Barcode, Db100Layout.BarcodeMaxLength, barcode);
        S7Data.SetBool(_buffer, Db100Layout.FlagsIn, Db100Layout.ScannerNoReadBit, scannerNoRead);
        S7Data.SetBool(_buffer, Db100Layout.FlagsIn, Db100Layout.PlcReadyBit, true);
        // ...then bump SeqNo LAST, mirroring how a real PLC commits a new case.
        S7Data.SetInt(_buffer, Db100Layout.SeqNo, seqNo);
    }

    /// <summary>(Demo only) Advance the PLC heartbeat so it looks alive.</summary>
    public void TickPlcHeartbeat()
    {
        short hb = S7Data.GetInt(_buffer, Db100Layout.PlcHeartbeat);
        S7Data.SetInt(_buffer, Db100Layout.PlcHeartbeat, unchecked((short)(hb + 1)));
    }
}
