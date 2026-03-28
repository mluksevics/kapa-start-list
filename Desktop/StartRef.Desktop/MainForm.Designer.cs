namespace StartRef.Desktop;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Controls
    private Label lblApiUrlCaption, lblApiUrl;
    private Label lblApiKeyCaption, lblApiKey;
    private Label lblDbPathCaption, lblDbPath;
    private Button btnEditApi, btnBrowseDb;
    private CheckBox chkAutoSync;
    private Label lblIntervalCaption;
    private NumericUpDown nudInterval;
    private Label lblIntervalUnit;
    private Label lblLastSyncCaption, lblLastSync;
    private Button btnSyncNow, btnForcePush;
    private Label lblStatus;
    private TextBox txtLog;
    private Label lblLogCaption;
    private TableLayoutPanel tlpSettings;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        Text = "StartRef Desktop";
        Size = new System.Drawing.Size(800, 620);
        MinimumSize = new System.Drawing.Size(700, 500);

        var font = new System.Drawing.Font("Segoe UI", 9f);
        Font = font;

        int y = 10, margin = 8, lw = 120, vw = 300, bw = 60;

        // ── Settings section ─────────────────────────────────────────────────
        lblApiUrlCaption = MakeLabel("API URL:", 10, y, lw);
        lblApiUrl = MakeLabel("(not set)", lw + margin + 10, y, vw);
        btnEditApi = MakeButton("Edit", lw + margin + vw + 10, y, bw);
        btnEditApi.Click += btnEditApi_Click;
        y += 28;

        lblApiKeyCaption = MakeLabel("API Key:", 10, y, lw);
        lblApiKey = MakeLabel("(not set)", lw + margin + 10, y, vw);
        y += 28;

        lblDbPathCaption = MakeLabel("DB Path:", 10, y, lw);
        lblDbPath = MakeLabel("(not set)", lw + margin + 10, y, vw);
        lblDbPath.AutoEllipsis = true;
        btnBrowseDb = MakeButton("Browse", lw + margin + vw + 10, y, bw);
        btnBrowseDb.Click += btnBrowseDb_Click;
        y += 28;

        // Auto-sync row
        chkAutoSync = new CheckBox
        {
            Text = "Auto-sync enabled",
            Location = new System.Drawing.Point(10, y),
            AutoSize = true
        };
        chkAutoSync.CheckedChanged += chkAutoSync_CheckedChanged;

        lblIntervalCaption = MakeLabel("Interval:", 200, y, 55);
        nudInterval = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 3600,
            Value = 60,
            Width = 60,
            Location = new System.Drawing.Point(258, y)
        };
        nudInterval.ValueChanged += nudInterval_ValueChanged;
        lblIntervalUnit = MakeLabel("s", 322, y, 20);

        lblLastSyncCaption = MakeLabel("Last sync:", 360, y, 65);
        lblLastSync = MakeLabel("Never", 428, y, 100);
        y += 32;

        // Separator
        var sep = new Label
        {
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(760, 2),
            BorderStyle = BorderStyle.Fixed3D
        };
        y += 10;

        // ── Buttons ──────────────────────────────────────────────────────────
        btnSyncNow = new Button
        {
            Text = "Sync Now",
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(120, 30)
        };
        btnSyncNow.Click += btnSyncNow_Click;

        btnForcePush = new Button
        {
            Text = "Force Push All",
            Location = new System.Drawing.Point(140, y),
            Size = new System.Drawing.Size(120, 30)
        };
        btnForcePush.Click += btnForcePush_Click;

        lblStatus = MakeLabel("Status: Idle", 280, y + 8, 300);
        y += 40;

        // ── Log ──────────────────────────────────────────────────────────────
        lblLogCaption = MakeLabel("Log:", 10, y, 50);
        y += 20;
        txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(760, 300),
            Font = new System.Drawing.Font("Consolas", 8.5f),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange(new Control[]
        {
            lblApiUrlCaption, lblApiUrl, btnEditApi,
            lblApiKeyCaption, lblApiKey,
            lblDbPathCaption, lblDbPath, btnBrowseDb,
            chkAutoSync, lblIntervalCaption, nudInterval, lblIntervalUnit,
            lblLastSyncCaption, lblLastSync,
            sep,
            btnSyncNow, btnForcePush, lblStatus,
            lblLogCaption, txtLog
        });
    }

    private static Label MakeLabel(string text, int x, int y, int w) => new()
    {
        Text = text,
        Location = new System.Drawing.Point(x, y),
        Size = new System.Drawing.Size(w, 20),
        AutoSize = false
    };

    private static Button MakeButton(string text, int x, int y, int w) => new()
    {
        Text = text,
        Location = new System.Drawing.Point(x, y),
        Size = new System.Drawing.Size(w, 23)
    };
}
