using System.Diagnostics;
using System.Text;
using AutoTtsStudio.Models;
using AutoTtsStudio.Services;

namespace AutoTtsStudio.Forms;

/// <summary>
/// Giao diện Bóc Timeline Chuẩn - hỗ trợ bóc theo dự án hoặc audio ngoài,
/// gom cảnh chuẩn theo giây, kiểm tra tính toàn vẹn 100% và xuất ra các định dạng sản xuất video.
/// </summary>
[System.ComponentModel.DesignerCategory("")]
public sealed class TimelineBuilderForm : Form
{
    private readonly List<TtsLineItem>? _projectLines;
    private readonly int _silenceGapMs;

    private List<TimelineEntry> _rawEntries = new();
    private List<TimelineEntry> _currentDisplayEntries = new();

    // Controls
    private Button _btnLoadProject = null!;
    private Button _btnLoadFile = null!;
    private Label _lblSourceInfo = null!;

    private RadioButton _rbExactSentences = null!;
    private RadioButton _rbGroupScenes = null!;
    private Label _lblMinSec = null!;
    private NumericUpDown _nudMinSec = null!;
    private Label _lblMaxSec = null!;
    private NumericUpDown _nudMaxSec = null!;
    private Button _btnApplyGroup = null!;

    private SmoothDataGridView _gridTimeline = null!;
    private Label _lblValidationStatus = null!;
    private ProgressBar _progressBar = null!;

    private Button _btnExportTxt = null!;
    private Button _btnExportPrompts = null!;
    private Button _btnExportSrt = null!;
    private Button _btnCopyTimeline = null!;
    private Button _btnClose = null!;

    public TimelineBuilderForm(IEnumerable<TtsLineItem>? projectLines = null, int silenceGapMs = 250)
    {
        _projectLines = projectLines?.ToList();
        _silenceGapMs = silenceGapMs;

        InitializeLayout();
        WireEvents();

        // Tự động nạp từ dự án nếu có dữ liệu âm thanh hợp lệ
        if (_projectLines != null && _projectLines.Any(i => !string.IsNullOrEmpty(i.OutputPath) && File.Exists(i.OutputPath)))
        {
            LoadFromProject();
        }
        else
        {
            UpdateValidationDisplay();
        }
    }

