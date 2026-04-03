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
    private Button btnSyncNow, btnForcePush, btnForcePushRange, btnPushAllChanges, btnPushClubs, btnPushClasses, btnPullPast, btnDeleteTodayData, btnResetDayData, btnDbExplorer, btnPeekWebApi, btnCancelSync, btnAdvancedToggle;
    private CheckBox chkAutoPush;
    private NumericUpDown nudAutoPushInterval;
    private Label lblAutoPushIntervalCaption, lblAutoPushIntervalUnit, lblLastAutoPushCaption, lblLastAutoPush;
    private Panel panelAdvancedContent;
    private FlowLayoutPanel flowAdvanced;
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

        // ── Auto-pull row ─────────────────────────────────────────────────────
        chkAutoSync = new CheckBox
        {
            Text = "Auto-pull enabled",
            Location = new System.Drawing.Point(10, y),
            AutoSize = true
        };
        chkAutoSync.CheckedChanged += chkAutoSync_CheckedChanged;

        lblIntervalCaption = MakeLabel("Interval:", 200, y, 55);
        nudInterval = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 3600,
            Value = 30,
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

        // ── Auto-push row ─────────────────────────────────────────────────────
        chkAutoPush = new CheckBox
        {
            Text = "Auto-push enabled",
            Location = new System.Drawing.Point(10, y),
            AutoSize = true,
            Checked = true
        };
        chkAutoPush.CheckedChanged += chkAutoPush_CheckedChanged;

        lblAutoPushIntervalCaption = MakeLabel("Interval:", 200, y, 55);
        nudAutoPushInterval = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 3600,
            Value = 600,
            Width = 60,
            Location = new System.Drawing.Point(258, y)
        };
        nudAutoPushInterval.ValueChanged += nudAutoPushInterval_ValueChanged;
        lblAutoPushIntervalUnit = MakeLabel("s", 322, y, 20);

        lblLastAutoPushCaption = MakeLabel("Last push:", 360, y, 65);
        lblLastAutoPush = MakeLabel("Never", 428, y, 100);
        y += 32;

        // Separator
        var sep = new Label
        {
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(760, 2),
            BorderStyle = BorderStyle.Fixed3D
        };
        y += 10;

        // ── Action buttons (one row) ──────────────────────────────────────────
        const int btnRowH = 28;
        int yAct = y;
        int xAct = 10;

        btnSyncNow = new Button
        {
            Text = "Pull Now",
            Location = new System.Drawing.Point(xAct, yAct),
            Size = new System.Drawing.Size(72, btnRowH)
        };
        btnSyncNow.Click += btnSyncNow_Click;
        xAct += 72 + 4;

        btnPushAllChanges = new Button
        {
            Text = "Push Updates",
            Location = new System.Drawing.Point(xAct, yAct),
            Size = new System.Drawing.Size(94, btnRowH)
        };
        btnPushAllChanges.Click += btnPushAllChanges_Click;
        tipUi.SetToolTip(btnPushAllChanges, "Sync then push all eligible DBISAM rows; API applies only real diffs");
        xAct += 94 + 4;

        btnPullPast = new Button
        {
            Text = "Pull Past",
            Location = new System.Drawing.Point(xAct, yAct),
            Size = new System.Drawing.Size(80, btnRowH)
        };
        btnPullPast.Click += btnPullPast_Click;
        xAct += 80 + 4;

        btnResetDayData = new Button
        {
            Text = "Reset Day",
            Location = new System.Drawing.Point(xAct, yAct),
            Size = new System.Drawing.Size(80, btnRowH)
        };
        btnResetDayData.Click += btnResetDayData_Click;
        tipUi.SetToolTip(btnResetDayData, "Delete all API data for this date, then re-upload clubs, classes, and all runners from DBISAM");
        xAct += 80 + 4;

        btnPeekWebApi = new Button
        {
            Text = "Peek API data",
            Location = new System.Drawing.Point(xAct, yAct),
            Size = new System.Drawing.Size(118, btnRowH)
        };
        btnPeekWebApi.Click += btnPeekWebApi_Click;

        int yAdv = yAct + btnRowH + 4;
        btnAdvancedToggle = new Button
        {
            Text = "▶ Advanced",
            Location = new System.Drawing.Point(10, yAdv),
            Size = new System.Drawing.Size(760, 26),
            TextAlign = ContentAlignment.MiddleLeft
        };
        btnAdvancedToggle.Click += btnAdvancedToggle_Click;

        int yPanel = yAdv + 26 + 2;
        panelAdvancedContent = new Panel
        {
            Location = new System.Drawing.Point(10, yPanel),
            Size = new System.Drawing.Size(760, 74),
            Visible = false
        };
        flowAdvanced = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Padding = new Padding(0)
        };
        panelAdvancedContent.Controls.Add(flowAdvanced);

        btnForcePush = new Button
        {
            Text = "Force Push All",
            Size = new System.Drawing.Size(105, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnForcePush.Click += btnForcePush_Click;

        btnForcePushRange = new Button
        {
            Text = "Push selected",
            Size = new System.Drawing.Size(105, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnForcePushRange.Click += btnForcePushRange_Click;
        tipUi.SetToolTip(btnForcePushRange, "Force push an inclusive range of start numbers from DBISAM");

        btnDeleteTodayData = new Button
        {
            Text = "Delete Today",
            Size = new System.Drawing.Size(88, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnDeleteTodayData.Click += btnDeleteTodayData_Click;

        btnPushClubs = new Button
        {
            Text = "Push Clubs",
            Size = new System.Drawing.Size(86, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnPushClubs.Click += btnPushClubs_Click;

        btnPushClasses = new Button
        {
            Text = "Push Classes",
            Size = new System.Drawing.Size(94, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnPushClasses.Click += btnPushClasses_Click;

        btnDbExplorer = new Button
        {
            Text = "E",
            Size = new System.Drawing.Size(28, 30),
            Margin = new Padding(0, 0, 8, 8)
        };
        btnDbExplorer.Click += btnDbExplorer_Click;

        flowAdvanced.Controls.Add(btnForcePush);
        flowAdvanced.Controls.Add(btnForcePushRange);
        flowAdvanced.Controls.Add(btnPushClubs);
        flowAdvanced.Controls.Add(btnPushClasses);
        flowAdvanced.Controls.Add(btnDeleteTodayData);
        flowAdvanced.Controls.Add(btnDbExplorer);

        int yStatusRow = yAdv + 26 + 8;
        btnCancelSync = new Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(10, yStatusRow),
            Size = new System.Drawing.Size(88, btnRowH),
            Enabled = false
        };
        btnCancelSync.Click += btnCancelSync_Click;

        lblStatus = MakeLabel("Status: Idle", 10 + 88 + 6, yStatusRow, 560);

        // ── Log ───────────────────────────────────────────────────────────────
        lblLogCaption = MakeLabel("Log:", 10, yStatusRow + 28, 50);
        txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new System.Drawing.Point(10, yStatusRow + 48),
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
            chkAutoPush, lblAutoPushIntervalCaption, nudAutoPushInterval, lblAutoPushIntervalUnit,
            lblLastAutoPushCaption, lblLastAutoPush,
            sep,
            btnSyncNow, btnPushAllChanges, btnPullPast, btnResetDayData, btnPeekWebApi,
            btnAdvancedToggle, panelAdvancedContent,
            btnCancelSync, lblStatus,
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
