using System.Data;
using System.Data.Odbc;

namespace StartRef.Desktop;

/// <summary>
/// Reads the full start list from DBISAM via the user's installed DBISAM ODBC driver.
///
/// Assumes an ODBC DSN or connection string pointing to the DBISAM database.
/// Table and column names are typical OE12 DBISAM schema — adjust if yours differ.
/// </summary>
public class DbIsamReader
{
    private readonly Func<AppSettings> _getSettings;

    public DbIsamReader(Func<AppSettings> getSettings)
    {
        _getSettings = getSettings;
    }

    private string BuildConnectionString()
    {
        var dbPath = _getSettings().DbIsamPath;
        // DBISAM ODBC driver connection string; adjust driver name as needed.
        return $"Driver={{DBISAM 4 ODBC Driver}};DatabaseName={dbPath};";
    }

    /// <summary>
    /// Reads all runners from the DBISAM start list.
    /// Returns an empty list if the DB is unavailable.
    /// </summary>
    public List<BulkRunnerDto> ReadAll()
    {
        var runners = new List<BulkRunnerDto>();
        try
        {
            using var conn = new OdbcConnection(BuildConnectionString());
            conn.Open();

            // OE12 table is typically named "Meldungen" (entries) or "Starts".
            // Adjust the query to match your actual DBISAM table/column names.
            using var cmd = new OdbcCommand(@"
                SELECT
                    StartnNr      AS StartNumber,
                    SIKarte       AS SiChipNo,
                    Vorname       AS Name,
                    Nachname      AS Surname,
                    Kategorie     AS ClassName,
                    Verein        AS ClubName,
                    Land          AS Country,
                    StartPos      AS StartPlace,
                    AufgabeTyp    AS DnsFlag
                FROM Starts
                ORDER BY StartnNr", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var dnsFlag = reader["DnsFlag"]?.ToString();
                var statusId = dnsFlag == "DNS" ? 3 : 1;

                runners.Add(new BulkRunnerDto
                {
                    StartNumber = Convert.ToInt32(reader["StartNumber"]),
                    SiChipNo = reader["SiChipNo"]?.ToString()?.Trim().NullIfEmpty(),
                    Name = reader["Name"]?.ToString()?.Trim() ?? "",
                    Surname = reader["Surname"]?.ToString()?.Trim() ?? "",
                    ClassName = reader["ClassName"]?.ToString()?.Trim() ?? "",
                    ClubName = reader["ClubName"]?.ToString()?.Trim() ?? "",
                    Country = reader["Country"]?.ToString()?.Trim().NullIfEmpty(),
                    StartPlace = reader["StartPlace"] == DBNull.Value ? 0 : Convert.ToInt32(reader["StartPlace"]),
                    StatusId = statusId
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DBISAM read failed: {ex.Message}", ex);
        }

        return runners;
    }
}

/// <summary>
/// Writes DNS status back to DBISAM for runners that the API reports as DNS.
/// Only modifies the DNS/status field — does not touch any other runner data.
/// </summary>
public class DbIsamWriter
{
    private readonly Func<AppSettings> _getSettings;

    public DbIsamWriter(Func<AppSettings> getSettings)
    {
        _getSettings = getSettings;
    }

    private string BuildConnectionString()
    {
        var dbPath = _getSettings().DbIsamPath;
        return $"Driver={{DBISAM 4 ODBC Driver}};DatabaseName={dbPath};";
    }

    /// <summary>
    /// Sets DNS status for the given start numbers in DBISAM.
    /// Called after a PULL that found runners with statusId=3 (DNS) from field devices.
    /// </summary>
    public void WriteDnsStatuses(IEnumerable<int> dnsStartNumbers)
    {
        var numbers = dnsStartNumbers.ToList();
        if (numbers.Count == 0) return;

        try
        {
            using var conn = new OdbcConnection(BuildConnectionString());
            conn.Open();

            foreach (var startNumber in numbers)
            {
                using var cmd = new OdbcCommand(
                    "UPDATE Starts SET AufgabeTyp = 'DNS' WHERE StartnNr = ?", conn);
                cmd.Parameters.AddWithValue("@sn", startNumber);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"DBISAM DNS write failed: {ex.Message}", ex);
        }
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
