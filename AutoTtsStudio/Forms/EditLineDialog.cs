namespace AutoTtsStudio.Forms;

/// <summary>
/// Minimal inline editor dialog for a single subtitle line.
/// </summary>
[System.ComponentModel.DesignerCategory("")]
internal sealed class EditLineDialog : Form
{
    private readonly TextBox _txtContent;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    public string EditedText => _txtContent.Text;

    public EditLineDialog(string initialText)
    {
        Text = "Sửa nội dung";
        Size = new Size(560, 180);
        MinimumSize = new Size(400, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _txtContent = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Font = new Font("Segoe UI", 9.5f),
            Text = initialText
        };
        tlp.Controls.Add(_txtContent, 0, 0);

        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0)
        };

        _btnCancel = new Button
        {
            Text = "Hủy",
            Width = 84,
            Height = 30,
            DialogResult = DialogResult.Cancel,
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true,
            Margin = new Padding(6, 4, 0, 0)
        };

        _btnOk = new Button
        {
            Text = "Lưu",
            Width = 84,
            Height = 30,
            DialogResult = DialogResult.None,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        _btnOk.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };

        pnlButtons.Controls.Add(_btnCancel);
        pnlButtons.Controls.Add(_btnOk);
        tlp.Controls.Add(pnlButtons, 0, 1);

        Controls.Add(tlp);
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }
}
