namespace StartRef.Desktop;

/// <summary>
/// Sync service between the StartRef API and DBISAM (via DbBridge DLL).
///
/// Each cycle:
///   1. PULL: GET /runners from API → get latest state from field devices
///   2. WRITE to DBISAM: write ChipNr changes from field devices back to DBISAM
///   3. SKIP (not yet supported by DLL):
///        - DNS write-back (needs DbChangeDnsByStartNr)
///        - Bulk read/push (needs DbReadAllTeiln)
///
/// DLL gaps to request from the Delphi developer:
///   • DbReadAllTeiln(ctx, buffer, bufferSize) — enumerate all participants
///   • DbChangeDnsByStartNr(ctx, startNr, dnsFlag) — set DNS/AufgabeTyp
/// </summary>
public class SyncService
{
    private readonly ApiClient _api;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string> _log;

    private System.Windows.Forms.Timer? _timer;
    private CancellationTokenSource? _autoSyncCts;
    private int _autoSyncInProgress;

    public event Action? AutoSyncStarted;
    public event Action? AutoSyncFinished;

    public SyncService(
        ApiClient api,
        Func<AppSettings> getSettings,
        Action<string> log)
    {
        _api = api;
        _getSettings = getSettings;
        _log = log;
    }

    public void Start()
    {
        _timer?.Stop();
        _timer?.Dispose();

        var settings = _getSettings();
        _timer = new System.Windows.Forms.Timer
        {
            Interval = settings.SyncIntervalSeconds * 1000
        };
        _timer.Tick += async (_, _) =>
        {
            if (_getSettings().AutoSyncEnabled)
                _ = Task.Run(RunAutoSyncTickAsync);
            await Task.CompletedTask;
        };
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    public void CancelAutoSync()
    {
        if (_autoSyncCts is null || _autoSyncCts.IsCancellationRequested) return;
        _autoSyncCts.Cancel();
    }

    public void UpdateInterval(int seconds)
    {
        if (_timer is null) return;
        _timer.Interval = seconds * 1000;
    }

    public async Task ForcePushAllAsync(CancellationToken ct = default)
    {
        var settings = _getSettings();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        await UploadForcePushAsync(settings, today, ct);
    }

    public async Task<int> PullUpdatesSinceAsync(DateTimeOffset changedSinceUtc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        _log($"{Ts()} Pulling from API since {changedSinceUtc:O}...");

        var pullResult = await _api.GetRunnersAsync(today, changedSinceUtc, ct);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL (since) failed — API unreachable.");
            return -1;
        }

        await Task.Run(() => ApplyPulledUpdatesToDbIsam(settings, pullResult.Runners, ct), ct);
        _log($"{Ts()} Pull (since): pulled {pullResult.Runners.Count} runner(s).");
        return pullResult.Runners.Count;
    }

    /// <summary>Runs one sync cycle: PULL from API → write ChipNr changes to DBISAM.</summary>
    public async Task RunCycleAsync(bool forcePush, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");

        // 1. PULL
        _log($"{Ts()} Pulling from API...");
        DateTimeOffset? changedSince = settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? null
            : settings.LastServerTimeUtc;

        var pullResult = await _api.GetRunnersAsync(today, changedSince, ct);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL failed — API unreachable.");
            return;
        }

        await Task.Run(() => ApplyPulledUpdatesToDbIsam(settings, pullResult.Runners, ct), ct);

        // 6. Bulk push
        if (forcePush)
            await UploadForcePushAsync(settings, today, ct);
        else if (pullResult.Runners.Count == 0)
            _log($"{Ts()} Sync: no changes from API.");
        else
            _log($"{Ts()} Sync: pulled {pullResult.Runners.Count} runner(s).");

