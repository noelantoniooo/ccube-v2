// InExpressValidator.cs
// -----------------------------------------------------------------------------
// The REAL validator: asks the plant's InExpress database whether a case is
// valid, by calling ONE stored procedure and passing the barcode.
//
// This is the ONLY file that knows anything about InExpress. If PMI finalizes a
// different procedure name, parameters, or result columns, you change it HERE
// and nowhere else.
//
// The contract we rely on:
//     EXEC dbo.usp_ValidateCase @Barcode = '...'
//   returns exactly ONE row containing an "IsValid" bit (1 = valid, 0 = invalid),
//   and optionally ProductName / BatchNo / Reason columns for display + history.
//
// Security: the barcode is passed as a PARAMETER (@Barcode), never glued into the
// SQL text, so a malicious barcode can't inject SQL.
// -----------------------------------------------------------------------------

using System.Data;
using CCube.Core;
using Microsoft.Data.SqlClient;

namespace CCube.Data;

public sealed class InExpressValidator : IValidator
{
    private readonly string _connectionString;
    private readonly string _procName;
    private readonly int _timeoutSeconds;

    public InExpressValidator(string connectionString,
        string procName = "dbo.usp_ValidateCase", int timeoutSeconds = 2)
    {
        _connectionString = connectionString;
        _procName = string.IsNullOrWhiteSpace(procName) ? "dbo.usp_ValidateCase" : procName;
        _timeoutSeconds = timeoutSeconds < 1 ? 1 : timeoutSeconds; // SQL minimum is 1s
    }

    public CaseResult Validate(string barcode)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(_procName, connection)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = _timeoutSeconds, // give up if the DB is too slow
            };
            command.Parameters.Add(new SqlParameter("@Barcode", SqlDbType.NVarChar, 50)
            {
                Value = barcode,
            });

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                // No row = we can't confirm it's valid = reject (fail-safe).
                AppLog.Warn($"InExpress returned no row for '{barcode}'; treating as INVALID.");
                return new CaseResult(false, Reason: "No result from validation proc");
            }

            bool isValid = ReadIsValid(reader);
            string? product = TryGetString(reader, "ProductName");
            string? batch = TryGetString(reader, "BatchNo");
            string? reason = TryGetString(reader, "Reason");

            AppLog.Info($"InExpress: '{barcode}' -> {(isValid ? "VALID" : "INVALID")}"
                + (reason is null ? "" : $" ({reason})"));
            return new CaseResult(isValid, product, batch, reason);
        }
        catch (Exception ex)
        {
            // Log with full detail, then rethrow so the scan engine can turn this
            // into a safe REJECT (and decide if it was a timeout or a hard error).
            AppLog.Error($"InExpress validation FAILED for '{barcode}'.", ex);
            throw;
        }
    }

    public bool Ping()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT 1", connection)
            {
                CommandTimeout = _timeoutSeconds,
            };
            command.ExecuteScalar();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warn("InExpress health ping failed.", ex);
            return false;
        }
    }

    // Find the "IsValid" column (by name; fall back to the first column) and read
    // it flexibly, accepting bit/int/bool - anything that means non-zero = valid.
    private static bool ReadIsValid(SqlDataReader reader)
    {
        int col = 0;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), "IsValid", StringComparison.OrdinalIgnoreCase))
            {
                col = i;
                break;
            }
        }

        if (reader.IsDBNull(col))
        {
            return false;
        }

        object value = reader.GetValue(col);
        return value switch
        {
            bool b => b,
            byte by => by != 0,
            short s => s != 0,
            int n => n != 0,
            long l => l != 0,
            _ => Convert.ToInt32(value) != 0,
        };
    }

    // Read an optional text column; return null if the proc didn't include it.
    private static string? TryGetString(SqlDataReader reader, string columnName)
    {
        try
        {
            int i = reader.GetOrdinal(columnName);
            return reader.IsDBNull(i) ? null : reader.GetValue(i)?.ToString();
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }
}
