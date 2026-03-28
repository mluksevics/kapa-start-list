using StartRef.Desktop;
using StartRef.Desktop.DbBridge;
using StartRef.Desktop.Services;

namespace StartRef.Desktop.Forms;

public partial class MainForm : Form
{
    private AppSettings _settings;
    private readonly ApiClient _api;
    private readonly SyncService _syncService;
    private readonly string _logFilePath;
    private bool _startupPeekCompleted;
    private CancellationTokenSource? _cancelSyncCts;
    private string? _runningCommandName;
    private System.Windows.Forms.Timer? _runningStatusTimer;
    private DateTimeOffset _runningStartedAtUtc;
    private bool _pushActionsEnabled;

    private sealed record EtapItem(int DayNo, string Name, string Date)
    {
        public override string ToString() => $"Day {DayNo}: {Name}  ({Date})";
    }

    public MainForm()
    {
        InitializeComponent();
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        _settings = AppSettings.Load();
        DbBridgeService.SetCodePage(_settings.DbCodePage);
        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_log.txt");

        _api = new ApiClient(() => _settings);
        var dbIsamRepository = new DbIsamRepository(AppendLog);
        _syncService = new SyncService(_api, dbIsamRepository, () => _settings, AppendLog);
        _syncService.AutoSyncStarted += SyncService_AutoSyncStarted;
        _syncService.AutoSyncFinished += SyncService_AutoSyncFinished;
        _pushActionsEnabled = false;

        LoadSettingsToUi();   // sets chkAutoSync, which fires Start() if enabled
        LoadStagesFromDb();
        Shown += MainForm_Shown;

        UpdateStatusLabel("Idle");
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        if (_startupPeekCompleted) return;
        _startupPeekCompleted = true;
        await RunPeekWebApiAsync();
    }

    private void LoadSettingsToUi()
    {
        txtApiUrl.Text = _settings.ApiBaseUrl;
        lblDbPath.Text = _settings.DbIsamPath.Length > 0 ? _settings.DbIsamPath : "(not set)";
        chkAutoSync.Checked = _settings.AutoSyncEnabled;
        btnFailureSound.Text = _settings.FailureSoundEnabled ? "🔊" : "🔇";
        nudInterval.Value = _settings.SyncIntervalSeconds;
        UpdateLastSyncLabel();
    }

    // ── Stage dropdown ────────────────────────────────────────────────────────

