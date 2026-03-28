namespace StartRef.Desktop;

/// <summary>Dialog for editing API URL, API key, device name, and sync interval.</summary>
public class SettingsDialog : Form
{
    private readonly TextBox _txtApiUrl;
    private readonly TextBox _txtApiKey;
    private readonly TextBox _txtDeviceName;
    private readonly NumericUpDown _nudInterval;

    public AppSettings Result { get; private set; }

    public SettingsDialog(AppSettings current)
    {
        Result = current;
        Text = "Settings";
        Size = new System.Drawing.Size(460, 280);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 15, lw = 110, tw = 290;

        var lblApiUrl = new Label { Text = "API Base URL:", Location = new(10, y), Size = new(lw, 20) };
        _txtApiUrl = new TextBox { Text = current.ApiBaseUrl, Location = new(lw + 10, y), Width = tw };
        y += 30;

        var lblApiKey = new Label { Text = "API Key:", Location = new(10, y), Size = new(lw, 20) };
        _txtApiKey = new TextBox { Text = current.ApiKey, Location = new(lw + 10, y), Width = tw, UseSystemPasswordChar = false };
        y += 30;

        var lblDevice = new Label { Text = "Device Name:", Location = new(10, y), Size = new(lw, 20) };
        _txtDeviceName = new TextBox { Text = current.DeviceName, Location = new(lw + 10, y), Width = tw };
        y += 30;

        var lblInterval = new Label { Text = "Sync interval (s):", Location = new(10, y), Size = new(lw, 20) };
        _nudInterval = new NumericUpDown
        {
            Minimum = 10, Maximum = 3600,
            Value = current.SyncIntervalSeconds,
            Location = new(lw + 10, y), Width = 80
        };
        y += 40;

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new(lw + 10, y), Width = 80 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new(lw + 100, y), Width = 80 };
        btnOk.Click += (_, _) => SaveResult();
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[]
        {
            lblApiUrl, _txtApiUrl,
            lblApiKey, _txtApiKey,
            lblDevice, _txtDeviceName,
            lblInterval, _nudInterval,
            btnOk, btnCancel
        });
    }

    private void SaveResult()
    {
        Result = Result with
        {
            ApiBaseUrl = _txtApiUrl.Text.Trim(),
            ApiKey = _txtApiKey.Text.Trim(),
            DeviceName = _txtDeviceName.Text.Trim().Length > 0 ? _txtDeviceName.Text.Trim() : "desktop",
            SyncIntervalSeconds = (int)_nudInterval.Value
        };
    }
}
