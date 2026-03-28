namespace StartRef.Desktop;

public class PullPastDialog : Form
{
    private readonly NumericUpDown _nudMinutes;

    public int SelectedMinutes => (int)_nudMinutes.Value;

    public PullPastDialog()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = "Pull Past";
        Size = new System.Drawing.Size(420, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 15;

        var lbl = new Label
        {
            Text = "Choose time window:",
            Location = new(10, y),
            Size = new(150, 20)
        };
        y += 28;

        var btn10 = new Button { Text = "10", Location = new(10, y), Size = new(55, 28) };
        var btn15 = new Button { Text = "15", Location = new(72, y), Size = new(55, 28) };
        var btn30 = new Button { Text = "30", Location = new(134, y), Size = new(55, 28) };
        var btn60 = new Button { Text = "60", Location = new(196, y), Size = new(55, 28) };
        var btn240 = new Button { Text = "240", Location = new(258, y), Size = new(55, 28) };

        _nudMinutes = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 1440,
            Value = 15,
            Width = 75,
            Location = new(320, y + 4)
        };
        var lblMin = new Label { Text = "min", Location = new(372, y + 8), Size = new(30, 20) };
        y += 42;

        var btnPull = new Button
        {
            Text = "Pull Past Updates",
            DialogResult = DialogResult.OK,
            Location = new(10, y),
            Size = new(150, 30)
        };

        btn10.Click += (_, _) => _nudMinutes.Value = 10;
        btn15.Click += (_, _) => _nudMinutes.Value = 15;
        btn30.Click += (_, _) => _nudMinutes.Value = 30;
        btn60.Click += (_, _) => _nudMinutes.Value = 60;
        btn240.Click += (_, _) => _nudMinutes.Value = 240;

        AcceptButton = btnPull;

        Controls.AddRange(new Control[] { lbl, btn10, btn15, btn30, btn60, btn240, _nudMinutes, lblMin, btnPull });
    }
}
