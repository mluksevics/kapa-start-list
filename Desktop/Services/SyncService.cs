using StartRef.Desktop.Models;

namespace StartRef.Desktop.Services;

/// <summary>
/// Sync service between the StartRef API and DBISAM (via DbBridge DLL).
///
/// Each cycle:
///   1. PULL: GET /runners from API → get latest state from field devices
///   2. WRITE to DBISAM: apply Name/Surname/Club/ChipNr/KatNr/StartTime/DNS changes
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

    private System.Windows.Forms.Timer? _autoPushTimer;
    private CancellationTokenSource? _autoPushCts;
    private int _autoPushInProgress;

    public event Action? AutoSyncStarted;
    public event Action? AutoSyncFinished;

    /// <summary>Raised on the thread pool when an auto-push cycle completes (success or error).</summary>
    public event Action<DateTimeOffset>? AutoPushCompleted;

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

    public void StartAutoPush(int intervalSeconds)
    {
        _autoPushTimer?.Stop();
        _autoPushTimer?.Dispose();
        _autoPushTimer = new System.Windows.Forms.Timer { Interval = Math.Max(10, intervalSeconds) * 1000 };
        _autoPushTimer.Tick += async (_, _) =>
        {
            _ = Task.Run(RunAutoPushTickAsync);
            await Task.CompletedTask;
        };
        _autoPushTimer.Start();
    }

    public void StopAutoPush()
    {
        _autoPushTimer?.Stop();
        _autoPushCts?.Cancel();
    }

    public void UpdateAutoPushInterval(int seconds)
    {
        if (_autoPushTimer is null) return;
        _autoPushTimer.Interval = Math.Max(10, seconds) * 1000;
    }

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
    public async Task RunCycleAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");

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

        if (pullResult.Runners.Count == 0)
            _log($"{Ts()} Sync: no changes from API.");
        else
            _log($"{Ts()} Sync: pulled {pullResult.Runners.Count} runner(s).");

        settings.SetWatermark(date, pullResult.ServerTimeUtc);
        settings.Save();
    }

    private async Task UploadForcePushAsync(AppSettings settings, string date, int minStartNumber, int maxStartNumber, CancellationToken ct = default)
    {
        int lo = Math.Min(minStartNumber, maxStartNumber);
        int hi = Math.Max(minStartNumber, maxStartNumber);
        bool isFullRange = lo == 1 && hi == 4000;
        _log($"{Ts()} Force Push: loading DBISAM{(isFullRange ? "" : $" {lo}–{hi}")}...");

        var allRunners = await Task.Run(() => _dbIsamRepository.GetAllRunnersByDay(settings, ct), ct);
        var runners = isFullRange
            ? allRunners
            : allRunners.Where(r => r.StartNumber >= lo && r.StartNumber <= hi).ToList();
        _log($"{Ts()} Force Push: DBISAM loaded {allRunners.Count} total, {runners.Count} in range.");

        if (runners.Count == 0)
        {
            _log($"{Ts()} Force Push: no runners found — check DB path.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        int totalInserted = 0, totalUpdated = 0, totalUnchanged = 0;
        var chunks = runners.Chunk(100).ToList();
        _log($"{Ts()} Force Push: uploading {runners.Count} runner(s) in {chunks.Count} chunk(s)...");

        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = chunks[i];
            var request = new BulkUploadRequest
            {
                Source = settings.DeviceName,
                LastModifiedUtc = now,
                Runners = chunk.ToList()
            };
            var result = await _api.BulkUploadAsync(date, request, touchAll: true, ct);
            if (result is null)
            {
                _log($"{Ts()} Force Push: chunk {i + 1}/{chunks.Count} failed — {_api.LastError ?? "API unreachable"}");
                return;
            }
            totalInserted += result.Inserted;
            totalUpdated += result.Updated;
            totalUnchanged += result.Unchanged;
            _log($"{Ts()} Force Push: chunk {i + 1}/{chunks.Count} done (ins={result.Inserted} upd={result.Updated} unch={result.Unchanged})");
        }

        _log($"{Ts()} Force Push: all done — inserted={totalInserted} updated={totalUpdated} unchanged={totalUnchanged}");
    }

    /// <summary>Push all eligible rows from DBISAM in chunks of 100; API compares and only bumps LastModified when values differ.</summary>
    public async Task PushAllChangesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        _log($"{Ts()} Push Updates: loading DBISAM...");

        var runners = await Task.Run(() => _dbIsamRepository.GetAllRunnersByDay(settings, ct), ct);
        var meaningful = runners.Where(r => HasIsamParticipantData(r)).ToList();
        _log($"{Ts()} Push Updates: DBISAM loaded {runners.Count} total, {meaningful.Count} eligible.");
        if (meaningful.Count == 0)
        {
            _log($"{Ts()} Push Updates: no eligible runners.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var r in meaningful)
            r.LastModifiedUtc = now;

        int totalInserted = 0, totalUpdated = 0, totalUnchanged = 0;
        var chunks = meaningful.Chunk(100).ToList();
        _log($"{Ts()} Push Updates: uploading {meaningful.Count} runner(s) in {chunks.Count} chunk(s)...");

        for (int i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = chunks[i];
            var request = new BulkUploadRequest
            {
                Source = settings.DeviceName,
                LastModifiedUtc = now,
                Runners = chunk.ToList()
            };
            var result = await _api.BulkUploadAsync(date, request, touchAll: false, ct);
            if (result is null)
            {
                _log($"{Ts()} Push Updates: chunk {i + 1}/{chunks.Count} failed — {_api.LastError ?? "API unreachable"}");
                return;
            }
            totalInserted += result.Inserted;
            totalUpdated += result.Updated;
            totalUnchanged += result.Unchanged;
            _log($"{Ts()} Push Updates: chunk {i + 1}/{chunks.Count} done (ins={result.Inserted} upd={result.Updated} unch={result.Unchanged})");
        }

        _log($"{Ts()} Push Updates: all done — inserted={totalInserted} updated={totalUpdated} unchanged={totalUnchanged}");
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

        _log($"{Ts()} Upload new: loading DBISAM...");
        var dbRunners = await Task.Run(() => _dbIsamRepository.GetAllRunnersByDay(settings, ct), ct);
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

        _log($"{Ts()} Upload new: uploading {toUpload.Count} runner(s) to API...");
        var request = new BulkUploadRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = now,
            Runners = toUpload
        };
        var pushResult = await _api.BulkUploadAsync(date, request, touchAll: false, ct);
        if (pushResult is null)
            _log($"{Ts()} Upload new: upload fault — API unreachable.");
        else
            _log($"{Ts()} Upload new: inserted={pushResult.Inserted} updated={pushResult.Updated} unchanged={pushResult.Unchanged}");
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

    /// <summary>
    /// Reset API day data: delete all existing data for the competition date, then re-upload
    /// all runners, clubs, and classes from DBISAM.
    /// </summary>
    public async Task ResetDayDataAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");

        // Step 1: Delete all existing data on the API
        _log($"{Ts()} Reset: step 1/4 — deleting API data for {date}...");
        var deleteResult = await _api.DeleteCompetitionDataAsync(date, ct);
        if (deleteResult is null)
        {
            _log($"{Ts()} Reset: delete failed — {_api.LastError ?? "API unreachable"}. Aborting reset.");
            return;
        }
        _log($"{Ts()} Reset: deleted runners={deleteResult.DeletedRunners} competitions={deleteResult.DeletedCompetitions} classes={deleteResult.DeletedClasses} clubs={deleteResult.DeletedClubs}");

        // Step 2: Push clubs
        _log($"{Ts()} Reset: step 2/4 — uploading clubs...");
        await PushClubsAsync(ct);

        // Step 3: Push classes
        _log($"{Ts()} Reset: step 3/4 — uploading classes...");
        await PushClassesAsync(ct);

        // Step 4: Force push all runners
        _log($"{Ts()} Reset: step 4/4 — uploading all runners (force push)...");
        await ForcePushAllAsync(ct);

        // Clear watermark so next sync does a full pull
        settings.SetWatermark(date, DateTimeOffset.MinValue);
        settings.Save();

        _log($"{Ts()} Reset: completed for {date}.");
    }

    public async Task<UpsertLookupResponse?> PushClubsAsync(CancellationToken ct = default)
    {
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        var clubs = await Task.Run(() => _dbIsamRepository.GetAllClubs(settings), ct);
        _log($"{Ts()} Push Clubs: DBISAM loaded {clubs.Count} club(s).");

        if (clubs.Count == 0)
        {
            _log($"{Ts()} Push Clubs: nothing to push.");
            return new UpsertLookupResponse();
        }

        _log($"{Ts()} Push Clubs: uploading {clubs.Count} club(s) to API...");
        var response = await _api.UpsertClubsAsync(date, new UpsertLookupRequest
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

    public async Task<UpsertLookupResponse?> PushClassesAsync(CancellationToken ct = default)
    {
        var settings = _getSettings();
        var date = ResolveCompetitionDate(settings).ToString("yyyy-MM-dd");
        var classes = await Task.Run(() => _dbIsamRepository.GetAllClasses(settings), ct);
        _log($"{Ts()} Push Classes: DBISAM loaded {classes.Count} class(es).");

        if (classes.Count == 0)
        {
            _log($"{Ts()} Push Classes: nothing to push.");
            return new UpsertLookupResponse();
        }

        _log($"{Ts()} Push Classes: uploading {classes.Count} class(es) to API...");
        var response = await _api.UpsertClassesAsync(date, new UpsertLookupRequest
        {
            Source = settings.DeviceName,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            Items = classes
        }, ct);

        if (response is null)
            _log($"{Ts()} Push Classes: upload failed — API unreachable.");
        else
            _log($"{Ts()} Push Classes: inserted={response.Inserted} updated={response.Updated} unchanged={response.Unchanged}");

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

    private async Task RunAutoPushTickAsync()
    {
        if (Interlocked.CompareExchange(ref _autoPushInProgress, 1, 0) != 0) return;
        // Skip if auto-pull is running to avoid concurrent DBISAM access.
        if (_autoSyncInProgress == 1) { Interlocked.Exchange(ref _autoPushInProgress, 0); return; }
        _autoPushCts = new CancellationTokenSource();
        try
        {
            _log($"{Ts()} Auto-push started.");
            await PushAllChangesAsync(_autoPushCts.Token);
            AutoPushCompleted?.Invoke(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            _log($"{Ts()} Auto-push cancelled.");
        }
        catch (Exception ex)
        {
            _log($"{Ts()} Auto-push error: {ex.Message}");
        }
        finally
        {
            _autoPushCts.Dispose();
            _autoPushCts = null;
            Interlocked.Exchange(ref _autoPushInProgress, 0);
        }
    }

    private async Task RunAutoSyncTickAsync()
    {
        if (Interlocked.CompareExchange(ref _autoSyncInProgress, 1, 0) != 0) return;
        _autoSyncCts = new CancellationTokenSource();
        AutoSyncStarted?.Invoke();
        try
        {
            await RunCycleAsync(_autoSyncCts.Token);
        }
        catch (OperationCanceledException)
        {
            _log($"{Ts()} Auto-pull cancelled.");
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
