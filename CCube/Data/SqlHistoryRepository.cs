// SqlHistoryRepository.cs
// -----------------------------------------------------------------------------
// Saves each finished scan to the CCube database (dbo.ScanHistory) for history
// and reporting. One parameterized INSERT per scan.
//
// Deliberately never throws: if the DB is briefly unavailable we log the problem
// and move on. Losing a history row must NEVER stop the conveyor. The verdict
// has already been sent to the PLC by the time we get here.
// -----------------------------------------------------------------------------

using CCube.Core;
using CCube.Models;
using CCube.Plc;
using Microsoft.Data.SqlClient;

namespace CCube.Data;

public sealed class SqlHistoryRepository : IHistorySink
{
    private const string InsertSql = @"
INSERT INTO dbo.ScanHistory
    (ScannedAtUtc, StationId, Barcode, CasePacker, InExpressId,
     ProductDescription, FaCode, ProductionAt, ValidationResult, ResultCode)
VALUES
    (@ScannedAtUtc, @StationId, @Barcode, @CasePacker, @InExpressId,
     @ProductDescription, @FaCode, @ProductionAt, @ValidationResult, @ResultCode);";

    private readonly string _connectionString;

    public SqlHistoryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Save(ScanRecord record)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(InsertSql, connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("@ScannedAtUtc", record.ScannedAtUtc);
            command.Parameters.AddWithValue("@StationId", record.StationId);
            command.Parameters.AddWithValue("@Barcode", record.Barcode);
            command.Parameters.AddWithValue("@CasePacker", (object?)record.CasePacker ?? DBNull.Value);
            command.Parameters.AddWithValue("@InExpressId", (object?)record.InExpressId ?? DBNull.Value);
            command.Parameters.AddWithValue("@ProductDescription", (object?)record.ProductDescription ?? DBNull.Value);
            command.Parameters.AddWithValue("@FaCode", (object?)record.FaCode ?? DBNull.Value);
            command.Parameters.AddWithValue("@ProductionAt", (object?)record.ProductionAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@ValidationResult", record.ValidationResult);
            command.Parameters.AddWithValue("@ResultCode", record.ResultCode.ToString());

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Logged, not thrown - history is never allowed to affect the line.
            AppLog.Error($"Could not save scan '{record.Barcode}' to history.", ex);
        }
    }

    // Reads this station's most recent scans (newest first) so the run screen can
    // repopulate the Case Logs after a restart (e.g. a theme change) instead of
    // starting blank. Read-only; returns an empty list on any error.
    private const string RecentSql = @"
SELECT TOP (@Max)
       ScannedAtUtc, StationId, Barcode, CasePacker, InExpressId,
       ProductDescription, FaCode, ProductionAt, ResultCode
FROM   dbo.ScanHistory
WHERE  StationId = @StationId
ORDER  BY Id DESC;";

    public List<ScanRecord> LoadRecent(string stationId, int max)
    {
        var results = new List<ScanRecord>();
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(RecentSql, connection) { CommandTimeout = 5 };
            command.Parameters.AddWithValue("@Max", max < 1 ? 1 : max);
            command.Parameters.AddWithValue("@StationId", stationId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new ScanRecord
                {
                    // Stored as UTC; mark the Kind so ScannedAtLocal converts correctly.
                    ScannedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                    StationId = reader.GetString(1),
                    Barcode = reader.GetString(2),
                    CasePacker = reader.IsDBNull(3) ? null : reader.GetString(3),
                    InExpressId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    ProductDescription = reader.IsDBNull(5) ? null : reader.GetString(5),
                    FaCode = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ProductionAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    ResultCode = ParseResultCode(reader.IsDBNull(8) ? null : reader.GetString(8)),
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Could not load recent scan history.", ex);
        }
        return results;
    }

    // Turn the stored reason name back into the enum; unknown/missing -> NotValid,
    // which keeps the fail-safe direction (anything we can't confirm is a REJECT).
    private static ResultCode ParseResultCode(string? name) =>
        Enum.TryParse(name, ignoreCase: true, out ResultCode code) ? code : ResultCode.NotValid;
}
