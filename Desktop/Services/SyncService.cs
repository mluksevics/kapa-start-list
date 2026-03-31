using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

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
    private readonly DbIsamRepository _dbIsamRepository;
    private readonly Func<AppSettings> _getSettings;
    private readonly Action<string> _log;

    private System.Windows.Forms.Timer? _timer;
    private CancellationTokenSource? _autoSyncCts;
    private int _autoSyncInProgress;

    public event Action? AutoSyncStarted;
    public event Action? AutoSyncFinished;

    public SyncService(
        ApiClient api,
        DbIsamRepository dbIsamRepository,
        Func<AppSettings> getSettings,
        Action<string> log)
    {
        _api = api;
        _dbIsamRepository = dbIsamRepository;
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
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        await UploadForcePushAsync(settings, date, minStartNumber: 1, maxStartNumber: 4000, ct);
    }

    /// <summary>Force-push only runners whose start numbers fall in the inclusive range (clamped to 1–4000 on scan).</summary>
    public async Task ForcePushSelectedAsync(int fromStartNumber, int toStartNumber, CancellationToken ct = default)
    {
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        await UploadForcePushAsync(settings, date, fromStartNumber, toStartNumber, ct);
    }

    public async Task<int> PullUpdatesSinceAsync(DateTimeOffset changedSinceUtc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        _log($"{Ts()} Pulling from API for date {date} since {changedSinceUtc:O}...");

        var pullResult = await _api.GetRunnersAsync(date, changedSinceUtc, ct);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL (since) failed — API unreachable.");
            return -1;
        }

        await Task.Run(() => _dbIsamRepository.ApplyPulledUpdates(settings, pullResult.Runners, ct), ct);
        _log($"{Ts()} Pull (since): pulled {pullResult.Runners.Count} runner(s).");
        return pullResult.Runners.Count;
    }

    /// <summary>Runs one sync cycle: PULL from API → write ChipNr changes to DBISAM.</summary>
    public async Task RunCycleAsync(bool forcePush, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");

        // 1. PULL
        _log($"{Ts()} Pulling from API for date {date}...");
        var watermark = settings.GetWatermark(date);
        DateTimeOffset? changedSince = watermark == DateTimeOffset.MinValue ? null : watermark;

        var pullResult = await _api.GetRunnersAsync(date, changedSince, ct);
        if (pullResult is null)
        {
            _log($"{Ts()} PULL failed — API unreachable.");
            return;
        }

        await Task.Run(() => _dbIsamRepository.ApplyPulledUpdates(settings, pullResult.Runners, ct), ct);

        // 6. Bulk push
        if (forcePush)
            await UploadForcePushAsync(settings, date, minStartNumber: 1, maxStartNumber: 4000, ct);
        else if (pullResult.Runners.Count == 0)
            _log($"{Ts()} Sync: no changes from API.");
        else
            _log($"{Ts()} Sync: pulled {pullResult.Runners.Count} runner(s).");

        // Update watermark
        settings.SetWatermark(date, pullResult.ServerTimeUtc);
        settings.Save();
    }

    private async Task UploadForcePushAsync(AppSettings settings, string date, int minStartNumber, int maxStartNumber, CancellationToken ct = default)
    {
        int lo = Math.Min(minStartNumber, maxStartNumber);
        int hi = Math.Max(minStartNumber, maxStartNumber);
        _log($"{Ts()} Force Push: scanning DBISAM {lo}–{hi} (workaround — ask Delphi dev for DbReadAllTeiln).");

        var runners = await Task.Run(() => _dbIsamRepository.ScanRunnersByStartNr(settings, minStartNumber, maxStartNumber, ct), ct);
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

    /// <summary>
    /// Pushes DBISAM rows that are absent from API (Registered/DNS set) or differ from API (e.g. MldKen/field changes), without scanning empty bibs.
    /// </summary>
    public async Task UploadNewRunnersFromIsamAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");

        _log($"{Ts()} Upload new: loading full start list from API...");
        var apiResponse = await _api.GetRunnersAsync(date, changedSince: null, ct);
        if (apiResponse is null)
        {
            _log($"{Ts()} Upload new: failed to read API (check URL/key).");
            return;
        }

        var apiByStart = apiResponse.Runners.ToDictionary(r => r.StartNumber);
        _log($"{Ts()} Upload new: API runner count={apiResponse.Runners.Count}.");

        _log($"{Ts()} Upload new: scanning DBISAM 1–4000...");
        var dbRunners = await Task.Run(() => _dbIsamRepository.ScanRunnersByStartNr(settings, ct), ct);
        var meaningful = dbRunners.Where(r => HasIsamParticipantData(r)).ToList();
        _log($"{Ts()} Upload new: DBISAM eligible rows={meaningful.Count}.");

        var now = DateTimeOffset.UtcNow;
        var toUpload = new List<BulkRunnerDto>();
        foreach (var db in meaningful)
        {
            apiByStart.TryGetValue(db.StartNumber, out var api);
            bool need = api is null || IsamRowDiffersFromApi(db, api);
            if (!need) continue;

            db.LastModifiedUtc = now;
            db.StatusId = api?.StatusId ?? 1;
            toUpload.Add(db);
        }

        if (toUpload.Count == 0)
        {
            _log($"{Ts()} Upload new: nothing to upload.");
            return;
        }

        _log($"{Ts()} Upload new: PUT {toUpload.Count} runner(s)...");
        var request = new BulkUploadRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = now,
            Runners = toUpload
        };
        var pushResult = await _api.BulkUploadAsync(date, request, ct);
        if (pushResult is null)
            _log($"{Ts()} Upload new: upload fault — API unreachable.");
        else
            _log($"{Ts()} Upload new: inserted={pushResult.Inserted} updated={pushResult.Updated} unchanged={pushResult.Unchanged} skippedAsOlder={pushResult.SkippedAsOlder}");
    }

    private static bool HasIsamParticipantData(BulkRunnerDto r) =>
        r.ClassId != 0
        || r.ClubId != 0
        || !string.IsNullOrWhiteSpace(r.Surname)
        || !string.IsNullOrWhiteSpace(r.Name)
        || !string.IsNullOrWhiteSpace(r.SiChipNo);

    private static bool IsamRowDiffersFromApi(BulkRunnerDto db, RunnerDto api)
    {
        if (!StringEqualNorm(db.SiChipNo, api.SiChipNo)) return true;
        if (!string.Equals(NormTxt(db.Name), NormTxt(api.Name), StringComparison.Ordinal))
            return true;
        if (!string.Equals(NormTxt(db.Surname), NormTxt(api.Surname), StringComparison.Ordinal))
            return true;
        if (db.ClassId != api.ClassId) return true;
        if (db.ClubId != api.ClubId) return true;
        if (!string.Equals(NormTxt(db.ClassName), NormTxt(api.ClassName), StringComparison.Ordinal))
            return true;
        if (!string.Equals(NormTxt(db.ClubName), NormTxt(api.ClubName), StringComparison.Ordinal))
            return true;
        if (!StringEqualNorm(db.Country, api.Country)) return true;
        if (db.StartPlace != api.StartPlace) return true;
        if (!string.Equals(NormStartTime(db.StartTime), NormStartTime(api.StartTime), StringComparison.Ordinal))
            return true;
        return false;
    }

    private static string NormTxt(string? s) => s?.Trim() ?? "";

    private static bool StringEqualNorm(string? a, string? b) =>
        string.Equals(NormTxt(a), NormTxt(b), StringComparison.Ordinal);

    private static string NormStartTime(string? t)
    {
        var s = NormTxt(t);
        if (s.Length == 0) return "";
        return TimeOnly.TryParse(s, out var to) ? to.ToString("HH:mm:ss") : s;
    }

    public async Task<UpsertLookupResponse?> PushClubsAsync(CancellationToken ct = default)
    {
        var settings = _getSettings();
        var runners = await Task.Run(() => _dbIsamRepository.ScanRunnersByStartNr(settings, ct), ct);
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

    private static DateOnly ResolveCompetitionDate(AppSettings settings)
    {
        if (DateOnly.TryParseExact(settings.CompetitionDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var configuredDate))
            return configuredDate;
        if (DateOnly.TryParse(settings.CompetitionDate, out configuredDate))
            return configuredDate;
        return DateOnly.FromDateTime(DateTime.Today);
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
