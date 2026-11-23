using AutoTtsStudio.Models;

namespace AutoTtsStudio.Forms;

/// <summary>
/// Dialog nhập cấu hình: Local API URL, Local API Model, API Keys, đường dẫn FFmpeg, khoảng nghỉ giữa câu.
/// </summary>
[System.ComponentModel.DesignerCategory("")]
public sealed class SettingsDialog : Form
{
    private readonly AppConfig _config;

    // Controls
    private TextBox _txtLocalApiUrl = null!;
    private TextBox _txtLocalApiModel = null!;
    private TextBox _txtLocalApiKey = null!;
    private TextBox _txtElevenLabsKey = null!;
    private TextBox _txtFfmpegPath = null!;
    private NumericUpDown _nudSilenceGap = null!;
    private Button _btnBrowseFfmpeg = null!;
    private Button _btnOk = null!;
    private Button _btnCancel = null!;

    public SettingsDialog(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        LoadValues();
    }

    private void InitializeComponent()
    {
        Text = "Cài đặt API && FFmpeg";
        Size = new Size(540, 400);
        MinimumSize = new Size(480, 360);
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
            ColumnCount = 3,
            RowCount = 8,
            AutoSize = true
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        for (int i = 0; i < 7; i++)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Local API URL
        tlp.Controls.Add(new Label { Text = "Local API URL:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
        _txtLocalApiUrl = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtLocalApiUrl, 1, 0);
        tlp.SetColumnSpan(_txtLocalApiUrl, 2);

        // Local API Model
        tlp.Controls.Add(new Label { Text = "Local API Model:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
        _txtLocalApiModel = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtLocalApiModel, 1, 1);
        tlp.SetColumnSpan(_txtLocalApiModel, 2);

        // Local API Key
        tlp.Controls.Add(new Label { Text = "Local API Key:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
        _txtLocalApiKey = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        tlp.Controls.Add(_txtLocalApiKey, 1, 2);
        tlp.SetColumnSpan(_txtLocalApiKey, 2);

        // ElevenLabs Key
        tlp.Controls.Add(new Label { Text = "ElevenLabs API Key:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 3);
        _txtElevenLabsKey = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        tlp.Controls.Add(_txtElevenLabsKey, 1, 3);
        tlp.SetColumnSpan(_txtElevenLabsKey, 2);

        // FFmpeg Path
        tlp.Controls.Add(new Label { Text = "FFmpeg Path:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 4);
        _txtFfmpegPath = new TextBox { Dock = DockStyle.Fill };
        tlp.Controls.Add(_txtFfmpegPath, 1, 4);
        _btnBrowseFfmpeg = new Button { Text = "Browse...", Dock = DockStyle.Fill, Height = 28, UseCompatibleTextRendering = false, UseVisualStyleBackColor = true };
        _btnBrowseFfmpeg.Click += BrowseFfmpeg_Click;
        tlp.Controls.Add(_btnBrowseFfmpeg, 2, 4);

        // Silence gap
        tlp.Controls.Add(new Label { Text = "Nghỉ giữa câu (ms):", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 5);
        _nudSilenceGap = new NumericUpDown
        {
            Minimum = 0, Maximum = 3000, Increment = 50, Value = 250,
            Dock = DockStyle.Fill
        };
        tlp.Controls.Add(_nudSilenceGap, 1, 5);

        // Help info label
        var lblInfo = new Label
        {
            Text = "Cấu hình được lưu trong file config.json cạnh file .exe",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill
        };
        tlp.Controls.Add(lblInfo, 0, 6);
        tlp.SetColumnSpan(lblInfo, 3);

        // Buttons
        var pnlButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _btnCancel = new Button { Text = "Hủy", Width = 84, Height = 30, DialogResult = DialogResult.Cancel, UseCompatibleTextRendering = false, UseVisualStyleBackColor = true };
        _btnOk = new Button { Text = "Lưu", Width = 84, Height = 30, DialogResult = DialogResult.None, UseCompatibleTextRendering = false, UseVisualStyleBackColor = true };
        _btnOk.Click += BtnOk_Click;
        pnlButtons.Controls.AddRange(new Control[] { _btnCancel, _btnOk });
        tlp.Controls.Add(pnlButtons, 0, 7);
        tlp.SetColumnSpan(pnlButtons, 3);

        Controls.Add(tlp);
        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }

    private void LoadValues()
    {
        _txtLocalApiUrl.Text = _config.LocalApiUrl;
        _txtLocalApiModel.Text = _config.LocalApiModel;
        _txtLocalApiKey.Text = _config.LocalApiKey;
        _txtElevenLabsKey.Text = _config.ElevenLabsApiKey;
        _txtFfmpegPath.Text = _config.FfmpegPath;
        _nudSilenceGap.Value = Math.Clamp(_config.SilenceGapMs, 0, 3000);
    }

    private void BrowseFfmpeg_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Chọn ffmpeg.exe",
            Filter = "ffmpeg.exe|ffmpeg.exe|All executables|*.exe",
            FileName = "ffmpeg.exe"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtFfmpegPath.Text = dlg.FileName;
    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        _config.LocalApiUrl = _txtLocalApiUrl.Text.Trim();
        _config.LocalApiModel = _txtLocalApiModel.Text.Trim();
        _config.LocalApiKey = _txtLocalApiKey.Text.Trim();
        _config.ElevenLabsApiKey = _txtElevenLabsKey.Text.Trim();
        _config.FfmpegPath = _txtFfmpegPath.Text.Trim();
        _config.SilenceGapMs = (int)_nudSilenceGap.Value;
        DialogResult = DialogResult.OK;
        Close();
    }
}
