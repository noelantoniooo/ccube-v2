// CsvHistoryRepository.cs
// -----------------------------------------------------------------------------
// Writes each finished scan to a daily CSV file on disk - a SECOND backup of the
// history the boss asked for, independent of the SQL database. If the DB is down,
// the CSV still captures every scan (and vice-versa).
//
// Layout:  <base folder>\<yyyy-MM>\CCube-History-<yyyy-MM-dd>.csv
// One file per day; a header row is written when a new file is created. The CSV
// columns match the Case Logs grid (local times, human-readable).
//
// Like the SQL sink, this NEVER throws: a disk hiccup must not affect the line.
// -----------------------------------------------------------------------------

using System.Text;
using CCube.Core;
using CCube.Models;

namespace CCube.Data;

public sealed class CsvHistoryRepository : IHistorySink
{
    // Serialises writes so two scans a moment apart can't interleave in the file.
    private static readonly object _fileLock = new();

    private const string Header =
        "InExpressId,StationId,Barcode,CasePacker,ProductDescription,FaCode," +
        "ProductionAt,ValidationResult,ResultCode,ScannedAt";

    private readonly string _baseFolder;

    public CsvHistoryRepository(string baseFolder)
    {
        _baseFolder = string.IsNullOrWhiteSpace(baseFolder) ? @"C:\CCube\History" : baseFolder;
    }

    public void Save(ScanRecord record)
    {
        try
        {
            DateTime local = record.ScannedAtLocal;
            string dir = Path.Combine(_baseFolder, local.ToString("yyyy-MM"));
            string path = Path.Combine(dir, $"CCube-History-{local:yyyy-MM-dd}.csv");

            lock (_fileLock)
            {
                Directory.CreateDirectory(dir);
                bool newFile = !File.Exists(path);
                using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
                if (newFile)
                {
                    writer.WriteLine(Header);
                }
                writer.WriteLine(BuildRow(record, local));
            }
        }
        catch (Exception ex)
        {
            // Logged, never thrown - the CSV backup must not affect the conveyor.
            AppLog.Error($"Could not write scan '{record.Barcode}' to the CSV backup.", ex);
        }
    }

    private static string BuildRow(ScanRecord r, DateTime local)
    {
        return string.Join(",",
            Csv(r.InExpressId?.ToString()),
            Csv(r.StationId),
            Csv(r.Barcode),
            Csv(r.CasePacker),
            Csv(r.ProductDescription),
            Csv(r.FaCode),
            Csv(r.ProductionAt?.ToString("yyyy-MM-dd HH:mm:ss")),
            Csv(r.ValidationResult),
            Csv(r.ResultCode.ToString()),
            Csv(local.ToString("yyyy-MM-dd HH:mm:ss")));
    }

    // Quote a field only when it needs it (comma, quote or newline), doubling any
    // embedded quotes - standard CSV escaping so a description with a comma is safe.
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