    private void LoadStagesFromDb()
    {
        cmbDay.Items.Clear();
        lblDayNote.Text = "";
        cmbDay.Enabled = false;

        if (string.IsNullOrWhiteSpace(_settings.DbIsamPath))
        {
            SetPushActionsEnabled(false);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        int todayIndex = -1;

        try
        {
            using var db = new DbBridgeService();
            if (!db.Open(_settings.DbIsamPath))
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} Failed to open DBISAM: {_settings.DbIsamPath}");
                AppendLog($"{DateTime.Now:HH:mm:ss} Check DLL logs: {DbBridgeService.GlobalDllLogPath}");
                AppendLog($"{DateTime.Now:HH:mm:ss} Check DB log: {DbBridgeService.DbErrorLogPath(_settings.DbIsamPath)}");
                SetPushActionsEnabled(false);
                return;
            }

            for (int dayNo = 1; dayNo <= 6; dayNo++)
            {
                var (result, info) = db.GetEtapInfo(dayNo);
                if (!result.Success) continue;
                cmbDay.Items.Add(new EtapItem(dayNo, info!.Name, info.Date));
                if (todayIndex < 0 && TryParseEtapDate(info.Date, out var d) && d == today)
                    todayIndex = cmbDay.Items.Count - 1;
            }
            SetPushActionsEnabled(true);
        }
        catch (Exception ex)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Failed to load stages from DBISAM: {ex.Message}");
            AppendLog($"{DateTime.Now:HH:mm:ss} Check DLL logs: {DbBridgeService.GlobalDllLogPath}");
            if (!string.IsNullOrWhiteSpace(_settings.DbIsamPath))
                AppendLog($"{DateTime.Now:HH:mm:ss} Check DB log: {DbBridgeService.DbErrorLogPath(_settings.DbIsamPath)}");
            SetPushActionsEnabled(false);
        }

        if (cmbDay.Items.Count == 0) return;

        cmbDay.Enabled = true;

        if (todayIndex >= 0)
        {
            cmbDay.SelectedIndex = todayIndex;
            lblDayNote.Text = "";
        }
        else
        {
            // Default to stage whose DayNo matches saved setting, or first stage
            int savedIndex = -1;
            for (int i = 0; i < cmbDay.Items.Count; i++)
            {
                if (((EtapItem)cmbDay.Items[i]!).DayNo == _settings.DayNo)
                { savedIndex = i; break; }
            }
            cmbDay.SelectedIndex = savedIndex >= 0 ? savedIndex : 0;
            lblDayNote.Text = $"Competition does not have a stage with today's date ({today:yyyy-MM-dd}).";
        }
    }

    private static bool TryParseEtapDate(string s, out DateOnly date)
    {
        s = s.Trim();
        string[] formats = { "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "MM/dd/yyyy" };
        foreach (var fmt in formats)
            if (DateOnly.TryParseExact(s, fmt, null, System.Globalization.DateTimeStyles.None, out date))
                return true;
        return DateOnly.TryParse(s, out date);
    }

    private void cmbDay_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (cmbDay.SelectedItem is EtapItem item)
        {
            _settings.DayNo = item.DayNo;
            _settings.Save();
        }
    }

    // ── Log / status ──────────────────────────────────────────────────────────

    private void UpdateLastSyncLabel()
    {
        lblLastSync.Text = _settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? "Never"
            : _settings.LastServerTimeUtc.ToLocalTime().ToString("HH:mm:ss");
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.ScrollToCaret();
        if (IsFailureMessage(message))
            PlayFailureSound();
        UpdateLastSyncLabel();
        try { File.AppendAllText(_logFilePath, message + Environment.NewLine); } catch { }
    }

    private bool IsFailureMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var text = message.ToLowerInvariant();
        if (text.Contains("cancelled")) return false;
        return text.Contains("failed") ||
               text.Contains("error") ||
               text.Contains("unreachable") ||
               text.Contains("not available");
    }

    private void PlayFailureSound()
    {
        if (!_settings.FailureSoundEnabled) return;
        _ = Task.Run(() =>
        {
            try
            {
                var tones = new[] { 1400, 1200, 1000, 800 };
                foreach (var tone in tones)
                {
                    Console.Beep(tone, 90);
                    Thread.Sleep(35);
                }
            }
            catch { }
        });
    }

    private void UpdateStatusLabel(string status)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatusLabel(status)); return; }
        lblStatus.Text = $"Status: {status}";
    }

    private void SetPushActionsEnabled(bool enabled)
    {
        var previous = _pushActionsEnabled;
        _pushActionsEnabled = enabled;

        if (enabled && !previous)
            AppendLog($"{DateTime.Now:HH:mm:ss} DBISAM opened successfully: {_settings.DbIsamPath}");

        if (!enabled)
            _syncService.Stop();

        if (_cancelSyncCts is null)
        {
            btnSyncNow.Enabled = enabled;
            btnForcePush.Enabled = enabled;
            btnPushClubs.Enabled = enabled;
        }
    }

    private bool EnsureDbAvailableForPush(string actionName)
    {
        if (string.IsNullOrWhiteSpace(_settings.DbIsamPath))
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Failed to run {actionName}: DBISAM path is not set.");
            SetPushActionsEnabled(false);
            return false;
        }

        try
        {
            using var db = new DbBridgeService();
            if (!db.Open(_settings.DbIsamPath))
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} Failed to run {actionName}: cannot open DBISAM.");
                AppendLog($"{DateTime.Now:HH:mm:ss} Check DLL logs: {DbBridgeService.GlobalDllLogPath}");
                AppendLog($"{DateTime.Now:HH:mm:ss} Check DB log: {DbBridgeService.DbErrorLogPath(_settings.DbIsamPath)}");
                SetPushActionsEnabled(false);
                return false;
            }
        }
        catch (Exception ex)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Failed to run {actionName}: DBISAM open error: {ex.Message}");
            SetPushActionsEnabled(false);
            return false;
        }

        SetPushActionsEnabled(true);
        return true;
    }

    private bool TryBeginCancelableCommand(string commandName)
    {
        if (_cancelSyncCts is not null)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Cannot start {commandName}: {_runningCommandName ?? "another command"} is already running.");
            return false;
        }

        _syncService.Stop();
        _runningCommandName = commandName;
        _cancelSyncCts = new CancellationTokenSource();
        btnCancelSync.Enabled = true;
        btnSyncNow.Enabled = false;
        btnForcePush.Enabled = false;
        btnPushClubs.Enabled = false;
        btnPeekWebApi.Enabled = false;
        btnPullPast.Enabled = false;
        StartRunningStatus(commandName);
        return true;
    }

    private void EndCancelableCommand()
    {
        _cancelSyncCts?.Dispose();
        _cancelSyncCts = null;
        _runningCommandName = null;
        btnCancelSync.Enabled = false;
        btnSyncNow.Enabled = _pushActionsEnabled;
        btnForcePush.Enabled = _pushActionsEnabled;
        btnPushClubs.Enabled = _pushActionsEnabled;
        btnPeekWebApi.Enabled = true;
        btnPullPast.Enabled = true;
        StopRunningStatus();
        if (_settings.AutoSyncEnabled && _pushActionsEnabled) _syncService.Start();
    }

    private void StartRunningStatus(string commandName)
    {
        _runningStartedAtUtc = DateTimeOffset.UtcNow;
        _runningStatusTimer?.Stop();
        _runningStatusTimer?.Dispose();
        _runningStatusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _runningStatusTimer.Tick += (_, _) =>
        {
            var elapsed = DateTimeOffset.UtcNow - _runningStartedAtUtc;
            UpdateStatusLabel($"Running: {commandName} ({(int)elapsed.TotalSeconds}s)");
        };
        _runningStatusTimer.Start();
        UpdateStatusLabel($"Running: {commandName} (0s)");
    }

    private void StopRunningStatus()
    {
        _runningStatusTimer?.Stop();
        _runningStatusTimer?.Dispose();
        _runningStatusTimer = null;
    }

    // ── Buttons / handlers ────────────────────────────────────────────────────

    private async void btnSyncNow_Click(object sender, EventArgs e)
    {
        if (!EnsureDbAvailableForPush("Sync Now")) return;
        if (!TryBeginCancelableCommand("Sync Now")) return;
        var cts = _cancelSyncCts;
        if (cts is null) return;
        UpdateStatusLabel("Syncing...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Sync Now initiated.");
            await Task.Run(() => _syncService.RunCycleAsync(forcePush: false, cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Sync Now cancelled by user.");
        }
        finally
        {
            EndCancelableCommand();
            UpdateStatusLabel("Idle");
        }
    }

    private async void btnForcePush_Click(object sender, EventArgs e)
    {
        if (!EnsureDbAvailableForPush("Force Push All")) return;
        if (MessageBox.Show(
                "Force Push All will overwrite all non-status fields on the server with current DBISAM values.\n\nContinue?",
                "Confirm Force Push",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        if (!TryBeginCancelableCommand("Force Push All")) return;
        var cts = _cancelSyncCts;
        if (cts is null) return;
        UpdateStatusLabel("Force pushing...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All initiated.");
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All step 1/2: running sync.");
            await Task.Run(() => _syncService.RunCycleAsync(forcePush: false, cts.Token), cts.Token);
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All step 2/2: running force push.");
            await Task.Run(() => _syncService.ForcePushAllAsync(cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All cancelled by user.");
        }
        finally
        {
            EndCancelableCommand();
            UpdateStatusLabel("Idle");
        }
    }

    private async void btnPushClubs_Click(object sender, EventArgs e)
    {
        if (!EnsureDbAvailableForPush("Push Clubs")) return;
        if (!TryBeginCancelableCommand("Push Clubs")) return;
        var cts = _cancelSyncCts;
        if (cts is null) return;
        UpdateStatusLabel("Pushing clubs...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Push Clubs initiated.");
            await Task.Run(() => _syncService.PushClubsAsync(cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Push Clubs cancelled by user.");
        }
        finally
        {
            EndCancelableCommand();
            UpdateStatusLabel("Idle");
        }
    }

    private void txtApiUrl_Leave(object sender, EventArgs e)
    {
        _settings.ApiBaseUrl = txtApiUrl.Text.Trim();
        _settings.Save();
    }

    private void btnEditApi_Click(object sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _settings = dlg.Result;
        DbBridgeService.SetCodePage(_settings.DbCodePage);
        _settings.Save();
        _syncService.UpdateInterval(_settings.SyncIntervalSeconds);
        LoadSettingsToUi();
    }

    private void btnBrowseDb_Click(object sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select DBISAM database folder",
            SelectedPath = _settings.DbIsamPath
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _settings.DbIsamPath = dlg.SelectedPath;
        _settings.Save();
        lblDbPath.Text = _settings.DbIsamPath;
        LoadStagesFromDb();
    }

    private void btnDbExplorer_Click(object sender, EventArgs e)
    {
        using var form = new DbBridgeExplorerForm();
        form.ShowDialog(this);
    }

    private async Task RunPeekWebApiAsync(bool userInitiated = false)
    {
        if (!TryBeginCancelableCommand("Peek in WebApi")) return;
        UpdateStatusLabel("Peeking WebApi...");
        try
        {
            if (userInitiated)
                AppendLog($"{DateTime.Now:HH:mm:ss} Peek in WebApi initiated.");

            var counts = await _api.GetLookupCountsAsync(_cancelSyncCts!.Token);
            if (counts is null)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} Peek in WebApi failed: no response.");
                return;
            }

            AppendLog($"{DateTime.Now:HH:mm:ss} WebApi SQL counts -> competitors: {counts.Competitors}, clubs: {counts.Clubs}, classes: {counts.Classes}");
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Peek in WebApi cancelled by user.");
        }
        finally
        {
            EndCancelableCommand();
            UpdateStatusLabel("Idle");
        }
    }

    private async void btnPeekWebApi_Click(object sender, EventArgs e)
    {
        await RunPeekWebApiAsync(userInitiated: true);
    }

    private async Task RunPullPastUpdatesAsync(int minutes)
    {
        if (!TryBeginCancelableCommand("Pull Past Updates")) return;
        var cts = _cancelSyncCts;
        if (cts is null) return;
        var changedSinceUtc = DateTimeOffset.UtcNow.AddMinutes(-minutes);
        UpdateStatusLabel($"Pulling past {minutes} min...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Pull Past Updates initiated for last {minutes} minute(s).");
            await Task.Run(() => _syncService.PullUpdatesSinceAsync(changedSinceUtc, cts.Token), cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Pull Past Updates cancelled by user.");
        }
        finally
        {
            EndCancelableCommand();
            UpdateStatusLabel("Idle");
        }
    }

    private async void btnPullPast_Click(object sender, EventArgs e)
    {
        using var dlg = new PullPastDialog();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        await RunPullPastUpdatesAsync(dlg.SelectedMinutes);
    }

    private void btnCancelSync_Click(object sender, EventArgs e)
    {
        if (_cancelSyncCts is null || _cancelSyncCts.IsCancellationRequested) return;
        if (string.Equals(_runningCommandName, "Auto-sync", StringComparison.Ordinal))
            _syncService.CancelAutoSync();
        else
            _cancelSyncCts.Cancel();
        btnCancelSync.Enabled = false;
        AppendLog($"{DateTime.Now:HH:mm:ss} Cancel requested for {_runningCommandName ?? "current command"}...");
    }

    private void SyncService_AutoSyncStarted()
    {
        if (InvokeRequired) { Invoke(SyncService_AutoSyncStarted); return; }
        if (!EnsureDbAvailableForPush("Auto-sync"))
        {
            _syncService.CancelAutoSync();
            return;
        }
        if (_cancelSyncCts is not null) return;
        _runningCommandName = "Auto-sync";
        _cancelSyncCts = new CancellationTokenSource();
        btnCancelSync.Enabled = true;
        btnSyncNow.Enabled = false;
        btnForcePush.Enabled = false;
        btnPushClubs.Enabled = false;
        btnPeekWebApi.Enabled = false;
        btnPullPast.Enabled = false;
        StartRunningStatus("Auto-sync");
        AppendLog($"{DateTime.Now:HH:mm:ss} Auto-sync started.");
    }

    private void SyncService_AutoSyncFinished()
    {
        if (InvokeRequired) { Invoke(SyncService_AutoSyncFinished); return; }
        if (!string.Equals(_runningCommandName, "Auto-sync", StringComparison.Ordinal)) return;
        _cancelSyncCts?.Dispose();
        _cancelSyncCts = null;
        _runningCommandName = null;
        btnCancelSync.Enabled = false;
        btnSyncNow.Enabled = _pushActionsEnabled;
        btnForcePush.Enabled = _pushActionsEnabled;
        btnPushClubs.Enabled = _pushActionsEnabled;
        btnPeekWebApi.Enabled = true;
        btnPullPast.Enabled = true;
        StopRunningStatus();
        UpdateStatusLabel("Idle");
    }

    private void chkAutoSync_CheckedChanged(object sender, EventArgs e)
    {
        _settings.AutoSyncEnabled = chkAutoSync.Checked;
        _settings.Save();
        if (_settings.AutoSyncEnabled)
        {
            if (!EnsureDbAvailableForPush("Auto-sync"))
            {
                _settings.AutoSyncEnabled = false;
                _settings.Save();
                chkAutoSync.Checked = false;
                return;
            }
            _syncService.Start();
        }
        else
        {
            _syncService.Stop();
        }
    }

    private void btnFailureSound_Click(object sender, EventArgs e)
    {
        _settings.FailureSoundEnabled = !_settings.FailureSoundEnabled;
        _settings.Save();
        btnFailureSound.Text = _settings.FailureSoundEnabled ? "🔊" : "🔇";
    }

    private void nudInterval_ValueChanged(object sender, EventArgs e)
    {
        _settings.SyncIntervalSeconds = (int)nudInterval.Value;
        _settings.Save();
        _syncService.UpdateInterval(_settings.SyncIntervalSeconds);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Flush any pending textbox edits before exit
        _settings.ApiBaseUrl = txtApiUrl.Text.Trim();
        _settings.Save();
        base.OnFormClosing(e);
    }
}
