using AutoTtsStudio.Models;

namespace AutoTtsStudio.Forms;

partial class MainForm
{
    // ── Tầng 1: Voice ──────────────────────────────────────────────────────────
    private GroupBox _grpVoice = null!;
    private ComboBox _cmbEngine = null!;
    private TextBox _txtVoiceSearch = null!;
    private Button _btnVoiceSearch = null!;
    private ComboBox _cmbVoice = null!;
    private Button _btnFavoriteVoice = null!;
    private CheckBox _chkOnlyFavorites = null!;
    private ComboBox _cmbModel = null!;
    private ComboBox _cmbLanguage = null!;

    // ── Tầng 1: Voice Settings ─────────────────────────────────────────────────
    private GroupBox _grpVoiceSettings = null!;
    private CheckBox _chkCustomVoice = null!;
    private NumericUpDown _nudSpeed = null!;
    private NumericUpDown _nudPitch = null!;
    private NumericUpDown _nudStability = null!;
    private NumericUpDown _nudSimilarity = null!;
    private NumericUpDown _nudStyle = null!;
    private CheckBox _chkSpeakerBoost = null!;
    private Button _btnReset = null!;
    private Label _lblSpeed = null!, _lblPitch = null!, _lblStability = null!,
                  _lblSimilarity = null!, _lblStyle = null!;

    // ── Tầng 1: Proxy ──────────────────────────────────────────────────────────
    private GroupBox _grpProxy = null!;
    private ComboBox _cmbProxyMode = null!;
    private Button _btnLoadProxy = null!;
    private Label _lblProxyStat = null!;
    private Button _btnSettings = null!;

    // ── Tầng 2: Batch Job ──────────────────────────────────────────────────────
    private GroupBox _grpBatch = null!;
    private TextBox _txtBatchFolder = null!;
    private Button _btnBatchChooseFolder = null!;
    private CheckBox _chkAutoSrt = null!;
    private CheckBox _chkAutoJoin = null!;
    private Button _btnRunBatch = null!;
    private ListBox _lstBatchFiles = null!;

    // ── Tầng 2: Options ────────────────────────────────────────────────────────
    private GroupBox _grpOptions = null!;
    private CheckBox _chkAutoRetry = null!;
    private NumericUpDown _nudThreads = null!;
    private CheckBox _chkAutoSplit = null!;
    private NumericUpDown _nudMaxChars = null!;
    private NumericUpDown _nudSilenceGap = null!;

    // ── Tầng 3: Subtitles ──────────────────────────────────────────────────────
    private GroupBox _grpSubtitles = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnImportFile = null!;
    private Button _btnPasteText = null!;
    private Button _btnImportFolder = null!;
    private Button _btnPreview = null!;
    private Button _btnOpenOutput = null!;
    private Button _btnCreateSrt = null!;
    private Button _btnJoinMp3 = null!;
    private Button _btnClearList = null!;
    private SmoothDataGridView _gridSubtitles = null!;
    private ContextMenuStrip _ctxGrid = null!;

    // ── Status Strip ───────────────────────────────────────────────────────────
    private StatusStrip _statusStrip = null!;
    private ToolStripStatusLabel _lblStatusEngine = null!;
    private ToolStripStatusLabel _lblStatusFfmpeg = null!;
    private ToolStripProgressBar _progressBar = null!;

