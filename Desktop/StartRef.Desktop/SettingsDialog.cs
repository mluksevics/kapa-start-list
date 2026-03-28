namespace StartRef.Desktop;

/// <summary>Dialog for API Key and Device Name (URL is edited inline on the main form).</summary>
public class SettingsDialog : Form
{
    private readonly TextBox _txtApiKey;
    private readonly TextBox _txtDeviceName;

    public AppSettings Result { get; private set; }

    public SettingsDialog(AppSettings current)
    {
        Result = current;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = "Settings";
        Size = new System.Drawing.Size(420, 175);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 15, lw = 110, tw = 260;

        var lblApiKey = new Label { Text = "API Key:", Location = new(10, y), Size = new(lw, 20) };
        _txtApiKey = new TextBox
        {
            Text = current.ApiKey,
            Location = new(lw + 10, y),
            Width = tw,
            UseSystemPasswordChar = true
        };
        y += 30;

        var lblDevice = new Label { Text = "Device Name:", Location = new(10, y), Size = new(lw, 20) };
        _txtDeviceName = new TextBox { Text = current.DeviceName, Location = new(lw + 10, y), Width = tw };
        y += 40;

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new(lw + 10, y), Width = 80 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new(lw + 100, y), Width = 80 };
        btnOk.Click += (_, _) => SaveResult();
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[] { lblApiKey, _txtApiKey, lblDevice, _txtDeviceName, btnOk, btnCancel });
    }

    private void SaveResult()
    {
        Result.ApiKey = _txtApiKey.Text.Trim();
        Result.DeviceName = _txtDeviceName.Text.Trim().Length > 0 ? _txtDeviceName.Text.Trim() : "desktop";
    }
}
