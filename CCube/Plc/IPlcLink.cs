// IPlcLink.cs
// -----------------------------------------------------------------------------
// The contract the scan engine uses to talk to the PLC. Methods are synchronous;
// the scan engine calls them from its own background thread.
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public interface IPlcLink : IDisposable
{
    /// <summary>Is the link currently usable?</summary>
    bool IsConnected { get; }

    /// <summary>Connect if not already connected. Safe to call every loop.</summary>
    void EnsureConnected();

    /// <summary>Read one consistent snapshot of the PLC-owned fields.</summary>
    PlcInputs ReadInputs();

    /// <summary>Write our verdict + liveness into the PC-owned fields.</summary>
    void WriteOutputs(PcOutputs outputs);
}
