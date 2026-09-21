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
}
