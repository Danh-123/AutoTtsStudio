using System.ComponentModel;

namespace AutoTtsStudio.Forms;

/// <summary>
/// Fast text input dialog for pasting English scripts directly without creating a file.
/// </summary>
[DesignerCategory("")]
public sealed class PasteTextDialog : Form
{
    private readonly TextBox _txtInput;
    private readonly CheckBox _chkAutoSplit;
    private readonly NumericUpDown _nudMaxChars;

    public string PastedText => _txtInput.Text;
    public bool AutoSplit => _chkAutoSplit.Checked;
    public int MaxChars => (int)_nudMaxChars.Value;

    public PasteTextDialog(bool defaultAutoSplit = true, int defaultMaxChars = 350)
    {
        Text = "Dán kịch bản tiếng Anh (Nhập nhanh)";
        Size = new Size(680, 460);
        MinimumSize = new Size(520, 360);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        ShowIcon = false;
        MaximizeBox = true;
        MinimizeBox = false;

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Instruction label
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Text editor
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Options and action buttons

        // Row 0: Instruction
        var lblInfo = new Label
        {
            Text = "Dán kịch bản hoặc gõ nội dung văn bản tiếng Anh vào ô dưới:",
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoEllipsis = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        rootLayout.Controls.Add(lblInfo, 0, 0);

        // Row 1: Multiline TextBox
        _txtInput = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5f),
            MaxLength = 500000
        };
        rootLayout.Controls.Add(_txtInput, 0, 1);

        // Row 2: Bottom toolbar
        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 0)
        };
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Auto split checkbox
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Max chars label
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Max chars nud
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // OK button
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Cancel button
        bottomLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chkAutoSplit = new CheckBox
        {
            Text = "Tự động tách câu (Auto Split)",
            Checked = defaultAutoSplit,
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 12, 0)
        };
        bottomLayout.Controls.Add(_chkAutoSplit, 0, 0);

        var lblMaxChars = new Label
        {
            Text = "Max ký tự:",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 4, 0)
        };
        bottomLayout.Controls.Add(lblMaxChars, 1, 0);

        _nudMaxChars = new NumericUpDown
        {
            Minimum = 50,
            Maximum = 2000,
            Increment = 10,
            Value = Math.Clamp(defaultMaxChars, 50, 2000),
            Dock = DockStyle.Fill,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        bottomLayout.Controls.Add(_nudMaxChars, 2, 0);

        var btnOk = new Button
        {
            Text = "✓ Nhập kịch bản",
            AutoSize = true,
            Height = 30,
            Padding = new Padding(10, 2, 10, 2),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        btnOk.Click += BtnOk_Click;
        bottomLayout.Controls.Add(btnOk, 4, 0);

        var btnCancel = new Button
        {
            Text = "✕ Hủy",
            AutoSize = true,
            Height = 30,
            Padding = new Padding(10, 2, 10, 2),
            DialogResult = DialogResult.Cancel,
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true,
            Margin = new Padding(6, 0, 0, 0)
        };
        bottomLayout.Controls.Add(btnCancel, 5, 0);

        rootLayout.Controls.Add(bottomLayout, 0, 2);

        Controls.Add(rootLayout);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtInput.Text))
        {
            MessageBox.Show("Vui lòng nhập hoặc dán nội dung văn bản kịch bản.",
                "Chưa có nội dung", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtInput.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
