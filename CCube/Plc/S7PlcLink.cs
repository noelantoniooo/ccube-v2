// S7PlcLink.cs
// -----------------------------------------------------------------------------
// Talks to a Siemens S7-1200 over the S7.NET plus library. Reads the whole data
// block in one call (a consistent snapshot) and writes back only the PC-owned
// slice of bytes, so it never overwrites the PLC's barcode.
//
// On any error it drops the connection (close + forget) so the next loop
// reconnects cleanly instead of reusing a dead handle.
//
// TIA Portal requirement: the data block must have "optimized block access" OFF
// and PUT/GET enabled, or these reads/writes won't reach it.
// -----------------------------------------------------------------------------

using CCube.Core;
using S7.Net;
// Our namespace is also called "Plc", which clashes with S7.Net's Plc class.
// This alias gives the S7 client a clear, unambiguous name.
using S7Client = S7.Net.Plc;

namespace CCube.Plc;

public sealed class S7PlcLink : IPlcLink
{
    private readonly string _ip;
    private readonly short _rack;
    private readonly short _slot;
    private readonly int _dbNumber;

    private S7Client? _plc;

    public S7PlcLink(PlcSettings settings)
    {
        _ip = settings.IpAddress;
        _rack = (short)settings.Rack;
        _slot = (short)settings.Slot;
        _dbNumber = settings.DbNumber;
    }

    public bool IsConnected => _plc?.IsConnected == true;

    public void EnsureConnected()
    {
        if (IsConnected)
        {
            return;
        }
        try
        {
            _plc = new S7Client(CpuType.S71200, _ip, _rack, _slot);
            _plc.Open();
            AppLog.Info($"PLC connected ({_ip}, rack {_rack}, slot {_slot}, DB{_dbNumber}).");
        }
        catch (Exception ex)
        {
            AppLog.Error($"PLC connect failed ({_ip}).", ex);
            Drop();
        }
    }

    public PlcInputs ReadInputs()
    {
        try
        {
            byte[] block = (byte[])_plc!.Read(
                DataType.DataBlock, _dbNumber, 0,
                VarType.Byte, Db100Layout.BlockSizeBytes)!;
            return new Db100Frame(block).ReadInputs();
        }
        catch (Exception ex)
        {
            AppLog.Error("PLC read failed.", ex);
            Drop();       // force a clean reconnect next loop
            throw;        // let the engine mark the PLC as down
        }
    }

    public void WriteOutputs(PcOutputs outputs)
    {
        try
        {
            // Build a full frame, then send only the PC-owned bytes (48..56).
            var frame = Db100Frame.Empty();
            frame.WriteOutputs(outputs);

            byte[] slice = new byte[Db100Layout.PcRegionLength];
            Array.Copy(frame.Bytes, Db100Layout.PcRegionStart, slice, 0, Db100Layout.PcRegionLength);

            _plc!.Write(DataType.DataBlock, _dbNumber, Db100Layout.PcRegionStart, slice);
        }
        catch (Exception ex)
        {
            // Logged, not thrown: a failed heartbeat/verdict write shouldn't crash
            // the loop; we drop and reconnect next pass.
            AppLog.Error("PLC write failed.", ex);
            Drop();
        }
    }

    public void Dispose() => Drop();

    // Close and forget the connection so IsConnected becomes false.
    private void Drop()
    {
        try
        {
            _plc?.Close();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Error while closing the PLC connection.", ex);
        }
        _plc = null;
    }
}
