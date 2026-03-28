namespace StartRef.Desktop;

/// <summary>
/// Reads data from DBISAM via DbBridge DLL.
///
/// NOTE: Bulk read (ReadAll) is NOT supported because DbBridge DLL has no enumerate function.
/// Ask the DLL author to add DbReadAllTeiln(ctx, buffer, bufferSize).
/// Individual lookups by IdNr, StartNr, and ChipNr are supported.
/// </summary>
public class DbIsamReader
{
    private readonly Func<AppSettings> _getSettings;

    public DbIsamReader(Func<AppSettings> getSettings)
    {
        _getSettings = getSettings;
    }

    /// <summary>
    /// NOT SUPPORTED — DbBridge DLL has no bulk enumerate function.
    /// Ask DLL author to implement DbReadAllTeiln(ctx, buffer, bufferSize).
    /// </summary>
    public List<BulkRunnerDto> ReadAll() =>
        throw new NotSupportedException(
            "DbBridge DLL lacks bulk read. Ask DLL author to add DbReadAllTeiln(ctx, buffer, bufferSize).");

    /// <summary>Returns raw participant info buffer for a given IdNr, or null if not found.</summary>
    public string? GetRawTeilnInfoByIdNr(int idNr)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath)) return null;
        var (result, raw) = db.GetTeilnInfoByIdNr(idNr);
        return result.Success ? raw : null;
    }

    /// <summary>Returns IdNr list buffer for a given StartNr, or null if not found.</summary>
    public string? FindIdNrListByStartNr(int startNr)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath)) return null;
        var (result, raw) = db.GetIdNrListByStartNr(startNr);
        return result.Success ? raw : null;
    }

    /// <summary>Returns IdNr list buffer for a given day+ChipNr, or null if not found.</summary>
    public string? FindIdNrListByChipNr(int dayNo, int chipNr)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath)) return null;
        var (result, raw) = db.GetIdNrListByChipNr(dayNo, chipNr);
        return result.Success ? raw : null;
    }
}

/// <summary>
/// Writes field changes to DBISAM via DbBridge DLL.
///
/// NOT SUPPORTED: WriteDnsStatuses — DLL has no DbChangeDnsByStartNr function.
/// Ask the DLL author to add DbChangeDnsByStartNr(ctx, startNr, dnsFlag).
/// </summary>
public class DbIsamWriter
{
    private readonly Func<AppSettings> _getSettings;

    public DbIsamWriter(Func<AppSettings> getSettings)
    {
        _getSettings = getSettings;
    }

    /// <summary>
    /// NOT SUPPORTED — DbBridge DLL has no DNS write function.
    /// Ask DLL author to add DbChangeDnsByStartNr(ctx, startNr, dnsFlag).
    /// </summary>
    public void WriteDnsStatuses(IEnumerable<int> _) =>
        throw new NotSupportedException(
            "DbBridge DLL lacks DNS write. Ask DLL author to add DbChangeDnsByStartNr(ctx, startNr, dnsFlag).");

    /// <summary>Changes ChipNr for the runner with the given StartNr on the specified day.</summary>
    public DbBridgeResult WriteChipNr(int dayNo, int startNr, int chipNr)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath))
            return new DbBridgeResult(false, DbBridgeNative.DBR_ERROR, "Cannot open DBISAM");
        return db.ChangeChipNrByStartNr(dayNo, chipNr, startNr);
    }

    /// <summary>Changes StartTime for the runner with the given StartNr on the specified day.</summary>
    public DbBridgeResult WriteStartTime(int dayNo, int startNr, string hhmmss)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath))
            return new DbBridgeResult(false, DbBridgeNative.DBR_ERROR, "Cannot open DBISAM");
        return db.ChangeStartTimeByStartNr(dayNo, hhmmss, startNr);
    }

    /// <summary>Changes KatNr for the runner with the given StartNr.</summary>
    public DbBridgeResult WriteKatNr(int startNr, int katNr)
    {
        var settings = _getSettings();
        using var db = new DbBridgeService();
        if (!db.Open(settings.DbIsamPath))
            return new DbBridgeResult(false, DbBridgeNative.DBR_ERROR, "Cannot open DBISAM");
        return db.ChangeKatNrByStartNr(katNr, startNr);
    }
}

internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
