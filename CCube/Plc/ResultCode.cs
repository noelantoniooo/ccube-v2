// ResultCode.cs
// -----------------------------------------------------------------------------
// The reason attached to every verdict. We send this number to the PLC (so it
// can show/act on it) and store it in history (so we can report on it).
//
// IMPORTANT: the numbers are part of the PLC contract. The PLC program expects
// specific values. NEVER renumber existing entries - only add new ones at the
// end. (Ok must stay 0.)
// -----------------------------------------------------------------------------

namespace CCube.Plc;

public enum ResultCode
{
    Ok = 0,             // InExpress said the case is valid  -> PASS
    NotValid = 1,       // InExpress said the case is invalid -> REJECT
    ScannerNoRead = 2,  // the scanner could not read this case -> REJECT
    BadBarcode = 3,     // the barcode is missing/implausible -> REJECT
    Timeout = 4,        // InExpress took too long to answer -> REJECT (our fault)
    Error = 5,          // InExpress/other error while validating -> REJECT (our fault)
    NotReady = 6,       // the system isn't ready to validate -> REJECT
}

public static class ResultCodeExtensions
{
    /// <summary>
    /// The allow-list of what counts as a PASS. ONLY <see cref="ResultCode.Ok"/>
    /// passes; anything else - including any code added in the future - is a
    /// REJECT. This is the fail-safe direction: when unsure, reject.
    /// </summary>
    public static bool IsPass(this ResultCode code) => code == ResultCode.Ok;

    /// <summary>
    /// True when WE rejected because of a system problem (timeout/error), not
    /// because the case was genuinely bad. Tracking these separately means a
    /// database outage can't hide inside a normal-looking reject rate.
    /// </summary>
    public static bool IsSystemReject(this ResultCode code) =>
        code is ResultCode.Timeout or ResultCode.Error or ResultCode.NotReady;
}
