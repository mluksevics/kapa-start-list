using StartRef.Desktop.DbBridge;
using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

public class DbIsamRepository
{
    private sealed record DbRunnerSnapshot(
        string? ChipNo,
        int ClassId,
        string? StartTime,
        string? Name,
        string? Surname,
        int ClubId,
        string? ClubName);

    private readonly Action<string> _log;

    public DbIsamRepository(Action<string> log)
    {
        _log = log;
    }

    public List<BulkRunnerDto> ScanRunnersByStartNr(AppSettings settings, CancellationToken ct = default) =>
        ScanRunnersByStartNr(settings, 1, 4000, ct);

    /// <summary>Scans DBISAM for start numbers in inclusive <paramref name="minStartNumber"/>..<paramref name="maxStartNumber"/> (clamped to 1–4000).</summary>
    public List<BulkRunnerDto> ScanRunnersByStartNr(AppSettings settings, int minStartNumber, int maxStartNumber, CancellationToken ct = default)
    {
        const int absoluteMax = 4000;
        int min = Math.Clamp(Math.Min(minStartNumber, maxStartNumber), 1, absoluteMax);
        int max = Math.Clamp(Math.Max(minStartNumber, maxStartNumber), 1, absoluteMax);
        int span = max - min + 1;

        var results = new List<BulkRunnerDto>();

        if (string.IsNullOrEmpty(settings.DbIsamPath))
        {
            _log($"{Ts()} Scan: DB path not set.");
            return results;
        }

        using var db = new DbBridgeService(_log);
        if (!db.Open(settings.DbIsamPath))
        {
            _log($"{Ts()} Scan: cannot open DBISAM.");
            return results;
        }

        _log($"{Ts()} Scanning start numbers {min}–{max}...");

        for (int startNr = min; startNr <= max; startNr++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (listResult, listRaw) = db.GetIdNrListByStartNr(startNr);
                if (!listResult.Success) continue;

                var idNrs = DbBridgeService.ParseIdNrList(listRaw);
                int idNr = idNrs.Count > 0 ? idNrs[0] : 0;
                if (idNr == 0)
                    continue;

                var dto = new BulkRunnerDto { StartNumber = startNr, StatusId = 1, LastModifiedUtc = DateTimeOffset.UtcNow };

                if (idNr > 0)
                {
                    var (infoResult, infoRaw) = db.GetTeilnInfoByIdNr(idNr);
                    if (infoResult.Success && !string.IsNullOrEmpty(infoRaw))
                    {
                        var fields = DbBridgeService.ParseTeilnInfo(infoRaw);

                        var mldKen = GetField(fields, $"MldKen{settings.DayNo}");
                        if (string.Equals(mldKen, "False", StringComparison.OrdinalIgnoreCase))
                        {
                            _log($"{Ts()} Scan startNr={startNr}: skipped (MldKen{settings.DayNo}=False)");
                            continue;
                        }

                        dto.Surname = GetField(fields, "Name") ?? "";
                        dto.Name = GetField(fields, "Vorname") ?? "";
                        dto.ClassId = int.TryParse(GetField(fields, "KatNr"), out var katNr) ? katNr : 0;
                        dto.ClassName = GetField(fields, "Grupa") ?? "";
                        dto.ClubId = int.TryParse(GetField(fields, "ClubNr"), out var clubNr) ? clubNr : 0;
                        dto.ClubName = GetField(fields, "Klubs") ?? "";
                        var chipStr = GetField(fields, $"ChipNr{settings.DayNo}");
                        if (!string.IsNullOrEmpty(chipStr)) dto.SiChipNo = chipStr;
                        dto.StartTime = GetField(fields, $"Start{settings.DayNo}");
                    }
                }

                results.Add(dto);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log($"{Ts()} Scan startNr={startNr} failed: {ex.Message}");
                _log($"{Ts()} Check DLL logs: {DbBridgeService.GlobalDllLogPath}");
                if (!string.IsNullOrWhiteSpace(settings.DbIsamPath))
                    _log($"{Ts()} Check DB log: {DbBridgeService.DbErrorLogPath(settings.DbIsamPath)}");
            }

            int idxInRange = startNr - min + 1;
            if (results.Count > 0 && (idxInRange % 100 == 0 || startNr == max))
                _log($"{Ts()} Scanned {idxInRange}/{span} in range, found {results.Count} so far...");
        }