    private void InitializeLayout()
    {
        Text = "Bóc Timeline Chuẩn - AutoTTS Studio";
        Size = new Size(1060, 700);
        MinimumSize = new Size(920, 580);
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        BackColor = Color.FromArgb(248, 249, 250);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Tầng 1: Chọn nguồn
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); // Tầng 2: Chế độ gom cảnh
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tầng 3: Bảng Timeline
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); // Tầng 4: Trạng thái & Nút thao tác

        // ─────────────────────────────────────────────────────────────────────
        // Tầng 1: Chọn nguồn
        // ─────────────────────────────────────────────────────────────────────
        var sourcePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };

        _btnLoadProject = new Button
        {
            Text = "📁 Lấy từ dự án hiện tại",
            Height = 32,
            AutoSize = true,
            UseVisualStyleBackColor = true,
            Margin = new Padding(0, 4, 8, 4)
        };

        _btnLoadFile = new Button
        {
            Text = "🎵 Nạp file MP3 / WAV ngoài...",
            Height = 32,
            AutoSize = true,
            UseVisualStyleBackColor = true,
            Margin = new Padding(0, 4, 12, 4)
        };

        _lblSourceInfo = new Label
        {
            Text = "Nguồn: Chưa nạp dữ liệu",
            AutoSize = true,
            ForeColor = Color.FromArgb(75, 85, 99),
            Margin = new Padding(0, 11, 0, 0)
        };

        sourcePanel.Controls.Add(_btnLoadProject);
        sourcePanel.Controls.Add(_btnLoadFile);
        sourcePanel.Controls.Add(_lblSourceInfo);
        mainLayout.Controls.Add(sourcePanel, 0, 0);

        // ─────────────────────────────────────────────────────────────────────
        // Tầng 2: Tùy chọn chế độ
        // ─────────────────────────────────────────────────────────────────────
        var modePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false
        };

        _rbExactSentences = new RadioButton
        {
            Text = "Giữ nguyên từng câu",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 8, 16, 4)
        };

        _rbGroupScenes = new RadioButton
        {
            Text = "Gom cảnh video:",
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 4)
        };

        _lblMinSec = new Label
        {
            Text = "Min (s):",
            AutoSize = true,
            Margin = new Padding(4, 11, 4, 0),
            Enabled = false
        };

        _nudMinSec = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 60,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 5.0m,
            Width = 60,
            Margin = new Padding(0, 8, 8, 4),
            Enabled = false
        };

        _lblMaxSec = new Label
        {
            Text = "Max (s):",
            AutoSize = true,
            Margin = new Padding(4, 11, 4, 0),
            Enabled = false
        };

        _nudMaxSec = new NumericUpDown
        {
            Minimum = 2,
            Maximum = 120,
            DecimalPlaces = 1,
            Increment = 0.5m,
            Value = 8.0m,
            Width = 60,
            Margin = new Padding(0, 8, 8, 4),
            Enabled = false
        };

        _btnApplyGroup = new Button
        {
            Text = "🔄 Áp dụng",
            Height = 28,
            AutoSize = true,
            UseVisualStyleBackColor = true,
            Margin = new Padding(4, 7, 12, 4),
            Enabled = false
        };

        _progressBar = new ProgressBar
        {
            Width = 140,
            Height = 16,
            Style = ProgressBarStyle.Marquee,
            Visible = false,
            Margin = new Padding(4, 13, 0, 0)
        };

        modePanel.Controls.Add(_rbExactSentences);
        modePanel.Controls.Add(_rbGroupScenes);
        modePanel.Controls.Add(_lblMinSec);
        modePanel.Controls.Add(_nudMinSec);
        modePanel.Controls.Add(_lblMaxSec);
        modePanel.Controls.Add(_nudMaxSec);
        modePanel.Controls.Add(_btnApplyGroup);
        modePanel.Controls.Add(_progressBar);
        mainLayout.Controls.Add(modePanel, 0, 1);

        // ─────────────────────────────────────────────────────────────────────
        // Tầng 3: DataGridView hiển thị 5 cột chuẩn
        // ─────────────────────────────────────────────────────────────────────
        _gridTimeline = new SmoothDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            ShowCellToolTips = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.FromArgb(229, 231, 235),
            RowTemplate = { Height = 28 }
        };

        _gridTimeline.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(4)
        };
        _gridTimeline.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        _gridTimeline.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);

        _gridTimeline.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn
            {
                Name = "colIndex",
                HeaderText = "STT",
                Width = 55,
                MinimumWidth = 55,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            },
            new DataGridViewTextBoxColumn
            {
                Name = "colStart",
                HeaderText = "Mốc [MM:SS]",
                Width = 110,
                MinimumWidth = 100,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            },
            new DataGridViewTextBoxColumn
            {
                Name = "colEnd",
                HeaderText = "Kết thúc",
                Width = 110,
                MinimumWidth = 100,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            },
            new DataGridViewTextBoxColumn
            {
                Name = "colDuration",
                HeaderText = "Thời lượng (s)",
                Width = 115,
                MinimumWidth = 105,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            },
            new DataGridViewTextBoxColumn
            {
                Name = "colText",
                HeaderText = "Nội dung thoại (Có thể sửa trực tiếp)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 300,
                ReadOnly = false, // Cho phép sửa trực tiếp nội dung trước khi xuất
                SortMode = DataGridViewColumnSortMode.NotSortable
            }
        });

        mainLayout.Controls.Add(_gridTimeline, 0, 2);

        // ─────────────────────────────────────────────────────────────────────
        // Tầng 4: Trạng thái kiểm tra & Nút xuất file
        // ─────────────────────────────────────────────────────────────────────
        var bottomLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _lblValidationStatus = new Label
        {
            Text = "Chưa có dữ liệu timeline.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(107, 114, 128)
        };

        var actionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnClose = MakeActionBtn("Đóng");
        _btnCopyTimeline = MakeActionBtn("📋 Copy Timeline");
        _btnExportSrt = MakeActionBtn("Xuất Phụ Đề (.srt)");
        _btnExportPrompts = MakeActionBtn("Xuất Khung Prompt Ảnh (.txt)");
        _btnExportTxt = MakeActionBtn("Xuất Timeline (.txt)");

        actionsPanel.Controls.Add(_btnClose);
        actionsPanel.Controls.Add(_btnCopyTimeline);
        actionsPanel.Controls.Add(_btnExportSrt);
        actionsPanel.Controls.Add(_btnExportPrompts);
        actionsPanel.Controls.Add(_btnExportTxt);

        bottomLayout.Controls.Add(_lblValidationStatus, 0, 0);
        bottomLayout.Controls.Add(actionsPanel, 1, 0);
        mainLayout.Controls.Add(bottomLayout, 0, 3);

        Controls.Add(mainLayout);
    }

    private static Button MakeActionBtn(string text)
    {
        return new Button
        {
            Text = text,
            Height = 32,
            AutoSize = true,
            UseVisualStyleBackColor = true,
            Margin = new Padding(4, 8, 0, 0)
        };
    }

    private void WireEvents()
    {
        _btnLoadProject.Click += (s, e) => LoadFromProject();
        _btnLoadFile.Click += async (s, e) => await LoadFromExternalAudioAsync();

        _rbExactSentences.CheckedChanged += (s, e) =>
        {
            bool isGroup = _rbGroupScenes.Checked;
            _lblMinSec.Enabled = isGroup;
            _nudMinSec.Enabled = isGroup;
            _lblMaxSec.Enabled = isGroup;
            _nudMaxSec.Enabled = isGroup;
            _btnApplyGroup.Enabled = isGroup;

            if (_rbExactSentences.Checked)
            {
                ApplyCurrentMode();
            }
        };

        _rbGroupScenes.CheckedChanged += (s, e) =>
        {
            bool isGroup = _rbGroupScenes.Checked;
            _lblMinSec.Enabled = isGroup;
            _nudMinSec.Enabled = isGroup;
            _lblMaxSec.Enabled = isGroup;
            _nudMaxSec.Enabled = isGroup;
            _btnApplyGroup.Enabled = isGroup;

            if (_rbGroupScenes.Checked)
            {
                ApplyCurrentMode();
            }
        };

        _btnApplyGroup.Click += (s, e) => ApplyCurrentMode();

        _gridTimeline.CellEndEdit += GridTimeline_CellEndEdit;

        _btnExportTxt.Click += (s, e) => ExportTimelineTxt();
        _btnExportPrompts.Click += (s, e) => ExportPromptTemplate();
        _btnExportSrt.Click += (s, e) => ExportSrt();
        _btnCopyTimeline.Click += (s, e) => CopyTimelineToClipboard();
        _btnClose.Click += (s, e) => Close();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Nạp dữ liệu
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadFromProject()
    {
        if (_projectLines == null || _projectLines.Count == 0)
        {
            MessageBox.Show("Dự án hiện tại chưa có danh sách kịch bản.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var doneCount = _projectLines.Count(i => !string.IsNullOrEmpty(i.OutputPath) && File.Exists(i.OutputPath));
        if (doneCount == 0)
        {
            MessageBox.Show("Chưa có câu nào được tạo file âm thanh đầu ra. Vui lòng bấm 'Start' để tạo âm thanh trước.",
                "Chưa có âm thanh", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _rawEntries = TimelineExtractorService.ExtractFromProject(_projectLines, _silenceGapMs);
        _lblSourceInfo.Text = $"Nguồn: Dự án hiện tại ({_rawEntries.Count} câu hợp lệ)";
        _lblSourceInfo.ForeColor = Color.FromArgb(5, 150, 105);

        ApplyCurrentMode();
    }

    private async Task LoadFromExternalAudioAsync()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Chọn file âm thanh để bóc timeline",
            Filter = "Audio Files (*.mp3;*.wav;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.m4a;*.ogg;*.flac|All Files (*.*)|*.*"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var audioPath = dlg.FileName;
        SetBusy(true, "Đang bóc timeline bằng Whisper AI...");

        try
        {
            var progress = new Progress<string>(msg =>
            {
                if (InvokeRequired) BeginInvoke(() => _lblSourceInfo.Text = msg);
                else _lblSourceInfo.Text = msg;
            });

            var entries = await Task.Run(() =>
                TimelineExtractorService.ExtractFromAudioFileAsync(audioPath, progress));

            if (entries.Count == 0)
            {
                MessageBox.Show("Không trích xuất được mốc timeline nào từ file âm thanh đã chọn.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _rawEntries = entries;
            _lblSourceInfo.Text = $"Nguồn: {Path.GetFileName(audioPath)} ({entries.Count} mốc câu)";
            _lblSourceInfo.ForeColor = Color.FromArgb(5, 150, 105);

            ApplyCurrentMode();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi bóc timeline", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _lblSourceInfo.Text = "Lỗi khi nạp file âm thanh.";
            _lblSourceInfo.ForeColor = Color.Crimson;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _progressBar.Visible = busy;
        _btnLoadProject.Enabled = !busy;
        _btnLoadFile.Enabled = !busy;
        _btnApplyGroup.Enabled = !busy && _rbGroupScenes.Checked;
        _btnExportTxt.Enabled = !busy;
        _btnExportPrompts.Enabled = !busy;
        _btnExportSrt.Enabled = !busy;
        _btnCopyTimeline.Enabled = !busy;

        if (busy && message != null)
        {
            _lblSourceInfo.Text = message;
            _lblSourceInfo.ForeColor = Color.FromArgb(37, 99, 235);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Chế độ & Render bảng
    // ─────────────────────────────────────────────────────────────────────────

    private void ApplyCurrentMode()
    {
        if (_rawEntries.Count == 0)
        {
            _currentDisplayEntries.Clear();
            RenderGrid();
            UpdateValidationDisplay();
            return;
        }

        if (_rbGroupScenes.Checked)
        {
            double minSec = (double)_nudMinSec.Value;
            double maxSec = (double)_nudMaxSec.Value;
            if (maxSec < minSec)
            {
                maxSec = minSec + 2.0;
                _nudMaxSec.Value = (decimal)maxSec;
            }

            _currentDisplayEntries = TimelineExtractorService.GroupEntriesByDuration(_rawEntries, minSec, maxSec);
        }
        else
        {
            _currentDisplayEntries = _rawEntries.Select(e => e.Clone()).ToList();
            for (int i = 0; i < _currentDisplayEntries.Count; i++)
            {
                _currentDisplayEntries[i].Index = i + 1;
            }
        }

        RenderGrid();
        UpdateValidationDisplay();
    }

    private void RenderGrid()
    {
        _gridTimeline.SuspendLayout();
        _gridTimeline.Rows.Clear();

        foreach (var item in _currentDisplayEntries)
        {
            int row = _gridTimeline.Rows.Add(
                item.Index,
                item.StartTag,
                item.EndTag,
                $"{item.DurationSeconds:F2}s",
                item.Text
            );
            _gridTimeline.Rows[row].Tag = item;
        }

        _gridTimeline.ResumeLayout();
    }

    private void GridTimeline_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _currentDisplayEntries.Count) return;
        var colText = _gridTimeline.Columns["colText"];
        if (colText != null && e.ColumnIndex == colText.Index)
        {
            var newText = _gridTimeline.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? string.Empty;
            _currentDisplayEntries[e.RowIndex].Text = newText.Trim();
            UpdateValidationDisplay();
        }
    }

    private void UpdateValidationDisplay()
    {
        bool isValid = TimelineExtractorService.ValidateTimeline(_currentDisplayEntries, out string report);

        if (isValid)
        {
            _lblValidationStatus.Text = $"✔ {report} ({_currentDisplayEntries.Count} cảnh)";
            _lblValidationStatus.ForeColor = Color.FromArgb(5, 150, 105); // Xanh lá
            EnableExportButtons(true);
        }
        else
        {
            _lblValidationStatus.Text = $"⚠ {report}";
            _lblValidationStatus.ForeColor = Color.Crimson; // Đỏ
            EnableExportButtons(_currentDisplayEntries.Count > 0);
        }
    }

    private void EnableExportButtons(bool enabled)
    {
        _btnExportTxt.Enabled = enabled;
        _btnExportPrompts.Enabled = enabled;
        _btnExportSrt.Enabled = enabled;
        _btnCopyTimeline.Enabled = enabled;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Xuất file & Clipboard
    // ─────────────────────────────────────────────────────────────────────────

    private void ExportTimelineTxt()
    {
        if (!EnsureHasEntries()) return;

        using var dlg = new SaveFileDialog
        {
            Title = "Xuất Timeline văn bản",
            Filter = "Text files (*.txt)|*.txt",
            FileName = "Timeline.txt"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var content = TimelineExtractorService.BuildTimelineTxt(_currentDisplayEntries);
            File.WriteAllText(dlg.FileName, content, new UTF8Encoding(false));
            OfferOpenFile(dlg.FileName, "Xuất Timeline thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportPromptTemplate()
    {
        if (!EnsureHasEntries()) return;

        using var dlg = new SaveFileDialog
        {
            Title = "Xuất Khung Prompt Ảnh",
            Filter = "Text files (*.txt)|*.txt",
            FileName = "Prompt_Template.txt"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var content = TimelineExtractorService.BuildLockedImagePromptTemplate(_currentDisplayEntries);
            File.WriteAllText(dlg.FileName, content, new UTF8Encoding(false));
            OfferOpenFile(dlg.FileName, "Xuất Khung Prompt Ảnh thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportSrt()
    {
        if (!EnsureHasEntries()) return;

        using var dlg = new SaveFileDialog
        {
            Title = "Xuất Phụ Đề SRT",
            Filter = "SRT Subtitle files (*.srt)|*.srt",
            FileName = "Subtitles.srt"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var content = TimelineExtractorService.BuildSrt(_currentDisplayEntries);
            File.WriteAllText(dlg.FileName, content, new UTF8Encoding(false));
            OfferOpenFile(dlg.FileName, "Xuất Phụ Đề SRT thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyTimelineToClipboard()
    {
        if (!EnsureHasEntries()) return;

        try
        {
            var content = TimelineExtractorService.BuildTimelineTxt(_currentDisplayEntries);
            Clipboard.SetText(content);
            MessageBox.Show($"Đã sao chép thành công {_currentDisplayEntries.Count} mốc timeline vào Clipboard!",
                "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi copy Clipboard: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool EnsureHasEntries()
    {
        if (_currentDisplayEntries.Count == 0)
        {
            MessageBox.Show("Chưa có mốc timeline nào để xuất. Vui lòng nạp dữ liệu trước.",
                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        return true;
    }

    private static void OfferOpenFile(string filePath, string successMessage)
    {
        var res = MessageBox.Show(
            $"{successMessage}\n\nĐường dẫn: {filePath}\n\nBạn có muốn mở thư mục chứa file không?",
            "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (res == DialogResult.Yes)
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
    }
}
