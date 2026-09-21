// CompositeHistorySink.cs
// -----------------------------------------------------------------------------
// Saves each scan to SEVERAL history sinks at once (the SQL database AND the CSV
// backup). One sink failing never stops the others - each is wrapped so a bad
// database can't block the CSV write, or vice-versa. Like every sink, it never
// throws back to the scan engine.
// -----------------------------------------------------------------------------

using CCube.Core;
using CCube.Models;

namespace CCube.Data;

public sealed class CompositeHistorySink : IHistorySink
{
    private readonly IHistorySink[] _sinks;

    public CompositeHistorySink(params IHistorySink[] sinks)
    {
        _sinks = sinks.Where(s => s is not null).ToArray();
    }

    public void Save(ScanRecord record)
    {
        foreach (IHistorySink sink in _sinks)
        {
            try
            {
                sink.Save(record);
            }
            catch (Exception ex)
            {
                // A single sink's own Save already swallows its errors; this is a
                // last-resort guard so one bad sink can't skip the rest.
                AppLog.Error($"History sink {sink.GetType().Name} threw while saving.", ex);
            }
        }
    }
}
