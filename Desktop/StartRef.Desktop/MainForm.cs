namespace StartRef.Desktop;

public partial class MainForm : Form
{
    private AppSettings _settings;
    private readonly ApiClient _api;
    private readonly DbIsamReader _dbReader;
    private readonly DbIsamWriter _dbWriter;
    private readonly SyncService _syncService;
    private readonly string _logFilePath;

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_log.txt");

        _api = new ApiClient(() => _settings);
        _dbReader = new DbIsamReader(() => _settings);
        _dbWriter = new DbIsamWriter(() => _settings);
        _syncService = new SyncService(_api, _dbReader, _dbWriter, () => _settings, AppendLog);

        LoadSettingsToUi();

        if (_settings.AutoSyncEnabled)
            _syncService.Start();

        UpdateStatusLabel("Idle");
    }

    private void LoadSettingsToUi()
    {
        lblApiUrl.Text = _settings.ApiBaseUrl.Length > 0
            ? _settings.ApiBaseUrl
            : "(not set)";
        lblApiKey.Text = _settings.ApiKey.Length > 0
            ? new string('●', Math.Min(_settings.ApiKey.Length, 12))
            : "(not set)";
        lblDbPath.Text = _settings.DbIsamPath.Length > 0
            ? _settings.DbIsamPath
            : "(not set)";
        chkAutoSync.Checked = _settings.AutoSyncEnabled;
        nudInterval.Value = _settings.SyncIntervalSeconds;
        UpdateLastSyncLabel();
    }

    private void UpdateLastSyncLabel()
    {
        lblLastSync.Text = _settings.LastServerTimeUtc == DateTimeOffset.MinValue
            ? "Never"
            : _settings.LastServerTimeUtc.ToLocalTime().ToString("HH:mm:ss");
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }

        txtLog.AppendText(message + Environment.NewLine);
        txtLog.ScrollToCaret();
        UpdateLastSyncLabel();

        try { File.AppendAllText(_logFilePath, message + Environment.NewLine); }
        catch { /* non-critical */ }
    }

    private void UpdateStatusLabel(string status)
    {
        if (InvokeRequired) { Invoke(() => UpdateStatusLabel(status)); return; }
        lblStatus.Text = $"Status: {status}";
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    private async void btnSyncNow_Click(object sender, EventArgs e)
    {
        btnSyncNow.Enabled = false;
        UpdateStatusLabel("Syncing...");
        try
        {
            await _syncService.RunCycleAsync(forcePush: false);
        }
        finally
        {
            btnSyncNow.Enabled = true;
            UpdateStatusLabel("Idle");
        }
    }

    private async void btnForcePush_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show(
                "Force Push All will overwrite all non-status fields on the server with current DBISAM values.\n\nContinue?",
                "Confirm Force Push",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        btnForcePush.Enabled = false;
        UpdateStatusLabel("Force pushing...");
        try
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} Force Push All initiated.");
            await _syncService.RunCycleAsync(forcePush: true);
        }
        finally
        {
            btnForcePush.Enabled = true;
            UpdateStatusLabel("Idle");
        }
    }

    private void btnEditApi_Click(object sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_settings);
        if (dlg.ShowDialog() != DialogResult.OK) return;
        _settings = dlg.Result;
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
    }

    private void chkAutoSync_CheckedChanged(object sender, EventArgs e)
    {
        _settings.AutoSyncEnabled = chkAutoSync.Checked;
        _settings.Save();
        if (_settings.AutoSyncEnabled)
            _syncService.Start();
        else
            _syncService.Stop();
    }

    private void nudInterval_ValueChanged(object sender, EventArgs e)
    {
        _settings.SyncIntervalSeconds = (int)nudInterval.Value;
        _settings.Save();
        _syncService.UpdateInterval(_settings.SyncIntervalSeconds);
    }
}
