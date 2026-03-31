namespace StartRef.Desktop.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    // Controls
    private Label lblApiUrlCaption;
    private TextBox txtApiUrl;
    private Button btnEditApi, btnBrowseDb;
    private Label lblDbPathCaption, lblDbPath;
    private Label lblStageCaption;
    private ComboBox cmbDay;
    private Label lblDbDateCaption;
    private DateTimePicker dtpDbDate;
    private Label lblDayNote;
    private Label lblDbDateWarning;
    private CheckBox chkAutoSync;
    private Button btnFailureSound;
    private Label lblIntervalCaption;
    private NumericUpDown nudInterval;
    private Label lblIntervalUnit;
    private Label lblLastSyncCaption, lblLastSync;
    private CheckBox chkTestMode;
    private Button btnSyncNow, btnForcePush, btnForcePushRange, btnPushClubs, btnPullPast, btnDeleteTodayData, btnDbExplorer, btnPeekWebApi, btnCancelSync;
    private Label lblStatus;
    private TextBox txtLog;
    private Label lblLogCaption;
    private ToolTip tipUi;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        tipUi = new ToolTip(components);
        Text = "StartRef Desktop";
        Size = new System.Drawing.Size(800, 630);
        MinimumSize = new System.Drawing.Size(700, 500);

        var font = new System.Drawing.Font("Segoe UI", 9f);
        Font = font;

        int y = 10, margin = 8, lw = 120, bw = 60;

        // ── API URL ───────────────────────────────────────────────────────────
        lblApiUrlCaption = MakeLabel("API URL:", 10, y, lw);
        txtApiUrl = new TextBox
        {
            Location = new System.Drawing.Point(lw + margin + 10, y - 2),
            Width = 440
        };
        txtApiUrl.Leave += txtApiUrl_Leave;
        btnEditApi = MakeButton("Settings", lw + margin + 460, y, 75);
        btnEditApi.Click += btnEditApi_Click;
        btnFailureSound = new Button
        {
            Text = "🔊",
            Location = new System.Drawing.Point(lw + margin + 540, y),
            Size = new System.Drawing.Size(34, 23)
        };
        btnFailureSound.Click += btnFailureSound_Click;
        tipUi.SetToolTip(btnFailureSound, "Toggle failure beep");
        y += 28;

        // ── DB Path ───────────────────────────────────────────────────────────
        lblDbPathCaption = MakeLabel("DB Path:", 10, y, lw);
        lblDbPath = MakeLabel("(not set)", lw + margin + 10, y, 350);
        lblDbPath.AutoEllipsis = true;
        btnBrowseDb = MakeButton("Browse", lw + margin + 360, y, bw);
        btnBrowseDb.Click += btnBrowseDb_Click;
        y += 28;

        // ── Stage selector ────────────────────────────────────────────────────
        lblStageCaption = MakeLabel("Stage:", 10, y, lw);
        cmbDay = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new System.Drawing.Point(lw + margin + 10, y - 2),
            Width = 330,
            Enabled = false
        };
        cmbDay.SelectedIndexChanged += cmbDay_SelectedIndexChanged;

        lblDbDateCaption = MakeLabel("API date:", lw + margin + 350, y, 58);
        dtpDbDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Location = new System.Drawing.Point(lw + margin + 410, y - 2),
            Width = 120
        };
        dtpDbDate.ValueChanged += dtpDbDate_ValueChanged;
        y += 28;

        // Stage warning (shown in red when today has no matching stage)
        lblDayNote = new Label
        {
            Text = "",
            Location = new System.Drawing.Point(lw + margin + 10, y),
            Size = new System.Drawing.Size(530, 18),
            ForeColor = System.Drawing.Color.Red,
            AutoSize = false
        };
        lblDbDateWarning = new Label
        {
            Text = "",
            Location = new System.Drawing.Point(lw + margin + 10, y + 18),
            Size = new System.Drawing.Size(620, 18),
            ForeColor = System.Drawing.Color.Red,
            AutoSize = false
        };
        y += 40;

        // ── Auto-sync row ─────────────────────────────────────────────────────
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

        chkTestMode = new CheckBox
        {
            Text = "Test mode",
            Location = new System.Drawing.Point(540, y),
            AutoSize = true
        };
        chkTestMode.CheckedChanged += chkTestMode_CheckedChanged;
        y += 32;

        // Separator
        var sep = new Label
        {
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(760, 2),
            BorderStyle = BorderStyle.Fixed3D
        };
        y += 10;

        // ── Action buttons ────────────────────────────────────────────────────
        btnSyncNow = new Button
        {
            Text = "Sync Now",
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(115, 30)
        };
        btnSyncNow.Click += btnSyncNow_Click;

        btnForcePush = new Button
        {
            Text = "Force Push All",
            Location = new System.Drawing.Point(130, y),
            Size = new System.Drawing.Size(105, 30)
        };
        btnForcePush.Click += btnForcePush_Click;

        btnForcePushRange = new Button
        {
            Text = "Push selected",
            Location = new System.Drawing.Point(240, y),
            Size = new System.Drawing.Size(105, 30)
        };
        btnForcePushRange.Click += btnForcePushRange_Click;
        tipUi.SetToolTip(btnForcePushRange, "Force push an inclusive range of start numbers from DBISAM");

        btnPushClubs = new Button
        {
            Text = "Push Clubs",
            Location = new System.Drawing.Point(350, y),
            Size = new System.Drawing.Size(95, 30)
        };
        btnPushClubs.Click += btnPushClubs_Click;

        btnPullPast = new Button
        {
            Text = "Pull Past",
            Location = new System.Drawing.Point(450, y),
            Size = new System.Drawing.Size(95, 30)
        };
        btnPullPast.Click += btnPullPast_Click;

        btnDeleteTodayData = new Button
        {
            Text = "Delete Today",
            Location = new System.Drawing.Point(550, y),
            Size = new System.Drawing.Size(88, 30)
        };
        btnDeleteTodayData.Click += btnDeleteTodayData_Click;

        btnDbExplorer = new Button
        {
            Text = "E",
            Location = new System.Drawing.Point(645, y),
            Size = new System.Drawing.Size(28, 30)
        };
        btnDbExplorer.Click += btnDbExplorer_Click;

        btnPeekWebApi = new Button
        {
            Text = "Peek in WebApi",
            Location = new System.Drawing.Point(678, y),
            Size = new System.Drawing.Size(112, 30)
        };
        btnPeekWebApi.Click += btnPeekWebApi_Click;

        btnCancelSync = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(10, y + 34),
            Size = new System.Drawing.Size(110, 30),
            Enabled = false
        };
        btnCancelSync.Click += btnCancelSync_Click;

        y += 38;
        lblStatus = MakeLabel("Status: Idle", 130, y, 260);
        y += 28;

        // ── Log ───────────────────────────────────────────────────────────────
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
            lblApiUrlCaption, txtApiUrl, btnEditApi, btnFailureSound,
            lblDbPathCaption, lblDbPath, btnBrowseDb,
            lblStageCaption, cmbDay, lblDbDateCaption, dtpDbDate, lblDayNote, lblDbDateWarning,
            chkAutoSync, lblIntervalCaption, nudInterval, lblIntervalUnit,
            lblLastSyncCaption, lblLastSync, chkTestMode,
            sep,
            btnSyncNow, btnForcePush, btnForcePushRange, btnPushClubs, btnPullPast, btnDeleteTodayData, btnDbExplorer, btnPeekWebApi, btnCancelSync, lblStatus,
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
