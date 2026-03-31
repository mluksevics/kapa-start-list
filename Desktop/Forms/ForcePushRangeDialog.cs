namespace StartRef.Desktop.Forms;

public class ForcePushRangeDialog : Form
{
    private readonly NumericUpDown _nudFrom;
    private readonly NumericUpDown _nudTo;

    public int FromStartNumber => (int)_nudFrom.Value;
    public int ToStartNumber => (int)_nudTo.Value;

    public ForcePushRangeDialog()
    {
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = "Force push selected start numbers";
        Size = new System.Drawing.Size(420, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 15;
        var lblInfo = new Label
        {
            Text = "Upload DBISAM data to the API for an inclusive range of bib (start) numbers.",
            Location = new System.Drawing.Point(10, y),
            Size = new System.Drawing.Size(390, 36)
        };
        y += 44;

        var lblFrom = new Label { Text = "From:", Location = new System.Drawing.Point(10, y + 3), Size = new System.Drawing.Size(45, 20) };
        _nudFrom = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 4000,
            Value = 1,
            Width = 80,
            Location = new System.Drawing.Point(58, y)
        };

        var lblTo = new Label { Text = "To:", Location = new System.Drawing.Point(160, y + 3), Size = new System.Drawing.Size(30, 20) };
        _nudTo = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 4000,
            Value = 100,
            Width = 80,
            Location = new System.Drawing.Point(190, y)
        };
        y += 42;

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new System.Drawing.Point(220, y),
            Size = new System.Drawing.Size(85, 28)
        };
        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new System.Drawing.Point(310, y),
            Size = new System.Drawing.Size(85, 28)
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[] { lblInfo, lblFrom, _nudFrom, lblTo, _nudTo, btnOk, btnCancel });
    }
}
