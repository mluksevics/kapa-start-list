using StartRef.Desktop.DbBridge;
using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

public class DbIsamRepository
{
    private readonly Action<string> _log;

    public DbIsamRepository(Action<string> log)
    {
        _log = log;
    }

    public List<BulkRunnerDto> ScanRunnersByStartNr(AppSettings settings, CancellationToken ct = default)
    {
        const int maxStartNr = 4000;
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

        _log($"{Ts()} Scanning start numbers 1–{maxStartNr}...");

        for (int startNr = 1; startNr <= maxStartNr; startNr++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (listResult, listRaw) = db.GetIdNrListByStartNr(startNr);
                if (!listResult.Success) continue;

                var idNrs = DbBridgeService.ParseIdNrList(listRaw);
                int idNr = idNrs.Count > 0 ? idNrs[0] : 0;

                var dto = new BulkRunnerDto { StartNumber = startNr, StatusId = 1, LastModifiedUtc = DateTimeOffset.UtcNow };

                if (idNr > 0)
                {
                    var (infoResult, infoRaw) = db.GetTeilnInfoByIdNr(idNr);
                    if (infoResult.Success && !string.IsNullOrEmpty(infoRaw))
                    {
                        var fields = DbBridgeService.ParseTeilnInfo(infoRaw);
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

            if (results.Count > 0 && results.Count % 100 == 0)
                _log($"{Ts()} Scanned {startNr}/{maxStartNr}, found {results.Count} so far...");
        }

        _log($"{Ts()} Scan complete: {results.Count} runners found in {maxStartNr} start numbers.");
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
            if (r.StatusId != 1)
                _log($"{Ts()} PULL #{r.StartNumber} {r.Name} {r.Surname} – Status={r.StatusName} (by: {r.LastModifiedBy})");
        }

        var chipUpdates = pulledRunners
            .Where(r => !string.IsNullOrEmpty(r.SiChipNo) &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var katUpdates = pulledRunners
            .Where(r => r.ClassId > 0 &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var startTimeUpdates = pulledRunners
            .Where(r => r.StartTime != null &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool hasDbUpdates = chipUpdates.Count > 0 || katUpdates.Count > 0 || startTimeUpdates.Count > 0;
        if (hasDbUpdates && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            if (db.Open(settings.DbIsamPath))
            {
                foreach (var r in chipUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!int.TryParse(r.SiChipNo, out int chipNr)) continue;
                    var res = db.ChangeChipNrByStartNr(settings.DayNo, chipNr, r.StartNumber);
                    _log($"{Ts()} DBISAM ChipNr #{r.StartNumber} → {chipNr}: {(res.Success ? "OK" : $"ERROR {res.Message}")}");
                }

                foreach (var r in katUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    var res = db.ChangeKatNrByStartNr(r.ClassId, r.StartNumber);
                    if (!res.Success)
                        _log($"{Ts()} DBISAM KatNr #{r.StartNumber} → {r.ClassId}: ERROR {res.Message}");
                }

                foreach (var r in startTimeUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    var res = db.ChangeStartTimeByStartNr(settings.DayNo, r.StartTime!, r.StartNumber);
                    if (!res.Success)
                        _log($"{Ts()} DBISAM StartTime #{r.StartNumber} → {r.StartTime}: ERROR {res.Message}");
                }
            }
            else
            {
                _log($"{Ts()} DBISAM not available — writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        int dnsCount = pulledRunners.Count(r => r.StatusId == 3);
        if (dnsCount > 0)
            _log($"{Ts()} [WARN] {dnsCount} DNS runner(s) NOT written to DBISAM — DLL lacks DbChangeDnsByStartNr.");
    }

    private static string? GetField(Dictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
            if (fields.TryGetValue(name, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        return null;
    }

    private static string Ts() => DateTime.Now.ToString("HH:mm:ss");
}
