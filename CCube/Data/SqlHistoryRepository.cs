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
using Microsoft.Data.SqlClient;

namespace CCube.Data;

public sealed class SqlHistoryRepository : IHistorySink
{
    private const string InsertSql = @"
INSERT INTO dbo.ScanHistory
    (ScannedAtUtc, StationId, Barcode, Verdict, ResultCode, IsSystemReject,
     ProductName, BatchNo, InExpressMs, TotalMs)
VALUES
    (@ScannedAtUtc, @StationId, @Barcode, @Verdict, @ResultCode, @IsSystemReject,
     @ProductName, @BatchNo, @InExpressMs, @TotalMs);";

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
            command.Parameters.AddWithValue("@Verdict", record.Verdict);
            command.Parameters.AddWithValue("@ResultCode", record.ResultCode.ToString());
            command.Parameters.AddWithValue("@IsSystemReject", record.IsSystemReject);
            command.Parameters.AddWithValue("@ProductName", (object?)record.ProductName ?? DBNull.Value);
            command.Parameters.AddWithValue("@BatchNo", (object?)record.BatchNo ?? DBNull.Value);
            command.Parameters.AddWithValue("@InExpressMs", record.InExpressMs);
            command.Parameters.AddWithValue("@TotalMs", record.TotalMs);

            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            // Logged, not thrown - history is never allowed to affect the line.
            AppLog.Error($"Could not save scan '{record.Barcode}' to history.", ex);
        }
    }
}
