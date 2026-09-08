// ScanRecord.cs
// -----------------------------------------------------------------------------
// One finished scan - the result of validating a single case. It's used in two
// places: shown in the live list on screen, and saved to the CCube history DB.
// -----------------------------------------------------------------------------

using CCube.Plc;

namespace CCube.Models;

public sealed record ScanRecord
{
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;
    public string StationId { get; init; } = "";
    public string Barcode { get; init; } = "";
    public ResultCode ResultCode { get; init; }
    public string? ProductName { get; init; }
    public string? BatchNo { get; init; }
    public int InExpressMs { get; init; }   // how long the InExpress call took
    public int TotalMs { get; init; }       // total time to answer this case

    // Convenience views derived from the fields above.
    public bool IsPass => ResultCode.IsPass();
    public string Verdict => IsPass ? "PASS" : "REJECT";
    public bool IsSystemReject => ResultCode.IsSystemReject();
    public DateTime ScannedAtLocal => ScannedAtUtc.ToLocalTime();
}