        // Update watermark
        settings.LastServerTimeUtc = pullResult.ServerTimeUtc;
        settings.Save();
    }

    /// <summary>
    /// WORKAROUND: iterates StartNr 1–4000, uses GetIdNrListByStartNr to find runners,
    /// then calls GetTeilnInfoByIdNr for each to populate Name, Surname, ClassName, ClubName, ChipNr.
    /// Buffer format confirmed: Key=Value lines (Grupa, IdNr, StartNr, Name, Vorname, Klubs,
    /// ChipNr1..3, KatNr, Start1Raw, Start1, Start2Raw, Start2, Start3Raw, Start3, MldKen1..3, Rent).
    /// Replace with DbReadAllTeiln when the Delphi developer adds that bulk-read function.
    /// </summary>
    private List<BulkRunnerDto> ScanRunnersByStartNr(AppSettings settings, CancellationToken ct = default)
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

                // Parse IdNr(s) from the buffer (integers separated by comma/newline/space)
                var idNrs = DbBridgeService.ParseIdNrList(listRaw);
                int idNr = idNrs.Count > 0 ? idNrs[0] : 0;

                var dto = new BulkRunnerDto { StartNumber = startNr, StatusId = 1 };

                if (idNr > 0)
                {
                    var (infoResult, infoRaw) = db.GetTeilnInfoByIdNr(idNr);
                    if (infoResult.Success && !string.IsNullOrEmpty(infoRaw))
                    {
                        var fields = DbBridgeService.ParseTeilnInfo(infoRaw);
                        // Field names confirmed from actual DllTest.exe output:
                        // Grupa, IdNr, Rent, StartNr, ClubNr, KatNr, Name, Vorname, Klubs,
                        // ChipNr1..3, MldKen1..3, Start1Raw, Start1, Start2Raw, Start2, Start3Raw, Start3
                        dto.Surname = GetField(fields, "Name") ?? "";
                        dto.Name = GetField(fields, "Vorname") ?? "";
                        dto.ClassId = int.TryParse(GetField(fields, "KatNr"), out var katNr) ? katNr : 0;
                        dto.ClassName = GetField(fields, "Grupa") ?? "";
                        dto.ClubId = int.TryParse(GetField(fields, "ClubNr"), out var clubNr) ? clubNr : 0;
                        dto.ClubName = GetField(fields, "Klubs") ?? "";
                        var chipStr = GetField(fields, "ChipNr1", "ChipNr2", "ChipNr3");
                        if (!string.IsNullOrEmpty(chipStr)) dto.SiChipNo = chipStr;
                        dto.StartTime = GetField(fields, $"Start{settings.DayNo}"); // "HH:mm:ss"
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

    private static string? GetField(Dictionary<string, string> fields, params string[] names)
    {
        foreach (var name in names)
            if (fields.TryGetValue(name, out var val) && !string.IsNullOrWhiteSpace(val))
                return val;
        return null;
    }

    private async Task UploadForcePushAsync(AppSettings settings, string date, CancellationToken ct = default)
    {
        _log($"{Ts()} Force Push: scanning DBISAM 1–4000 (workaround — ask Delphi dev for DbReadAllTeiln).");

        var runners = await Task.Run(() => ScanRunnersByStartNr(settings, ct), ct);
        if (runners.Count == 0)
        {
            _log($"{Ts()} Force Push: no runners found in scan — check DB path.");
            return;
        }

        var request = new BulkUploadRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            Runners = runners
        };
        var pushResult = await _api.BulkUploadAsync(date, request, ct);
        if (pushResult is null)
            _log($"{Ts()} Force Push: upload failed — API unreachable.");
        else
            _log($"{Ts()} Force Push: inserted={pushResult.Inserted} updated={pushResult.Updated} unchanged={pushResult.Unchanged}");
    }

    public async Task<UpsertLookupResponse?> PushClubsAsync(CancellationToken ct = default)
    {
        var settings = _getSettings();
        var runners = await Task.Run(() => ScanRunnersByStartNr(settings, ct), ct);
        var clubs = runners
            .Where(r => r.ClubId > 0 && !string.IsNullOrWhiteSpace(r.ClubName))
            .GroupBy(r => r.ClubId)
            .Select(g => new LookupItemDto
            {
                Id = g.Key,
                Name = g.First().ClubName.Trim()
            })
            .Where(x => x.Name.Length > 0)
            .OrderBy(x => x.Id)
            .ToList();

        if (clubs.Count == 0)
        {
            _log($"{Ts()} Push Clubs: nothing to push.");
            return new UpsertLookupResponse();
        }

        var response = await _api.UpsertClubsAsync(new UpsertLookupRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            Items = clubs
        }, ct);

        if (response is null)
            _log($"{Ts()} Push Clubs: upload failed — API unreachable.");
        else
            _log($"{Ts()} Push Clubs: inserted={response.Inserted} updated={response.Updated} unchanged={response.Unchanged}");

        return response;
    }

    private static string Ts() => DateTime.Now.ToString("HH:mm:ss");

    private void ApplyPulledUpdatesToDbIsam(AppSettings settings, List<RunnerDto> pulledRunners, CancellationToken ct)
    {
        // Log field-device status changes
        foreach (var r in pulledRunners)
        {
            ct.ThrowIfCancellationRequested();
            if (r.StatusId != 1)
                _log($"{Ts()} PULL #{r.StartNumber} {r.Name} {r.Surname} – Status={r.StatusName} (by: {r.LastModifiedBy})");
        }

        // Write ChipNr changes to DBISAM (runners modified by field devices, not desktop)
        var chipUpdates = pulledRunners
            .Where(r => !string.IsNullOrEmpty(r.SiChipNo) &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chipUpdates.Count > 0 && !string.IsNullOrEmpty(settings.DbIsamPath))
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
            }
            else
            {
                _log($"{Ts()} DBISAM not available — ChipNr writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        // Write KatNr changes to DBISAM (runners modified by field devices, not desktop)
        var katUpdates = pulledRunners
            .Where(r => r.ClassId > 0 &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (katUpdates.Count > 0 && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            if (db.Open(settings.DbIsamPath))
            {
                foreach (var r in katUpdates)
                {
                    ct.ThrowIfCancellationRequested();
                    var res = db.ChangeKatNrByStartNr(r.ClassId, r.StartNumber);
                    if (!res.Success)
                        _log($"{Ts()} DBISAM KatNr #{r.StartNumber} → {r.ClassId}: ERROR {res.Message}");
                }
            }
            else
            {
                _log($"{Ts()} DBISAM not available — KatNr writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        // Write StartTime changes to DBISAM (runners modified by field devices, not desktop)
        var startTimeUpdates = pulledRunners
            .Where(r => r.StartTime != null &&
                        !string.Equals(r.LastModifiedBy, settings.DeviceName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (startTimeUpdates.Count > 0 && !string.IsNullOrEmpty(settings.DbIsamPath))
        {
            using var db = new DbBridgeService(_log);
            if (db.Open(settings.DbIsamPath))
            {
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
                _log($"{Ts()} DBISAM not available — StartTime writes skipped. Check DB path and DbBridge.dll.");
            }
        }

        // DNS write — not yet supported by DLL
        int dnsCount = pulledRunners.Count(r => r.StatusId == 3);
        if (dnsCount > 0)
            _log($"{Ts()} [WARN] {dnsCount} DNS runner(s) NOT written to DBISAM — DLL lacks DbChangeDnsByStartNr.");
    }

    private async Task RunAutoSyncTickAsync()
    {
        if (Interlocked.CompareExchange(ref _autoSyncInProgress, 1, 0) != 0) return;
        _autoSyncCts = new CancellationTokenSource();
        AutoSyncStarted?.Invoke();
        try
        {
            await RunCycleAsync(forcePush: false, _autoSyncCts.Token);
        }
        catch (OperationCanceledException)
        {
            _log($"{Ts()} Auto-sync cancelled.");
        }
        finally
        {
            _autoSyncCts.Dispose();
            _autoSyncCts = null;
            Interlocked.Exchange(ref _autoSyncInProgress, 0);
            AutoSyncFinished?.Invoke();
        }
    }
}
