// IHistorySink.cs
// -----------------------------------------------------------------------------
// The contract for saving a finished scan to history. Save() runs after the
// verdict is sent to the PLC and must not throw or slow the line - it swallows
// and logs its own errors, and the scan engine calls it off the critical path.
// -----------------------------------------------------------------------------

using CCube.Models;

namespace CCube.Data;

public interface IHistorySink
{
    void Save(ScanRecord record);
}

/// <summary>Used when no CCube database is configured.</summary>
public sealed class NullHistorySink : IHistorySink
{
    public void Save(ScanRecord record)
    {
    }
}
