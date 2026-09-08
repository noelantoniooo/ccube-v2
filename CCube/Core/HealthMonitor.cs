// HealthMonitor.cs
// -----------------------------------------------------------------------------
// Holds the current health of the three things we watch - the PLC, InExpress,
// and the scanner - and raises an event whenever any of them changes, so the
// screen can update its status lights.
//
// The scan engine PUSHES updates in here (it's the component that actually talks
// to the PLC and InExpress). The UI just listens.
// -----------------------------------------------------------------------------

namespace CCube.Core;

public enum Health
{
    Unknown, // we haven't checked yet (grey light)
    Up,      // working (green light)
    Down,    // not working (red light)
}

public sealed class HealthMonitor
{
    private readonly object _gate = new();

    public Health Plc { get; private set; } = Health.Unknown;
    public Health InExpress { get; private set; } = Health.Unknown;
    public Health Scanner { get; private set; } = Health.Unknown;

    // Extra words for the PLC state, e.g. "LINK LOST" vs "NOT RESPONDING".
    public string PlcDetail { get; private set; } = "";

    /// <summary>Raised (on a background thread) whenever any value above changes.</summary>
    public event Action<HealthMonitor>? Changed;

    public void SetPlc(Health value, string detail = "")
    {
        bool changed;
        lock (_gate)
        {
            changed = Plc != value || PlcDetail != detail;
            Plc = value;
            PlcDetail = detail;
        }
        if (changed)
        {
            Raise();
        }
    }

    public void SetInExpress(Health value)
    {
        bool changed;
        lock (_gate)
        {
            changed = InExpress != value;
            InExpress = value;
        }
        if (changed)
        {
            Raise();
        }
    }

    public void SetScanner(Health value)
    {
        bool changed;
        lock (_gate)
        {
            changed = Scanner != value;
            Scanner = value;
        }
        if (changed)
        {
            Raise();
        }
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke(this);
        }
        catch (Exception ex)
        {
            AppLog.Error("A health-change handler threw.", ex);
        }
    }
}