        _log($"{Ts()} Scan complete: {results.Count} runners found in start range {min}–{max}.");
        return results;
    }

    public void ApplyPulledUpdates(AppSettings settings, List<RunnerDto> pulledRunners, CancellationToken ct)
    {
        int ours = pulledRunners.Count(r => string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (pulledRunners.Count > 0)
            _log($"{Ts()} PULL summary: {pulledRunners.Count} total — {ours} ours, {pulledRunners.Count - ours} from other devices.");

        foreach (var r in pulledRunners)
        {
            ct.ThrowIfCancellationRequested();
            var cfLog = FormatApiChangedFields(r.ChangedFields);
            if (!string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                _log($"{Ts()} PULL #{r.StartNumber} (by: {r.LastModifiedBy}) changedFields=[{cfLog}] Name='{r.Name}' Surname='{r.Surname}' Class='{r.ClassName}' ({r.ClassId}) Club='{r.ClubName}' ({r.ClubId}) StartTime='{r.StartTime ?? "-"}'");
            }
            else
            {
                _log($"{Ts()} PULL #{r.StartNumber} ours changedFields=[{cfLog}]");
            }

            if (r.StatusId != 1)
                _log($"{Ts()} PULL #{r.StartNumber} {r.Name} {r.Surname} – Status={r.StatusName} (by: {r.LastModifiedBy})");
        }

        var chipUpdates = pulledRunners
            .Where(r => !string.IsNullOrEmpty(r.SiChipNo) &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                        HintIncludes(r, "SiChipNo"))
            .ToList();

        var katUpdates = pulledRunners
            .Where(r => r.ClassId > 0 &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                        HintIncludes(r, "ClassId"))
            .ToList();

        var startTimeUpdates = pulledRunners
            .Where(r => r.StartTime != null &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                        HintIncludes(r, "StartTime"))
            .ToList();

        bool hasDbUpdates = chipUpdates.Count > 0 || katUpdates.Count > 0 || startTimeUpdates.Count > 0;
        if (hasDbUpdates && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            db.TestMode = settings.IsTestMode;
            if (db.Open(settings.DbIsamPath))
            {
                var snapshots = new Dictionary<(int StartNumber, int DayNo), DbRunnerSnapshot?>();
                var foreignUpdates = pulledRunners
                    .Where(r => !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var r in foreignUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    bool logName = HintIncludes(r, "Name");
                    bool logSurname = HintIncludes(r, "Surname");
                    bool logClub = HintIncludes(r, "ClubId");
                    if (!logName && !logSurname && !logClub)
                        continue;

                    var snapshot = GetSnapshot(db, snapshots, settings.DayNo, r.StartNumber);
                    if (snapshot is null) continue;

                    if (logName)
                    {
                        if (TextEqual(snapshot.Name, r.Name))
                            _log($"{Ts()} DBISAM Name #{r.StartNumber}: SKIP unchanged ('{r.Name}')");
                        else
                            _log($"{Ts()} DBISAM Name #{r.StartNumber}: NEEDS change ('{snapshot.Name ?? "-"}' → '{r.Name}') [not applied: DLL missing DbChangeNameByStartNr]");
                    }

                    if (logSurname)
                    {
                        if (TextEqual(snapshot.Surname, r.Surname))
                            _log($"{Ts()} DBISAM Surname #{r.StartNumber}: SKIP unchanged ('{r.Surname}')");
                        else
                            _log($"{Ts()} DBISAM Surname #{r.StartNumber}: NEEDS change ('{snapshot.Surname ?? "-"}' → '{r.Surname}') [not applied: DLL missing DbChangeSurnameByStartNr]");
                    }

                    if (logClub)
                    {
                        if (snapshot.ClubId == r.ClubId && TextEqual(snapshot.ClubName, r.ClubName))
                            _log($"{Ts()} DBISAM Club #{r.StartNumber}: SKIP unchanged ('{r.ClubName}'/{r.ClubId})");
                        else
                            _log($"{Ts()} DBISAM Club #{r.StartNumber}: NEEDS change ('{snapshot.ClubName ?? "-"}'/{snapshot.ClubId} → '{r.ClubName}'/{r.ClubId}) [not applied: DLL missing DbChangeClubNrByStartNr]");
                    }
                }

                foreach (var r in chipUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!int.TryParse(r.SiChipNo, out int chipNr)) continue;
                    const int maxChipUpdateDay = 3;
                    int startDay = Math.Max(1, settings.DayNo);
                    if (startDay > maxChipUpdateDay)
                    {
                        _log($"{Ts()} DBISAM ChipNr #{r.StartNumber}: SKIP (selected API day {settings.DayNo} is outside allowed chip update range 1-{maxChipUpdateDay}).");
                        continue;
                    }

                    for (int dayNo = startDay; dayNo <= maxChipUpdateDay; dayNo++)
                    {
                        var snapshot = GetSnapshot(db, snapshots, dayNo, r.StartNumber);
                        if (dayNo > startDay)
                        {
                            var existingChip = (snapshot?.ChipNo ?? string.Empty).Trim();
                            if (!int.TryParse(existingChip, out int existingChipNr) || existingChipNr == 0)
                            {
                                _log($"{Ts()} DBISAM ChipNr #{r.StartNumber} Day{dayNo}: SKIP propagation (no chip assigned)");
                                continue;
                            }
                        }
                        if (snapshot is not null && ChipsEqual(snapshot.ChipNo, r.SiChipNo))
                        {
                            _log($"{Ts()} DBISAM ChipNr #{r.StartNumber} Day{dayNo}: SKIP unchanged ({r.SiChipNo})");
                            continue;
                        }
                        var res = db.ChangeChipNrByStartNr(dayNo, chipNr, r.StartNumber);
                        _log($"{Ts()} DBISAM ChipNr #{r.StartNumber} Day{dayNo} → {chipNr}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                    }
                }

                foreach (var r in katUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    var snapshot = GetSnapshot(db, snapshots, settings.DayNo, r.StartNumber);
                    if (snapshot is not null && snapshot.ClassId == r.ClassId)
                    {
                        _log($"{Ts()} DBISAM KatNr #{r.StartNumber}: SKIP unchanged ({r.ClassId})");
                        continue;
                    }
                    var res = db.ChangeKatNrByStartNr(r.ClassId, r.StartNumber);
                    _log($"{Ts()} DBISAM KatNr #{r.StartNumber} → {r.ClassId}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                }

                foreach (var r in startTimeUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    var snapshot = GetSnapshot(db, snapshots, settings.DayNo, r.StartNumber);
                    if (snapshot is not null && StartTimesEqual(snapshot.StartTime, r.StartTime))
                    {
                        _log($"{Ts()} DBISAM StartTime #{r.StartNumber}: SKIP unchanged ({r.StartTime})");
                        continue;
                    }
                    _log($"{Ts()} DBISAM StartTime #{r.StartNumber}: NEEDS change ('{snapshot?.StartTime ?? "-"}' → '{r.StartTime}')");
                    var res = db.ChangeStartTimeByStartNr(settings.DayNo, r.StartTime!, r.StartNumber);
                    _log($"{Ts()} DBISAM StartTime #{r.StartNumber} → {r.StartTime}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                }
            }
            else
            {
                _log($"{Ts()} DBISAM not available — writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        var foreignForDns = pulledRunners
            .Where(r => !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (foreignForDns.Count > 0 && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var dnsDb = new DbBridgeService(_log);
            dnsDb.TestMode = settings.IsTestMode;
            if (dnsDb.Open(settings.DbIsamPath))
            {
                foreach (var r in foreignForDns)
                {
                    ct.ThrowIfCancellationRequested();
                    if (r.StatusId == 3)
                    {
                        var res = dnsDb.ChangeDnsByStartNr(settings.DayNo, dnsFlag: 1, r.StartNumber);
                        _log($"{Ts()} DBISAM DNS #{r.StartNumber}: {(res.Success ? "OK" : res.Message)}");
                    }
                    else if (r.StatusId == 1 && HintIncludes(r, "StatusId"))
                    {
                        var res = dnsDb.ChangeDnsByStartNr(settings.DayNo, dnsFlag: 0, r.StartNumber);
                        _log($"{Ts()} DBISAM DNS clear #{r.StartNumber}: {(res.Success ? "OK" : res.Message)}");
                    }
                }
            }
        }
    }

    private static string FormatApiChangedFields(List<string>? changedFields)
    {
        if (changedFields is null)
            return "not specified";
        if (changedFields.Count == 0)
            return "empty";
        return string.Join(", ", changedFields);
    }

    private static bool HintIncludes(RunnerDto r, string fieldName)
    {
        var hints = r.ChangedFields;
        if (hints is null || hints.Count == 0)
            return true;

        foreach (var h in hints)
        {
            if (string.Equals(h, fieldName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? GetField(Dictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
            if (fields.TryGetValue(name, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        return null;
    }

    private static string Ts() => DateTime.Now.ToString("HH:mm:ss");

    private DbRunnerSnapshot? GetSnapshot(
        DbBridgeService db,
        Dictionary<(int StartNumber, int DayNo), DbRunnerSnapshot?> cache,
        int dayNo,
        int startNumber)
    {
        var key = (startNumber, dayNo);
        if (cache.TryGetValue(key, out var cached))
            return cached;

        var (listResult, listRaw) = db.GetIdNrListByStartNr(startNumber);
        if (!listResult.Success)
        {
            cache[key] = null;
            return null;
        }

        var idNrs = DbBridgeService.ParseIdNrList(listRaw);
        int idNr = idNrs.Count > 0 ? idNrs[0] : 0;
        if (idNr <= 0)
        {
            cache[key] = null;
            return null;
        }

        var (infoResult, infoRaw) = db.GetTeilnInfoByIdNr(idNr);
        if (!infoResult.Success || string.IsNullOrWhiteSpace(infoRaw))
        {
            cache[key] = null;
            return null;
        }

        var fields = DbBridgeService.ParseTeilnInfo(infoRaw);
        var chip = GetField(fields, $"ChipNr{dayNo}");
        var start = GetField(fields, $"Start{dayNo}");
        var surname = GetField(fields, "Name");
        var name = GetField(fields, "Vorname");
        var clubName = GetField(fields, "Klubs");
        int clubId = int.TryParse(GetField(fields, "ClubNr"), out var clubNr) ? clubNr : 0;
        int classId = int.TryParse(GetField(fields, "KatNr"), out var katNr) ? katNr : 0;

        var snapshot = new DbRunnerSnapshot(chip, classId, start, name, surname, clubId, clubName);
        cache[key] = snapshot;
        return snapshot;
    }

    private static bool ChipsEqual(string? currentChip, string? incomingChip)
    {
        if (int.TryParse((currentChip ?? string.Empty).Trim(), out var current) &&
            int.TryParse((incomingChip ?? string.Empty).Trim(), out var incoming))
            return current == incoming;

        return string.Equals(
            (currentChip ?? string.Empty).Trim(),
            (incomingChip ?? string.Empty).Trim(),
            StringComparison.Ordinal);
    }

    private static bool StartTimesEqual(string? currentTime, string? incomingTime)
    {
        return string.Equals(NormalizeTime(currentTime), NormalizeTime(incomingTime), StringComparison.Ordinal);
    }

    private static bool TextEqual(string? currentValue, string? incomingValue)
    {
        return string.Equals(
            (currentValue ?? string.Empty).Trim(),
            (incomingValue ?? string.Empty).Trim(),
            StringComparison.Ordinal);
    }

    private static string NormalizeTime(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (TimeSpan.TryParse(text, out var ts))
            return ts.ToString(@"hh\:mm\:ss");

        return text;
    }
}
