// DemoPlcLink.cs
// -----------------------------------------------------------------------------
// A FAKE PLC for running / demonstrating the app WITHOUT real Siemens hardware.
//
// It keeps its own 58-byte data block in memory - the exact same byte layout the
// real PLC uses (Db100Layout) - so the whole path runs identically to the real
// thing: the scan loop READS this block, DECODES it into PlcInputs, validates,
// and WRITES the verdict back into it. Only the "wire" is faked.
//
// Call InjectCase(barcode) to present a new case - just like a scanner writing a
// barcode into the PLC and the PLC bumping SeqNo. The scan loop then notices the
// new SeqNo and processes it, so you can set a breakpoint in ReadInputs (or in
// Db100Frame.ReadInputs, which is shared with the real link) and watch the bytes
// turn into values.
//
// Selected instead of S7PlcLink when settings.DemoMode is true (see MainForm).
// -----------------------------------------------------------------------------

using CCube.Core;

namespace CCube.Plc;

public sealed class DemoPlcLink : IPlcLink
{
    // The in-memory data block - our fake PLC's shared "shelf" of bytes.
    private readonly byte[] _block = new byte[Db100Layout.BlockSizeBytes];
    private readonly object _lock = new(); // reads run on the engine thread, injects on the UI thread
    private short _seqNo;

    public bool IsConnected => true;           // the fake link is always "up"
    public void EnsureConnected() { }

    /// <summary>Read one snapshot of the block, advancing the PLC heartbeat so the
    /// engine sees the (fake) PLC as alive. Same decode path as the real link.</summary>
    public PlcInputs ReadInputs()
    {
        lock (_lock)
        {
            var frame = new Db100Frame(_block); // wraps the live block (no copy)
            frame.TickPlcHeartbeat();           // pretend the PLC is alive
            return frame.ReadInputs();          // bytes -> PlcInputs (shared with S7PlcLink)
        }
    }

    /// <summary>The app writes its verdict here; we just store it into the block
    /// (a real PLC would physically keep/eject the case on these bits).</summary>
    public void WriteOutputs(PcOutputs outputs)
    {
        lock (_lock)
        {
            new Db100Frame(_block).WriteOutputs(outputs);
        }
    }

    /// <summary>Present a NEW case - like a scanner writing a barcode into the PLC
    /// and the PLC bumping SeqNo. The scan loop will pick it up on its next poll.</summary>
    public void InjectCase(string barcode, bool scannerNoRead = false)
    {
        lock (_lock)
        {
            _seqNo++;
            new Db100Frame(_block).InjectCase(_seqNo, barcode ?? "", scannerNoRead);
            AppLog.Info($"[Demo PLC] Injected case seq {_seqNo}: '{barcode}'"
                + (scannerNoRead ? " (scanner no-read)" : "") + ".");
        }
    }

    public void Dispose() { }
}
