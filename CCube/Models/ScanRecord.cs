// ScanRecord.cs
// -----------------------------------------------------------------------------
// One finished scan - the result of validating a single case. It's used in two
// places: shown in the live list on screen, and saved to the CCube history DB.
// -----------------------------------------------------------------------------

using CCube.Plc;

namespace CCube.Models;

public sealed record ScanRecord
{
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;  // when the app got the case
    public string StationId { get; init; } = "";
    public string Barcode { get; init; } = "";
    public ResultCode ResultCode { get; init; }

    // Returned by the InExpress validate proc.
    public int? InExpressId { get; init; }
    public string? ProductDescription { get; init; }
    public string? FaCode { get; init; }

    // Parsed from the barcode, client-side (display/history only, never validation).
    public string? CasePacker { get; init; }
    public DateTime? ProductionAt { get; init; }

    // Timings - shown in the Event Logs / used for debugging, not in the grid.
    public int InExpressMs { get; init; }   // how long the InExpress call took
    public int TotalMs { get; init; }       // total time to answer this case

    // Convenience views derived from the fields above.
    public bool IsPass => ResultCode.IsPass();
    public string Verdict => IsPass ? "PASS" : "REJECT";        // for the big verdict panel
    public string ValidationResult => IsPass ? "Good" : "Reject"; // for the Case Logs column
    public bool IsSystemReject => ResultCode.IsSystemReject();
    public DateTime ScannedAtLocal => ScannedAtUtc.ToLocalTime();
}
