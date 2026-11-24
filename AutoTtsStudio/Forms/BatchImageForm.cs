using System.ComponentModel;
using System.Diagnostics;
using AutoTtsStudio.Services;

namespace AutoTtsStudio.Forms;

/// <summary>
/// Giao diện xử lý ảnh hàng loạt: Xóa watermark/logo, chuyển đổi .webp sang PNG/JPG, và upscale 1080p.
/// </summary>
[DesignerCategory("")]
public sealed class BatchImageForm : Form
{
    private TextBox _txtFolder = null!;
    private Button _btnBrowse = null!;
    private NumericUpDown _nudCropPercent = null!;
    private ComboBox _cmbFormat = null!;
    private CheckBox _chkUpscale = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblProgress = null!;
    private Button _btnStart = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnClose = null!;

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public BatchImageForm()
    {
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        Text = "Xử lý Ảnh Hàng Loạt (Xóa Watermark & Đổi định dạng WebP)";
        ClientSize = new Size(680, 340);
        MinimumSize = new Size(620, 320);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(248, 249, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 14, 16, 14)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Thư mục ảnh nguồn
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Tùy chọn cắt viền & định dạng
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // Checkbox Upscale 1080p
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Progress bar & status
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // Action buttons

        // ── Hàng 0: Thư mục ảnh nguồn ──────────────────────────────────────────
        var pnlFolder = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        pnlFolder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlFolder.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlFolder.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlFolder.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblFolder = new Label
        {
            Text = "Thư mục ảnh:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        };
        _txtFolder = new TextBox
        {
            Dock = DockStyle.Fill,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            PlaceholderText = "Chọn thư mục chứa ảnh cần xử lý (.webp, .png, .jpg)..."
        };
        _btnBrowse = new Button
        {
            Text = "Chọn thư mục...",
            AutoSize = true,
            Height = 29,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = true,
            UseCompatibleTextRendering = false
        };
        _btnBrowse.Click += BtnBrowse_Click;

        pnlFolder.Controls.Add(lblFolder, 0, 0);
        pnlFolder.Controls.Add(_txtFolder, 1, 0);
        pnlFolder.Controls.Add(_btnBrowse, 2, 0);
        root.Controls.Add(pnlFolder, 0, 0);

        // ── Hàng 1: Tỷ lệ cắt watermark & Định dạng xuất ────────────────────────
        var pnlParams = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0)
        };
        pnlParams.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlParams.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlParams.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlParams.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        pnlParams.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var lblCrop = new Label
        {
            Text = "Cắt viền watermark (%):",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        };
        _nudCropPercent = new NumericUpDown
        {
            DecimalPlaces = 1,
            Minimum = 0,
            Maximum = 15,
            Increment = 0.5m,
            Value = 4.5m,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 0, 16, 0)
        };

        var lblFormat = new Label
        {
            Text = "Định dạng xuất:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0)
        };
        _cmbFormat = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        _cmbFormat.Items.AddRange(new object[]
        {
            "PNG (Nét nhất cho video 2D)",
            "JPG (Nhẹ, chất lượng 95%)"
        });
        _cmbFormat.SelectedIndex = 0;

        pnlParams.Controls.Add(lblCrop, 0, 0);
        pnlParams.Controls.Add(_nudCropPercent, 1, 0);
        pnlParams.Controls.Add(lblFormat, 2, 0);
        pnlParams.Controls.Add(_cmbFormat, 3, 0);
        root.Controls.Add(pnlParams, 0, 1);

        // ── Hàng 2: Tùy chọn Upscale 1080p ─────────────────────────────────────
        _chkUpscale = new CheckBox
        {
            Text = "Tự động nâng lên 1920x1080 (Lanczos3) nếu ảnh gốc nhỏ hơn",
            AutoSize = true,
            Checked = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 0, 0)
        };
        root.Controls.Add(_chkUpscale, 0, 2);

        // ── Hàng 3: Tiến độ & Thông báo ────────────────────────────────────────
        var pnlProgress = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 6, 0, 6)
        };
        pnlProgress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlProgress.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        pnlProgress.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Height = 18,
            Style = ProgressBarStyle.Continuous
        };
        _lblProgress = new Label
        {
            Text = "Sẵn sàng (chọn thư mục ảnh và bấm 'Bắt đầu xử lý')",
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.FromArgb(70, 70, 70),
            TextAlign = ContentAlignment.MiddleLeft
        };

        pnlProgress.Controls.Add(_progressBar, 0, 0);
        pnlProgress.Controls.Add(_lblProgress, 0, 1);
        root.Controls.Add(pnlProgress, 0, 3);

        // ── Hàng 4: Action Buttons ─────────────────────────────────────────────
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0),
            Padding = new Padding(0, 4, 0, 0)
        };

        _btnClose = new Button
        {
            Text = "Đóng",
            Width = 90,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0),
            UseVisualStyleBackColor = true,
            UseCompatibleTextRendering = false
        };
        _btnClose.Click += (s, e) => Close();

        _btnOpenFolder = new Button
        {
            Text = "📂 Mở Cleaned_Images",
            AutoSize = true,
            Height = 32,
            Margin = new Padding(8, 0, 0, 0),
            Enabled = false,
            UseVisualStyleBackColor = true,
            UseCompatibleTextRendering = false
        };
        _btnOpenFolder.Click += BtnOpenFolder_Click;

        _btnStart = new Button
        {
            Text = "▶ Bắt đầu xử lý",
            Width = 140,
            Height = 32,
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Margin = new Padding(0),
            UseVisualStyleBackColor = false,
            UseCompatibleTextRendering = false
        };
        _btnStart.FlatAppearance.BorderSize = 0;
        _btnStart.Click += BtnStart_Click;

        pnlButtons.Controls.Add(_btnClose);
        pnlButtons.Controls.Add(_btnOpenFolder);
        pnlButtons.Controls.Add(_btnStart);
        root.Controls.Add(pnlButtons, 0, 4);

        Controls.Add(root);

        FormClosing += BatchImageForm_FormClosing;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Chọn thư mục chứa ảnh cần xử lý",
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(_txtFolder.Text) && Directory.Exists(_txtFolder.Text))
        {
            dlg.InitialDirectory = _txtFolder.Text;
        }

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _txtFolder.Text = dlg.SelectedPath;
            string outDir = Path.Combine(dlg.SelectedPath, "Cleaned_Images");
            _btnOpenFolder.Enabled = Directory.Exists(outDir);
        }
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_isRunning)
        {
            // Hủy xử lý
            _cts?.Cancel();
            _btnStart.Enabled = false;
            _lblProgress.Text = "Đang hủy xử lý...";
            return;
        }

        string inputFolder = _txtFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
        {
            MessageBox.Show("Vui lòng chọn một thư mục ảnh hợp lệ trước khi bắt đầu.", "Chưa chọn thư mục",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isRunning = true;
        _cts = new CancellationTokenSource();
        SetInputControlsEnabled(false);
        _btnStart.Text = "⏹ Hủy xử lý";
        _btnStart.BackColor = Color.FromArgb(220, 38, 38);
        _btnOpenFolder.Enabled = false;

        double cropPercent = (double)_nudCropPercent.Value;
        string outputFormat = _cmbFormat.SelectedItem?.ToString() ?? "PNG";
        bool upscale = _chkUpscale.Checked;

        var progress = new Progress<(int current, int total, string fileName)>(info =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => UpdateProgressUI(info.current, info.total, info.fileName));
            }
            else
            {
                UpdateProgressUI(info.current, info.total, info.fileName);
            }
        });

        var sw = Stopwatch.StartNew();

        try
        {
            int count = await Task.Run(() => BatchImageProcessorService.ProcessFolderAsync(
                inputFolder, cropPercent, outputFormat, upscale, progress, _cts.Token), _cts.Token);

            sw.Stop();

            string outDir = Path.Combine(inputFolder, "Cleaned_Images");
            _btnOpenFolder.Enabled = Directory.Exists(outDir);

            if (count == 0)
            {
                _lblProgress.Text = "Không tìm thấy file ảnh (.webp, .png, .jpg, .jpeg) trong thư mục.";
                MessageBox.Show("Không tìm thấy file ảnh phù hợp trong thư mục đã chọn.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _lblProgress.Text = $"Đã xử lý xong {count} ảnh ({sw.Elapsed.TotalSeconds:F1}s). Đã lưu vào Cleaned_Images.";
                MessageBox.Show($"Xử lý thành công {count} ảnh!\nThời gian: {sw.Elapsed.TotalSeconds:F1} giây\nThư mục xuất: Cleaned_Images",
                    "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            _lblProgress.Text = "Đã dừng xử lý theo yêu cầu của bạn.";
        }
        catch (Exception ex)
        {
            _lblProgress.Text = $"Lỗi: {ex.Message}";
            MessageBox.Show($"Lỗi trong quá trình xử lý ảnh: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;

            _btnStart.Text = "▶ Bắt đầu xử lý";
            _btnStart.BackColor = Color.FromArgb(37, 99, 235);
            _btnStart.Enabled = true;
            SetInputControlsEnabled(true);

            string outDir = Path.Combine(inputFolder, "Cleaned_Images");
            _btnOpenFolder.Enabled = Directory.Exists(outDir);
        }
    }

    private void UpdateProgressUI(int current, int total, string fileName)
    {
        _progressBar.Maximum = total;
        _progressBar.Value = Math.Min(current, total);
        _lblProgress.Text = $"Đang xử lý ({current}/{total}): {fileName}";
    }

    private void SetInputControlsEnabled(bool enabled)
    {
        _txtFolder.Enabled = enabled;
        _btnBrowse.Enabled = enabled;
        _nudCropPercent.Enabled = enabled;
        _cmbFormat.Enabled = enabled;
        _chkUpscale.Enabled = enabled;
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        string inputFolder = _txtFolder.Text.Trim();
        string outDir = Path.Combine(inputFolder, "Cleaned_Images");
        if (Directory.Exists(outDir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = outDir,
                UseShellExecute = true
            });
        }
        else
        {
            MessageBox.Show("Thư mục Cleaned_Images chưa được tạo.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BatchImageForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isRunning)
        {
            _cts?.Cancel();
        }
    }
}