    private void InitializeComponent()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1240, 760);
        MinimumSize = new Size(1120, 680);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AutoTTS Studio";
        ResumeLayout(false);
    }

    private void BuildCustomLayout()
    {
        Controls.Clear();
        SuspendLayout();

        Text = "AutoTTS Studio";
        ClientSize = new Size(1240, 760);
        MinimumSize = new Size(1120, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;

        // ─── Root layout: 3 rows ─────────────────────────────────────────────
        var rootTlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(6, 6, 6, 2)
        };
        rootTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));  // Tầng 1: min 195-205px
        rootTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));  // Tầng 2: ~145px
        rootTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Tầng 3: remainder

        // ─── Tầng 1 (34% | 42% | 24%) ─────────────────────────────────────────
        var row1 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4)
        };
        row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        row1.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        row1.Controls.Add(BuildVoiceGroupBox(), 0, 0);
        row1.Controls.Add(BuildVoiceSettingsGroupBox(), 1, 0);
        row1.Controls.Add(BuildProxyGroupBox(), 2, 0);
        rootTlp.Controls.Add(row1, 0, 0);

        // ─── Tầng 2 ──────────────────────────────────────────────────────────
        var row2 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 4)
        };
        row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        row2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row2.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        row2.Controls.Add(BuildBatchGroupBox(), 0, 0);
        row2.Controls.Add(BuildOptionsGroupBox(), 1, 0);
        rootTlp.Controls.Add(row2, 0, 1);

        // ─── Tầng 3 ──────────────────────────────────────────────────────────
        rootTlp.Controls.Add(BuildSubtitlesGroupBox(), 0, 2);

        // ─── Status Strip ─────────────────────────────────────────────────────
        BuildStatusStrip();

        Controls.Add(rootTlp);
        Controls.Add(_statusStrip);

        ResumeLayout(false);
        PerformLayout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build helpers
    // ─────────────────────────────────────────────────────────────────────────

    private GroupBox BuildVoiceGroupBox()
    {
        _grpVoice = new GroupBox
        {
            Text = "Voice",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6)
        };

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = false
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        // Row 0 — Engine
        tlp.Controls.Add(MakeLabel("Engine:"), 0, 0);
        _cmbEngine = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbEngine.Items.AddRange(new object[]
        {
            "Edge-TTS (Miễn phí)",
            "Local AI (OpenAI API)",
            "ElevenLabs API"
        });
        _cmbEngine.SelectedIndex = 0;
        tlp.Controls.Add(_cmbEngine, 1, 0);

        // Row 1 — Voice Search + Favorites CheckBox
        tlp.Controls.Add(MakeLabel("Tìm:"), 0, 1);
        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtVoiceSearch = new TextBox { Dock = DockStyle.Fill };
        _txtVoiceSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) VoiceSearch_Click(s, e); };
        searchRow.Controls.Add(_txtVoiceSearch, 0, 0);

        _btnVoiceSearch = new Button
        {
            Text = "Search",
            AutoSize = true,
            Height = 28,
            Margin = new Padding(4, 0, 4, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnVoiceSearch.Click += VoiceSearch_Click;
        searchRow.Controls.Add(_btnVoiceSearch, 1, 0);

        _chkOnlyFavorites = new CheckBox
        {
            Text = "Chỉ hiện giọng yêu thích",
            AutoSize = true,
            AutoEllipsis = false,
            Margin = new Padding(4, 4, 0, 0)
        };
        _chkOnlyFavorites.CheckedChanged += ChkOnlyFavorites_CheckedChanged;
        searchRow.Controls.Add(_chkOnlyFavorites, 2, 0);
        tlp.Controls.Add(searchRow, 1, 1);

        // Row 2 — Voice ComboBox + Favorite Button
        tlp.Controls.Add(MakeLabel("Voice:"), 0, 2);
        var voiceRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        voiceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        voiceRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        voiceRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _cmbVoice = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        voiceRow.Controls.Add(_cmbVoice, 0, 0);

        _btnFavoriteVoice = new Button
        {
            Text = "★ Lưu",
            AutoSize = true,
            Height = 28,
            Margin = new Padding(4, 0, 0, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnFavoriteVoice.Click += BtnFavoriteVoice_Click;
        voiceRow.Controls.Add(_btnFavoriteVoice, 1, 0);
        tlp.Controls.Add(voiceRow, 1, 2);

        // Row 3 — Model (65%) + Language (35%)
        tlp.Controls.Add(MakeLabel("Model:"), 0, 3);
        var modelLangRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        modelLangRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        modelLangRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        modelLangRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _cmbModel = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        modelLangRow.Controls.Add(_cmbModel, 0, 0);

        _cmbLanguage = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(4, 0, 0, 0) };
        _cmbLanguage.Items.AddRange(new object[] { "All", "vi-VN", "en-US", "en-GB", "ja-JP", "ko-KR", "zh-CN", "fr-FR", "de-DE" });
        _cmbLanguage.SelectedIndex = 0;
        modelLangRow.Controls.Add(_cmbLanguage, 1, 0);

        tlp.Controls.Add(modelLangRow, 1, 3);

        _grpVoice.Controls.Add(tlp);
        return _grpVoice;
    }

    private GroupBox BuildVoiceSettingsGroupBox()
    {
        _grpVoiceSettings = new GroupBox
        {
            Text = "Change voice settings",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 4)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,
            AutoSize = false
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Hàng 0: CheckBox Tùy chỉnh giọng
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Hàng 1: Speed | Pitch
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Hàng 2: Stability | Similarity
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Hàng 3: Style | Speaker Boost
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Hàng 4: Reset

        // Hàng 0: CheckBox Tùy chỉnh giọng (gộp 4 cột)
        _chkCustomVoice = new CheckBox
        {
            Text = "Tùy chỉnh giọng",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Left,
            Margin = new Padding(2, 2, 0, 4)
        };
        _chkCustomVoice.CheckedChanged += ChkCustomVoice_CheckedChanged;
        grid.Controls.Add(_chkCustomVoice, 0, 0);
        grid.SetColumnSpan(_chkCustomVoice, 4);

        // Hàng 1: Speed [1.00] | Pitch (Hz): [0]
        _lblSpeed = MakeLabel("Speed:");
        grid.Controls.Add(_lblSpeed, 0, 1);
        _nudSpeed = MakeNud(0.5m, 2.0m, 0.05m, 1.0m);
        _nudSpeed.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _nudSpeed.Margin = new Padding(2, 2, 8, 2);
        grid.Controls.Add(_nudSpeed, 1, 1);

        _lblPitch = MakeLabel("Pitch (Hz):");
        grid.Controls.Add(_lblPitch, 2, 1);
        _nudPitch = MakeNud(-50, 50, 1, 0);
        _nudPitch.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _nudPitch.Margin = new Padding(2, 2, 2, 2);
        grid.Controls.Add(_nudPitch, 3, 1);

        // Hàng 2: Stability (%): [50] | Similarity (%): [75]
        _lblStability = MakeLabel("Stability (%):");
        grid.Controls.Add(_lblStability, 0, 2);
        _nudStability = MakeNud(0, 100, 1, 50);
        _nudStability.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _nudStability.Margin = new Padding(2, 2, 8, 2);
        grid.Controls.Add(_nudStability, 1, 2);

        _lblSimilarity = MakeLabel("Similarity (%):");
        grid.Controls.Add(_lblSimilarity, 2, 2);
        _nudSimilarity = MakeNud(0, 100, 1, 75);
        _nudSimilarity.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _nudSimilarity.Margin = new Padding(2, 2, 2, 2);
        grid.Controls.Add(_nudSimilarity, 3, 2);

        // Hàng 3: Style (%): [0] | CheckBox Speaker Boost
        _lblStyle = MakeLabel("Style (%):");
        grid.Controls.Add(_lblStyle, 0, 3);
        _nudStyle = MakeNud(0, 100, 1, 0);
        _nudStyle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _nudStyle.Margin = new Padding(2, 2, 8, 2);
        grid.Controls.Add(_nudStyle, 1, 3);

        grid.Controls.Add(new Label { AutoSize = true, Margin = Padding.Empty }, 2, 3);
        _chkSpeakerBoost = new CheckBox
        {
            Text = "Speaker Boost",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Left,
            Enabled = false,
            Margin = new Padding(2, 4, 0, 0)
        };
        grid.Controls.Add(_chkSpeakerBoost, 3, 3);

        // Hàng 4: Nút Reset căn phải gọn gàng
        var pnlReset = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0)
        };
        _btnReset = new Button
        {
            Text = "↻ Reset",
            AutoSize = true,
            Height = 28,
            Margin = new Padding(2, 2, 0, 2),
            Padding = new Padding(8, 0, 8, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnReset.Click += BtnReset_Click;
        pnlReset.Controls.Add(_btnReset);
        grid.Controls.Add(pnlReset, 0, 4);
        grid.SetColumnSpan(pnlReset, 4);

        _grpVoiceSettings.Controls.Add(grid);
        return _grpVoiceSettings;
    }

    private GroupBox BuildProxyGroupBox()
    {
        _grpProxy = new GroupBox
        {
            Text = "Proxy && Cài đặt",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 6, 8, 6)
        };

        var tlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5
        };
        tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        tlp.Controls.Add(new Label
        {
            Text = "Chế độ Proxy:",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 0);

        _cmbProxyMode = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbProxyMode.Items.AddRange(new object[]
        {
            "Không dùng (Direct)",
            "Dùng danh sách Proxy"
        });
        _cmbProxyMode.SelectedIndex = 0;
        tlp.Controls.Add(_cmbProxyMode, 0, 1);

        var proxyRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        proxyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        proxyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        proxyRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _btnLoadProxy = new Button
        {
            Text = "Load Proxy",
            AutoSize = true,
            Height = 28,
            Padding = new Padding(8, 0, 8, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnLoadProxy.Click += BtnLoadProxy_Click;
        proxyRow.Controls.Add(_btnLoadProxy, 0, 0);

        _lblProxyStat = new Label
        {
            Text = "Live: 0 | Die: 0 | Total: 0",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5f),
            Padding = new Padding(6, 0, 0, 0)
        };
        proxyRow.Controls.Add(_lblProxyStat, 1, 0);
        tlp.Controls.Add(proxyRow, 0, 2);

        tlp.Controls.Add(new Label(), 0, 3); // Spacer

        _btnSettings = new Button
        {
            Text = "⚙  Cài đặt API / FFmpeg",
            Dock = DockStyle.Bottom,
            Height = 30,
            Padding = new Padding(8, 0, 8, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnSettings.Click += BtnSettings_Click;
        tlp.Controls.Add(_btnSettings, 0, 4);

        _grpProxy.Controls.Add(tlp);
        return _grpProxy;
    }

    private GroupBox BuildBatchGroupBox()
    {
        _grpBatch = new GroupBox
        {
            Text = "Batch Job",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 4)
        };

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0, 0, 4, 0)
        };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        left.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _txtBatchFolder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = SystemColors.Window };
        left.Controls.Add(_txtBatchFolder, 0, 0);

        _btnBatchChooseFolder = new Button
        {
            Text = "Chọn...",
            AutoSize = true,
            Height = 28,
            Padding = new Padding(8, 0, 8, 0),
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnBatchChooseFolder.Click += BtnBatchChooseFolder_Click;
        left.Controls.Add(_btnBatchChooseFolder, 1, 0);

        var chkTlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        chkTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        chkTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        chkTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chkAutoSrt = new CheckBox
        {
            Text = "Tạo SRT tự động",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Left,
            Margin = new Padding(2, 4, 0, 0)
        };
        _chkAutoJoin = new CheckBox
        {
            Text = "Join MP3 tự động",
            AutoSize = true,
            AutoEllipsis = false,
            Dock = DockStyle.Left,
            Margin = new Padding(2, 4, 0, 0)
        };
        chkTlp.Controls.Add(_chkAutoSrt, 0, 0);
        chkTlp.Controls.Add(_chkAutoJoin, 1, 0);
        left.Controls.Add(chkTlp, 0, 1);
        left.SetColumnSpan(chkTlp, 2);

        _btnRunBatch = new Button
        {
            Text = "▶ Chạy hàng loạt",
            Dock = DockStyle.Fill,
            Height = 30,
            UseCompatibleTextRendering = false,
            UseVisualStyleBackColor = true
        };
        _btnRunBatch.Click += BtnRunBatch_Click;
        left.Controls.Add(_btnRunBatch, 0, 2);
        left.SetColumnSpan(_btnRunBatch, 2);

        split.Controls.Add(left, 0, 0);

        _lstBatchFiles = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8.5f),
            BorderStyle = BorderStyle.FixedSingle
        };
        split.Controls.Add(_lstBatchFiles, 1, 0);

        _grpBatch.Controls.Add(split);
        return _grpBatch;
    }

    private GroupBox BuildOptionsGroupBox()
    {
        _grpOptions = new GroupBox
        {
            Text = "Options",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 4)
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Cột trái: CheckBoxes
        var leftTlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = new Padding(0)
        };
        leftTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        leftTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftTlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _chkAutoRetry = new CheckBox
        {
            Text = "Auto Retry (lỗi mạng)",
            AutoSize = true,
            AutoEllipsis = false,
            Checked = true,
            Dock = DockStyle.Fill
        };
        leftTlp.Controls.Add(_chkAutoRetry, 0, 0);

        _chkAutoSplit = new CheckBox
        {
            Text = "Auto Split câu dài",
            AutoSize = true,
            AutoEllipsis = false,
            Checked = true,
            Dock = DockStyle.Fill
        };
        leftTlp.Controls.Add(_chkAutoSplit, 0, 1);
        root.Controls.Add(leftTlp, 0, 0);

        // Cột phải: Lưới Label AutoSize + NumericUpDown 75-80px
        var rightTlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = new Padding(0)
        };
        rightTlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rightTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        rightTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rightTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        rightTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        rightTlp.Controls.Add(MakeLabel("Số luồng:"), 0, 0);
        _nudThreads = MakeNud(1, 10, 1, 3);
        rightTlp.Controls.Add(_nudThreads, 1, 0);

        rightTlp.Controls.Add(MakeLabel("Max ký tự:"), 0, 1);
        _nudMaxChars = MakeNud(50, 2000, 10, 350);
        rightTlp.Controls.Add(_nudMaxChars, 1, 1);

        rightTlp.Controls.Add(MakeLabel("Nghỉ giữa câu (ms):"), 0, 2);
        _nudSilenceGap = MakeNud(0, 3000, 50, 250);
        rightTlp.Controls.Add(_nudSilenceGap, 1, 2);

        root.Controls.Add(rightTlp, 1, 0);

        _grpOptions.Controls.Add(root);
        return _grpOptions;
    }

    private GroupBox BuildSubtitlesGroupBox()
    {
        _grpSubtitles = new GroupBox
        {
            Text = "Subtitles (Done: 0 | Processing: 0 | Total: 0 | Elapsed: 00:00:00)",
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 6, 6, 6)
        };

        var vLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        vLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        vLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        vLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 4)
        };

        _btnStart        = MakeToolBtn("▶ Start", true);
        _btnStop         = MakeToolBtn("■ Stop", true);
        _btnImportFile   = MakeToolBtn("📂 Import File");
        _btnPasteText    = MakeToolBtn("📋 Dán văn bản");
        _btnImportFolder = MakeToolBtn("📁 Import Folder");
        _btnPreview      = MakeToolBtn("🔊 Nghe thử");
        _btnOpenOutput   = MakeToolBtn("📤 Open Output");
        _btnCreateSrt    = MakeToolBtn("📝 Tạo SRT");
        _btnJoinMp3      = MakeToolBtn("🔗 Join MP3");
        _btnClearList    = MakeToolBtn("🗑 Xóa danh sách");

        _btnStop.Enabled = false;

        _btnStart.Click        += BtnStart_Click;
        _btnStop.Click         += BtnStop_Click;
        _btnImportFile.Click   += BtnImportFile_Click;
        _btnPasteText.Click    += BtnPasteText_Click;
        _btnImportFolder.Click += BtnImportFolder_Click;
        _btnPreview.Click      += BtnPreview_Click;
        _btnOpenOutput.Click   += BtnOpenOutput_Click;
        _btnCreateSrt.Click    += BtnCreateSrt_Click;
        _btnJoinMp3.Click      += BtnJoinMp3_Click;
        _btnClearList.Click    += BtnClearList_Click;

        toolbar.Controls.AddRange(new Control[]
        {
            _btnStart,
            _btnStop,
            _btnImportFile,
            _btnPasteText,
            _btnImportFolder,
            _btnPreview,
            _btnOpenOutput,
            _btnCreateSrt,
            _btnJoinMp3,
            _btnClearList
        });

        vLayout.Controls.Add(toolbar, 0, 0);

        _gridSubtitles = new SmoothDataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            RowHeadersWidth = 24,
            RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
            BorderStyle = BorderStyle.None,
            BackgroundColor = SystemColors.Window,
            GridColor = Color.FromArgb(220, 220, 220),
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing,
            ColumnHeadersHeight = 34,
            RowTemplate = { Height = 28 }
        };
        _gridSubtitles.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            BackColor = Color.FromArgb(243, 244, 246),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(4, 4, 4, 4),
            WrapMode = DataGridViewTriState.False,
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        _gridSubtitles.DefaultCellStyle.Font = new Font("Segoe UI", 9f);
        _gridSubtitles.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 255);

        _gridSubtitles.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "colIndex",    HeaderText = "STT",        Width = 55,  MinimumWidth = 55,  SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "colText",     HeaderText = "Nội dung",   AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "colChars",    HeaderText = "Ký tự",      Width = 70,  MinimumWidth = 70,  SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "colDuration", HeaderText = "Thời lượng", Width = 95,  MinimumWidth = 95,  SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Trạng thái", Width = 140, MinimumWidth = 130, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { Name = "colOutput",   HeaderText = "File Output", Width = 180, MinimumWidth = 180, SortMode = DataGridViewColumnSortMode.NotSortable }
        });

        _gridSubtitles.ShowCellToolTips = true;
        _gridSubtitles.CellFormatting += GridSubtitles_CellFormatting;
        _gridSubtitles.CellDoubleClick += GridSubtitles_CellDoubleClick;
        _gridSubtitles.CellMouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                _gridSubtitles.CurrentCell = _gridSubtitles.Rows[e.RowIndex].Cells[e.ColumnIndex];
            }
        };

        _ctxGrid = new ContextMenuStrip();
        _ctxGrid.Items.Add("🔊 Nghe thử dòng này", null, CtxPreviewRow_Click);
        _ctxGrid.Items.Add("🔄 Tạo lại dòng này", null, CtxRegenRow_Click);
        _ctxGrid.Items.Add("✏ Sửa nội dung", null, CtxEditRow_Click);
        _ctxGrid.Items.Add("🔍 Xem chi tiết lỗi", null, CtxViewError_Click);
        _ctxGrid.Items.Add(new ToolStripSeparator());
        _ctxGrid.Items.Add("🗑 Xóa dòng", null, CtxDeleteRow_Click);
        _gridSubtitles.ContextMenuStrip = _ctxGrid;

        vLayout.Controls.Add(_gridSubtitles, 0, 1);
        _grpSubtitles.Controls.Add(vLayout);
        return _grpSubtitles;
    }

    private void BuildStatusStrip()
    {
        _statusStrip = new StatusStrip { SizingGrip = false };
        _lblStatusEngine = new ToolStripStatusLabel("Engine: Edge-TTS (Miễn phí)") { Spring = false };
        _lblStatusFfmpeg = new ToolStripStatusLabel("FFmpeg: Kiểm tra...") { Spring = false };
        var spacer = new ToolStripStatusLabel { Spring = true };
        _progressBar = new ToolStripProgressBar { Width = 180, Visible = false };
        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _lblStatusEngine,
            new ToolStripSeparator(),
            _lblStatusFfmpeg,
            spacer,
            _progressBar
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoEllipsis = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0, 0, 4, 0)
    };

    private static Label MakeLabelR(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoEllipsis = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Margin = new Padding(0, 0, 4, 0)
    };

    private static NumericUpDown MakeNud(decimal min, decimal max, decimal step, decimal val) => new()
    {
        Minimum = min,
        Maximum = max,
        Increment = step,
        Value = val,
        Dock = DockStyle.Fill,
        DecimalPlaces = step < 1 ? 2 : 0
    };

    private static Button MakeToolBtn(string text, bool isPrimary = false)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 30,
            Margin = new Padding(0, 0, 6, 4),
            Padding = new Padding(8, 2, 8, 2),
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true,
            UseCompatibleTextRendering = false
        };
        if (isPrimary)
        {
            btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        return btn;
    }
}
