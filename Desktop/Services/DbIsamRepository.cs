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
        string? ClubName,
        int NcKen);  // 0=OK, 1=DNS, 2=DNF, 3=MP, 4=DQ; -1=unknown

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
                            continue;

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

        var foreignUpdates = pulledRunners
            .Where(r => !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool hasDbUpdates = chipUpdates.Count > 0 || katUpdates.Count > 0 ||
                            startTimeUpdates.Count > 0 || foreignUpdates.Count > 0;
        if (hasDbUpdates && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            db.TestMode = settings.IsTestMode;
            if (db.Open(settings.DbIsamPath))
            {
                var snapshots = new Dictionary<(int StartNumber, int DayNo), DbRunnerSnapshot?>();

                foreach (var r in foreignUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    bool applyName = HintIncludes(r, "Name");
                    bool applySurname = HintIncludes(r, "Surname");
                    bool applyClub = HintIncludes(r, "ClubId");
                    if (!applyName && !applySurname && !applyClub)
                        continue;

                    var snapshot = GetSnapshot(db, snapshots, settings.DayNo, r.StartNumber);
                    if (snapshot is null) continue;

                    if (applyName)
                    {
                        // API Name = first name = DBISAM Vorname
                        if (TextEqual(snapshot.Name, r.Name))
                            _log($"{Ts()} DBISAM Vorname #{r.StartNumber}: SKIP unchanged ('{r.Name}')");
                        else
                        {
                            var res = db.ChangeVornameByStartNr(r.StartNumber, r.Name ?? "");
                            _log($"{Ts()} DBISAM Vorname #{r.StartNumber} → '{r.Name}': {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                        }
                    }

                    if (applySurname)
                    {
                        // API Surname = last name = DBISAM Name
                        if (TextEqual(snapshot.Surname, r.Surname))
                            _log($"{Ts()} DBISAM Name #{r.StartNumber}: SKIP unchanged ('{r.Surname}')");
                        else
                        {
                            var res = db.ChangeNameByStartNr(r.StartNumber, r.Surname ?? "");
                            _log($"{Ts()} DBISAM Name #{r.StartNumber} → '{r.Surname}': {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                        }
                    }

                    if (applyClub)
                    {
                        if (snapshot.ClubId == r.ClubId)
                            _log($"{Ts()} DBISAM ClubNr #{r.StartNumber}: SKIP unchanged ({r.ClubId})");
                        else
                        {
                            var res = db.ChangeClubNrByStartNr(r.ClubId, r.StartNumber);
                            _log($"{Ts()} DBISAM ClubNr #{r.StartNumber} → {r.ClubId}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                        }
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

                // Fetch Nullzeit so we can detect pre-Nullzeit start times before calling the DLL.
                // The DLL stores Start1 as centiseconds relative to Nullzeit; negative offsets are rejected.
                var (etapResult, etapInfo) = db.GetEtapInfo(settings.DayNo);
                double? nullzeitSeconds = etapResult.Success ? etapInfo!.Nullzeit / 100.0 : null;
                if (nullzeitSeconds.HasValue)
                    _log($"{Ts()} Nullzeit for day {settings.DayNo}: {etapInfo!.NullzeitFormatted} ({etapInfo.Nullzeit} cs)");

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

                    // Guard: DLL rejects times before Nullzeit (relative offset would be negative).
                    if (nullzeitSeconds.HasValue && TimeSpan.TryParse(r.StartTime, out var startTs) &&
                        startTs.TotalSeconds < nullzeitSeconds.Value)
                    {
                        _log($"{Ts()} DBISAM StartTime #{r.StartNumber} → {r.StartTime}: SKIP — time is before Nullzeit ({etapInfo!.NullzeitFormatted}). DLL cannot store negative offsets. Fix in Delphi DLL needed.");
                        continue;
                    }

                    var res = db.ChangeStartTimeByStartNr(settings.DayNo, r.StartTime!, r.StartNumber);
                    _log($"{Ts()} DBISAM StartTime #{r.StartNumber} → {r.StartTime}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                }

                foreach (var r in foreignUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (r.StatusId == 3)
                    {
                        var res = db.SetDNSByStartNr(settings.DayNo, r.StartNumber);
                        _log($"{Ts()} DBISAM DNS set #{r.StartNumber}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                    }
                    else if (r.StatusId == 1 && HintIncludes(r, "StatusId"))
                    {
                        // Only clear DNS if runner currently has DNS status in DBISAM.
                        // Do not touch DNF/MP/DQ (NCKen 2/3/4) or already-OK (NCKen 0).
                        var snapshot = GetSnapshot(db, snapshots, settings.DayNo, r.StartNumber);
                        if (snapshot is not null && snapshot.NcKen != 1)
                        {
                            _log($"{Ts()} DBISAM DNS clear #{r.StartNumber}: SKIP — current NCKen={snapshot.NcKen} (not DNS)");
                            continue;
                        }
                        var res = db.ClearDNSByStartNr(settings.DayNo, r.StartNumber);
                        _log($"{Ts()} DBISAM DNS clear #{r.StartNumber}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                    }
                }
            }
            else
            {
                _log($"{Ts()} DBISAM not available — writes skipped. Check DB path and DbBridge.dll.");
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
        int ncKen = int.TryParse(GetField(fields, $"NCKen{dayNo}"), out var nc) ? nc : -1;

        var snapshot = new DbRunnerSnapshot(chip, classId, start, name, surname, clubId, clubName, ncKen);
        cache[key] = snapshot;
        return snapshot;
    }

    // ── CSV-based bulk reads ─────────────────────────────────────────────────

    /// <summary>
    /// Loads all participants for the configured day in one DLL call using DbGetAllTeilnDay.
    /// Joins club/class names from DbGetAllClubs and DbGetAllClasses.
    /// NCKen=1 (DNS) is mapped to StatusId=3; all others to StatusId=1.
    /// </summary>
    public List<BulkRunnerDto> GetAllRunnersByDay(AppSettings settings, CancellationToken ct = default)
    {
        var results = new List<BulkRunnerDto>();

        if (string.IsNullOrEmpty(settings.DbIsamPath))
        {
            _log($"{Ts()} GetAllRunners: DB path not set.");
            return results;
        }

        using var db = new DbBridgeService(_log);
        if (!db.Open(settings.DbIsamPath))
        {
            _log($"{Ts()} GetAllRunners: cannot open DBISAM.");
            return results;
        }

        var clubNames = new Dictionary<int, string>();
        var (clubRes, clubCsv) = db.GetAllClubs();
        if (clubRes.Success && clubCsv != null)
            clubNames = ParseClubsCsvToDict(clubCsv);

        var classInfo = new Dictionary<int, (string Name, int StartPlace)>();
        var (classRes, classCsv) = db.GetAllClasses();
        if (classRes.Success && classCsv != null)
            classInfo = ParseClassesCsvToDict(classCsv, settings.DayNo);

        var (teilnRes, teilnCsv) = db.GetAllTeilnDay(settings.DayNo);
        if (!teilnRes.Success || teilnCsv == null)
        {
            _log($"{Ts()} GetAllRunners: GetAllTeilnDay failed: {teilnRes.Message}");
            return results;
        }

        _log($"{Ts()} GetAllRunners: CSV received, {teilnCsv.Length} chars");
        var lines = teilnCsv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        _log($"{Ts()} GetAllRunners: {lines.Length} lines (header + data)");
        if (lines.Length < 2) { _log($"{Ts()} GetAllRunners: no data rows in CSV"); return results; }

        var hdr = ParseCsvHeader(lines[0]);
        int iStartNr  = Col(hdr, "StartNr");
        int iName     = Col(hdr, "Name");
        int iVorname  = Col(hdr, "Vorname");
        int iKatNr    = Col(hdr, "KatNr");
        int iClubNr   = Col(hdr, "ClubNr");
        int iChipNr   = Col(hdr, "ChipNr");
        int iMldKen   = Col(hdr, "MldKen");
        int iNcKen    = Col(hdr, "NCKen");
        int iStart    = Col(hdr, "Start");
        if (iStartNr < 0) { _log($"{Ts()} GetAllRunners: 'StartNr' column not found in: {lines[0]}"); return results; }

        var now = DateTimeOffset.UtcNow;
        for (int i = 1; i < lines.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var parts = lines[i].Split(';');

            if (!int.TryParse(GetCol(parts, iStartNr), out int startNr) || startNr <= 0) continue;

            if (string.Equals(GetCol(parts, iMldKen), "False", StringComparison.OrdinalIgnoreCase))
                continue;

            int classId = int.TryParse(GetCol(parts, iKatNr), out var k) ? k : 0;
            int clubId = int.TryParse(GetCol(parts, iClubNr), out var c) ? c : 0;
            int chipNr = int.TryParse(GetCol(parts, iChipNr), out var ch) ? ch : 0;
            int ncKen = int.TryParse(GetCol(parts, iNcKen), out var nc) ? nc : 0;
            var startTime = GetCol(parts, iStart);

            classInfo.TryGetValue(classId, out var cls);
            clubNames.TryGetValue(clubId, out var clubName);

            results.Add(new BulkRunnerDto
            {
                StartNumber = startNr,
                Surname = GetCol(parts, iName) ?? "",
                Name = GetCol(parts, iVorname) ?? "",
                ClassId = classId,
                ClassName = cls.Name ?? "",
                ClubId = clubId,
                ClubName = clubName ?? "",
                SiChipNo = chipNr > 0 ? chipNr.ToString() : null,
                StartTime = string.IsNullOrEmpty(startTime) ? null : startTime,
                StatusId = ncKen == 1 ? 3 : 1,
                LastModifiedUtc = now
            });
        }

        _log($"{Ts()} GetAllRunners: {results.Count} runners loaded (day {settings.DayNo}).");
        return results;
    }

    /// <summary>Returns all clubs from DBISAM as lookup items (ClubNr → Ort).</summary>
    public List<LookupItemDto> GetAllClubs(AppSettings settings)
    {
        var result = new List<LookupItemDto>();
        if (string.IsNullOrEmpty(settings.DbIsamPath)) return result;

        using var db = new DbBridgeService(_log);
        if (!db.Open(settings.DbIsamPath)) return result;

        var (res, csv) = db.GetAllClubs();
        if (!res.Success || csv == null)
        {
            _log($"{Ts()} GetAllClubs: failed: {res.Message}");
            return result;
        }

        _log($"{Ts()} GetAllClubs: CSV received, {csv.Length} chars");
        var lines = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        _log($"{Ts()} GetAllClubs: {lines.Length} lines (header + data)");
        if (lines.Length < 2) { _log($"{Ts()} GetAllClubs: no data rows in CSV"); return result; }

        var hdr = ParseCsvHeader(lines[0]);
        int iClubNr = Col(hdr, "ClubNr");
        int iOrt    = Col(hdr, "Ort");
        if (iClubNr < 0) { _log($"{Ts()} GetAllClubs: 'ClubNr' column not found in: {lines[0]}"); return result; }

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(';');
            if (int.TryParse(GetCol(parts, iClubNr), out int id) && id > 0)
                result.Add(new LookupItemDto { Id = id, Name = GetCol(parts, iOrt) ?? "" });
        }

        _log($"{Ts()} GetAllClubs: {result.Count} clubs loaded.");
        return result;
    }

    /// <summary>Returns all classes from DBISAM as lookup items (KatNr → KatKurz, StartPlatz for DayNo).</summary>
    public List<LookupItemDto> GetAllClasses(AppSettings settings)
    {
        var result = new List<LookupItemDto>();
        if (string.IsNullOrEmpty(settings.DbIsamPath)) return result;

        using var db = new DbBridgeService(_log);
        if (!db.Open(settings.DbIsamPath)) return result;

        var (res, csv) = db.GetAllClasses();
        if (!res.Success || csv == null)
        {
            _log($"{Ts()} GetAllClasses: failed: {res.Message}");
            return result;
        }

        _log($"{Ts()} GetAllClasses: CSV received, {csv.Length} chars");
        var lines = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        _log($"{Ts()} GetAllClasses: {lines.Length} lines (header + data)");
        if (lines.Length < 2) { _log($"{Ts()} GetAllClasses: no data rows in CSV"); return result; }

        var hdr = ParseCsvHeader(lines[0]);
        int iKatNr      = Col(hdr, "KatNr");
        int iKatKurz    = Col(hdr, "KatKurz");
        int iStartPlatz = Col(hdr, $"StartPlatz{settings.DayNo}");
        if (iKatNr < 0) { _log($"{Ts()} GetAllClasses: 'KatNr' column not found in: {lines[0]}"); return result; }

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(';');
            if (int.TryParse(GetCol(parts, iKatNr), out int id) && id > 0)
            {
                int startPlace = int.TryParse(GetCol(parts, iStartPlatz), out var sp) ? sp : 0;
                result.Add(new LookupItemDto { Id = id, Name = GetCol(parts, iKatKurz) ?? "", StartPlace = startPlace });
            }
        }

        _log($"{Ts()} GetAllClasses: {result.Count} classes loaded.");
        return result;
    }

    private static Dictionary<string, int> ParseCsvHeader(string line)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var cols = line.Split(';');
        for (int i = 0; i < cols.Length; i++)
            result[cols[i].Trim()] = i;
        return result;
    }

    /// <summary>Returns the column index, or -1 if the column name is not in the header.</summary>
    private static int Col(Dictionary<string, int> hdr, string name) =>
        hdr.TryGetValue(name, out int i) ? i : -1;

    private static string? GetCol(string[] parts, int index) =>
        index >= 0 && index < parts.Length ? parts[index].Trim() : null;

    private static Dictionary<int, string> ParseClubsCsvToDict(string csv)
    {
        var result = new Dictionary<int, string>();
        var lines = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return result;
        var hdr = ParseCsvHeader(lines[0]);
        int iId   = Col(hdr, "ClubNr");
        int iName = Col(hdr, "Ort");
        if (iId < 0) return result;
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(';');
            if (int.TryParse(GetCol(parts, iId), out int id) && id > 0)
                result[id] = GetCol(parts, iName) ?? "";
        }
        return result;
    }

    private static Dictionary<int, (string Name, int StartPlace)> ParseClassesCsvToDict(string csv, int dayNo)
    {
        var result = new Dictionary<int, (string, int)>();
        var lines = csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return result;
        var hdr = ParseCsvHeader(lines[0]);
        int iId         = Col(hdr, "KatNr");
        int iName       = Col(hdr, "KatKurz");
        int iStartPlatz = Col(hdr, $"StartPlatz{dayNo}");
        if (iId < 0) return result;
        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(';');
            if (int.TryParse(GetCol(parts, iId), out int id) && id > 0)
            {
                int sp = int.TryParse(GetCol(parts, iStartPlatz), out var s) ? s : 0;
                result[id] = (GetCol(parts, iName) ?? "", sp);
            }
        }
        return result;
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
