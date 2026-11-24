using System.ComponentModel;
using System.Diagnostics;
using AutoTtsStudio.Models;
using AutoTtsStudio.Providers;
using AutoTtsStudio.Services;

namespace AutoTtsStudio.Forms;

[DesignerCategory("")]
public sealed partial class MainForm : Form
{
    // ─── State ─────────────────────────────────────────────────────────────────
    private AppConfig _config = ConfigService.Load();
    private readonly BindingList<TtsLineItem> _lines = new();
    private readonly ProxyService _proxyService = new();
    private readonly BatchRunnerService _runnerService = new();
    private readonly AudioPlayerService _audioPlayer = new();
    private readonly System.Windows.Forms.Timer _elapsedTimer = new() { Interval = 500 };
    private readonly Stopwatch _stopwatch = new();
    private const string LoadingVoicesPlaceholder = "Đang tải danh sách giọng...";
    private CancellationTokenSource? _batchCts;
    private CancellationTokenSource? _voiceCts;
    private string _currentJobName = string.Empty;

    // Providers (lazy initialized)
    private EdgeTtsProvider?        _edgeProvider;
    private LocalOpenAiTtsProvider? _localProvider;
    private ElevenLabsProvider?     _elevenProvider;

    public MainForm()
    {
        InitializeComponent();
        BuildCustomLayout();
        WireEvents();
        ApplyConfig();
        UpdateControlsByProvider(loadVoices: true);
        CheckFfmpeg();
        UpdateSubtitleHeader();
        UpdateProxyStatDisplay();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        if (_cmbEngine != null) _cmbEngine.SelectedIndexChanged += (s, e) => UpdateControlsByProvider(loadVoices: true);
        if (_cmbLanguage != null) _cmbLanguage.SelectedIndexChanged += (s, e) => LoadVoicesAsync();
        if (_cmbVoice != null)
        {
            _cmbVoice.SelectedIndexChanged += (s, e) => AutoDetectModelForVoice();
            _cmbVoice.TextChanged          += (s, e) => AutoDetectModelForVoice();
        }
        _elapsedTimer.Tick += ElapsedTimer_Tick;
        FormClosing        += MainForm_FormClosing;

        _audioPlayer.PlaybackStarted += (s, e) =>
        {
            if (InvokeRequired) BeginInvoke(() => _btnPreview.Text = "⏹ Dừng");
            else _btnPreview.Text = "⏹ Dừng";
        };
        _audioPlayer.PlaybackStopped += (s, e) =>
        {
            if (InvokeRequired) BeginInvoke(() => _btnPreview.Text = "🔊 Nghe thử");
            else _btnPreview.Text = "🔊 Nghe thử";
        };

        if (_lblStatusFfmpeg != null)
        {
            _lblStatusFfmpeg.Click += (s, e) =>
            {
                if (!ConfigService.IsFfmpegReady(_config.FfmpegPath))
                {
                    BtnSettings_Click(s, e);
                }
            };
        }
    }

    private void ApplyConfig()
    {
        if (_cmbEngine != null)
        {
            _cmbEngine.SelectedIndexChanged -= (s, e) => UpdateControlsByProvider(loadVoices: true);
            _cmbEngine.SelectedIndex = (int)_config.LastProvider;
            _cmbEngine.SelectedIndexChanged += (s, e) => UpdateControlsByProvider(loadVoices: true);
        }

        if (_chkCustomVoice != null) _chkCustomVoice.Checked = _config.CustomVoiceParams;
        if (_chkAutoRetry != null)   _chkAutoRetry.Checked   = _config.AutoRetry;
        if (_chkAutoSplit != null)   _chkAutoSplit.Checked   = _config.AutoSplit;
        if (_nudMaxChars != null)    _nudMaxChars.Value      = Math.Clamp(_config.AutoSplitMaxChars, 50, 2000);
        if (_nudThreads != null)     _nudThreads.Value       = Math.Clamp(_config.MaxConcurrency, 1, 10);
        if (_nudSilenceGap != null)  _nudSilenceGap.Value    = Math.Clamp(_config.SilenceGapMs, 0, 3000);
        if (_chkAutoSrt != null)     _chkAutoSrt.Checked     = _config.AutoCreateSrt;
        if (_chkAutoJoin != null)    _chkAutoJoin.Checked    = _config.AutoJoinMp3;
    }

    private void CheckFfmpeg()
    {
        var ready = ConfigService.IsFfmpegReady(_config.FfmpegPath);
        _lblStatusFfmpeg.Text      = ready ? "FFmpeg: ✅ Sẵn sàng" : "FFmpeg: ❌ Thiếu ffmpeg.exe (Click để chọn)";
        _lblStatusFfmpeg.ForeColor = ready ? Color.DarkGreen : Color.Crimson;
        _lblStatusFfmpeg.IsLink    = !ready;
    }

    private void UpdateProxyStatDisplay()
    {
        var (live, die, total) = _proxyService.GetCounts();
        _lblProxyStat.Text = $"Live: {live} | Die: {die} | Total: {total}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Core: UpdateControlsByProvider
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateControlsByProvider(bool loadVoices = false)
    {
        var provider = (ProviderType)_cmbEngine.SelectedIndex;
        bool isEdge   = provider == ProviderType.EdgeTts;
        bool isLocal  = provider == ProviderType.LocalOpenAiApi;
        bool isEleven = provider == ProviderType.ElevenLabs;

        // Model & Language combo
        _cmbModel.Enabled = !isEdge;
        _cmbModel.Items.Clear();

        if (isEdge)
        {
            _cmbModel.Items.Add("(N/A — Edge-TTS)");
            _cmbModel.SelectedIndex = 0;
        }
        else if (isLocal)
        {
            _cmbModel.Items.Add("kokoro");
            _cmbModel.Items.Add("tts-1");
            _cmbModel.Items.Add("tts-1-hd");
            _cmbModel.Items.Add("vixt-tts");

            var savedModel = string.IsNullOrWhiteSpace(_config.LocalApiModel) ? "kokoro" : _config.LocalApiModel;
            var idx = _cmbModel.FindStringExact(savedModel);
            _cmbModel.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else // ElevenLabs
        {
            _cmbModel.Items.Add("eleven_multilingual_v2");
            _cmbModel.Items.Add("eleven_turbo_v2_5");
            _cmbModel.Items.Add("eleven_monolingual_v1");
            _cmbModel.SelectedIndex = 0;
        }

        // Voice combo style: Local AI allows free text (voice mixing or Chatterbox clone)
        _cmbVoice.DropDownStyle = isLocal
            ? ComboBoxStyle.DropDown
            : ComboBoxStyle.DropDownList;

        // Thread count suggestion: Local AI sets 1 to protect CPU/VRAM; Edge-TTS suggests 3
        if (isLocal)
        {
            _nudThreads.Value = 1;
        }
        else if (_nudThreads.Value == 1 && !isLocal)
        {
            _nudThreads.Value = Math.Clamp(_config.MaxConcurrency, 1, 10);
        }

        // Parameter labels and visibility
        _lblPitch.Enabled = isEdge;
        _lblPitch.Text    = "Pitch (Hz):";

        _lblStability.Text    = isLocal ? "CFG / Stability (%):" : "Stability (%):";
        _lblStability.Enabled = isLocal || isEleven;

        _lblSimilarity.Enabled = isEleven;

        _lblStyle.Text    = isLocal ? "Exaggeration / Style (%):" : "Style (%):";
        _lblStyle.Enabled = isLocal || isEleven;

        _chkSpeakerBoost.Enabled = isEleven;

        // Status bar engine label
        string engineLabel = provider switch
        {
            ProviderType.LocalOpenAiApi => "Local AI (OpenAI API)",
            ProviderType.ElevenLabs     => "ElevenLabs API",
            _                           => "Edge-TTS (Miễn phí)"
        };
        _lblStatusEngine.Text = $"Engine: {engineLabel}";

        UpdateVoiceParamsEnabled();

        if (loadVoices)
        {
            LoadVoicesAsync();
        }
    }

    private void UpdateVoiceParamsEnabled()
    {
        bool custom   = _chkCustomVoice.Checked;
        var provider  = (ProviderType)_cmbEngine.SelectedIndex;
        bool isEdge   = provider == ProviderType.EdgeTts;
        bool isLocal  = provider == ProviderType.LocalOpenAiApi;
        bool isEleven = provider == ProviderType.ElevenLabs;

        _nudSpeed.Enabled        = custom;
        _nudPitch.Enabled        = custom && isEdge;
        _nudStability.Enabled    = custom && (isLocal || isEleven);
        _nudSimilarity.Enabled   = custom && isEleven;
        _nudStyle.Enabled        = custom && (isLocal || isEleven);
        _chkSpeakerBoost.Enabled = custom && isEleven;
        _btnReset.Enabled        = custom;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Voice Loading
    // ─────────────────────────────────────────────────────────────────────────

    private async void LoadVoicesAsync()
    {
        _voiceCts?.Cancel();
        _voiceCts = new CancellationTokenSource();
        var ct = _voiceCts.Token;

        _cmbVoice.Enabled = false;
        _cmbVoice.Items.Clear();
        _cmbVoice.Items.Add(LoadingVoicesPlaceholder);
        _cmbVoice.SelectedIndex = 0;

        try
        {
            var provider = GetCurrentProvider();
            var langFilter = _cmbLanguage.SelectedItem?.ToString();
            if (langFilter == "All") langFilter = null;

            var voices = await provider.GetVoicesAsync(langFilter, ct);
            if (ct.IsCancellationRequested) return;

            var searchTerm = _txtVoiceSearch.Text.Trim();
            if (!string.IsNullOrEmpty(searchTerm))
            {
                voices = voices.Where(v =>
                    v.DisplayName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    v.Id.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    v.Language.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (_chkOnlyFavorites.Checked && _config.FavoriteVoiceIds.Count > 0)
            {
                voices = voices.Where(v => _config.FavoriteVoiceIds.Contains(v.Id)).ToList();
            }

            _cmbVoice.Items.Clear();
            foreach (var v in voices)
            {
                _cmbVoice.Items.Add(v);
            }

            var providerType = (ProviderType)_cmbEngine.SelectedIndex;
            var isLocal = providerType == ProviderType.LocalOpenAiApi;

            if (isLocal)
            {
                if (!string.IsNullOrWhiteSpace(_config.LastVoiceText) &&
                    !_config.LastVoiceText.Equals(LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase) &&
                    !_config.LastVoiceText.StartsWith("(Lỗi", StringComparison.OrdinalIgnoreCase))
                {
                    var matchIdx = voices.FindIndex(v =>
                        v.Id.Equals(_config.LastVoiceText, StringComparison.OrdinalIgnoreCase) ||
                        v.DisplayName.Equals(_config.LastVoiceText, StringComparison.OrdinalIgnoreCase));

                    if (matchIdx >= 0)
                    {
                        _cmbVoice.SelectedIndex = matchIdx;
                    }
                    else
                    {
                        _cmbVoice.Text = _config.LastVoiceText;
                    }
                }
                else if (voices.Count > 0)
                {
                    _cmbVoice.SelectedIndex = 0;
                }
                else
                {
                    _cmbVoice.SelectedIndex = -1;
                    _cmbVoice.Text = string.Empty;
                }
            }
            else // EdgeTTS or ElevenLabs
            {
                if (!string.IsNullOrEmpty(_config.LastVoiceId))
                {
                    var idx = voices.FindIndex(v => v.Id == _config.LastVoiceId);
                    if (idx >= 0) _cmbVoice.SelectedIndex = idx;
                    else if (voices.Count > 0) _cmbVoice.SelectedIndex = 0;
                    else
                    {
                        _cmbVoice.SelectedIndex = -1;
                        _cmbVoice.Text = string.Empty;
                    }
                }
                else if (voices.Count > 0)
                {
                    _cmbVoice.SelectedIndex = 0;
                }
                else
                {
                    _cmbVoice.SelectedIndex = -1;
                    _cmbVoice.Text = string.Empty;
                }
            }

            AutoDetectModelForVoice();

            // Update status bar with voice search result or count
            if (voices.Count == 0 && !string.IsNullOrEmpty(searchTerm))
            {
                _lblStatusEngine.Text = $"Engine: Không tìm thấy giọng nào khớp với '{searchTerm}'";
            }
            else if (voices.Count > 0)
            {
                var engineLabel = providerType switch
                {
                    ProviderType.LocalOpenAiApi => "Local AI (OpenAI API)",
                    ProviderType.ElevenLabs     => "ElevenLabs API",
                    _                           => "Edge-TTS (Miễn phí)"
                };
                _lblStatusEngine.Text = $"Engine: {engineLabel} ({voices.Count} giọng)";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            _cmbVoice.Items.Clear();
            _cmbVoice.Items.Add($"(Lỗi: {ex.Message.Truncate(60)})");
            _cmbVoice.SelectedIndex = 0;
        }
        finally
        {
            if (_voiceCts?.Token == ct)
            {
                _cmbVoice.Enabled = true;

                // Ensure the placeholder never lingers in Items or Text
                if (_cmbVoice.Items.Count == 1 &&
                    _cmbVoice.Items[0] is string s &&
                    s == LoadingVoicesPlaceholder)
                {
                    _cmbVoice.Items.Clear();
                    _cmbVoice.Text = string.Empty;
                }
                else if (string.Equals(_cmbVoice.Text, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbVoice.Text = string.Empty;
                }
            }
        }
    }

    private void AutoDetectModelForVoice()
    {
        if ((ProviderType)_cmbEngine.SelectedIndex == ProviderType.LocalOpenAiApi)
        {
            var vi = _cmbVoice.SelectedItem as VoiceInfo;
            var voiceId = vi?.Id ?? _cmbVoice.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(voiceId) && (voiceId.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || voiceId.Contains(".wav", StringComparison.OrdinalIgnoreCase)))
            {
                int chatterIdx = _cmbModel.FindStringExact("chatterbox");
                if (chatterIdx >= 0 && _cmbModel.SelectedIndex != chatterIdx)
                {
                    _cmbModel.SelectedIndex = chatterIdx;
                }
            }
            else if (!string.IsNullOrEmpty(voiceId) && (voiceId.StartsWith("am_") || voiceId.StartsWith("af_") || voiceId.StartsWith("bm_") || voiceId.StartsWith("bf_")))
            {
                int kokoroIdx = _cmbModel.FindStringExact("kokoro");
                if (kokoroIdx >= 0 && _cmbModel.SelectedIndex != kokoroIdx)
                {
                    _cmbModel.SelectedIndex = kokoroIdx;
                }
            }
        }
    }

    private ITtsProvider GetCurrentProvider()
    {
        return (ProviderType)_cmbEngine.SelectedIndex switch
        {
            ProviderType.LocalOpenAiApi => _localProvider  ??= new LocalOpenAiTtsProvider(_config),
            ProviderType.ElevenLabs     => _elevenProvider ??= new ElevenLabsProvider(_config),
            _                           => _edgeProvider   ??= new EdgeTtsProvider()
        };
    }

    private string ExtractVoiceIdFromText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        var text = rawText.Trim();

        // 1. If text has format like "Eli (Eli.wav) (en-US)" or "Eli (Eli.wav)", extract "Eli.wav"
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\(([^()]+\.(?:wav|mp3))\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // 2. If text matches a VoiceInfo in the combobox by DisplayName, ToString(), or Id
        var matchedItem = _cmbVoice.Items.OfType<VoiceInfo>().FirstOrDefault(v =>
            string.Equals(v.DisplayName, text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.ToString(), text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v.Id, text, StringComparison.OrdinalIgnoreCase));
        if (matchedItem != null)
        {
            return matchedItem.Id.Trim();
        }

        // 3. Otherwise return the trimmed text (e.g. Kokoro blend "am_adam:0.7+af_bella:0.3" or direct filename)
        return text;
    }

    public VoiceSettings BuildVoiceSettings()
    {
        var provider = (ProviderType)_cmbEngine.SelectedIndex;

        string voiceId;
        // Đảm bảo gửi đúng Voice ID (Eli.wav), không gửi chuỗi hiển thị (Eli (Eli.wav) (en-US)):
        // Nếu cboVoice.SelectedItem là VoiceInfo, BẮT BUỘC lấy voiceInfo.Id, không lấy cboVoice.Text.
        if (_cmbVoice.SelectedItem is VoiceInfo vi && !string.IsNullOrWhiteSpace(vi.Id))
        {
            voiceId = vi.Id.Trim();
        }
        else
        {
            var rawText = _cmbVoice.Text?.Trim() ?? string.Empty;
            if (string.Equals(rawText, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase))
            {
                voiceId = string.Empty;
            }
            else
            {
                voiceId = ExtractVoiceIdFromText(rawText);
            }
        }

        return new VoiceSettings
        {
            Provider     = provider,
            VoiceId      = voiceId,
            Model        = _cmbModel.SelectedItem?.ToString() ?? string.Empty,
            Language     = _cmbLanguage.SelectedItem?.ToString() ?? "en-US",
            Speed        = (double)_nudSpeed.Value,
            Pitch        = (int)_nudPitch.Value,
            Stability    = (int)_nudStability.Value,
            Similarity   = (int)_nudSimilarity.Value,
            Style        = (int)_nudStyle.Value,
            SpeakerBoost = _chkSpeakerBoost.Checked,
            CustomParams = _chkCustomVoice.Checked,
            SilenceGapMs = (int)_nudSilenceGap.Value
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DataGridView binding & formatting
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshGrid()
    {
        _gridSubtitles.SuspendLayout();
        _gridSubtitles.Rows.Clear();
        foreach (var item in _lines)
        {
            int row = _gridSubtitles.Rows.Add(
                item.Index,
                item.Text,
                item.CharCount,
                item.DurationDisplay,
                item.StatusDisplay,
                item.OutputPath);
            _gridSubtitles.Rows[row].Tag = item;
            if (!string.IsNullOrEmpty(item.ErrorMessage))
            {
                _gridSubtitles.Rows[row].Cells["colStatus"].ToolTipText = item.ErrorMessage;
            }
        }
        _gridSubtitles.ResumeLayout();
        UpdateSubtitleHeader();
    }

    private void UpdateGridRow(TtsLineItem item)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateGridRow(item)));
            return;
        }

        int rowIndex = item.Index - 1;
        if (rowIndex >= 0 && rowIndex < _gridSubtitles.Rows.Count)
        {
            var row = _gridSubtitles.Rows[rowIndex];
            row.Cells["colDuration"].Value = item.DurationDisplay;
            row.Cells["colStatus"].Value   = item.StatusDisplay;
            row.Cells["colStatus"].ToolTipText = !string.IsNullOrEmpty(item.ErrorMessage) ? item.ErrorMessage : string.Empty;
            row.Cells["colOutput"].Value   = item.OutputPath;
            _gridSubtitles.InvalidateRow(rowIndex);
        }
        UpdateSubtitleHeader();
        UpdateProxyStatDisplay();
    }

    private void GridSubtitles_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _gridSubtitles.Rows.Count) return;
        if (_gridSubtitles.Rows[e.RowIndex].Tag is not TtsLineItem item) return;

        // Color "Trạng thái" column
        if (_gridSubtitles.Columns[e.ColumnIndex].Name == "colStatus")
        {
            e.CellStyle.ForeColor = item.Status switch
            {
                TtsStatus.Done       => Color.DarkGreen,
                TtsStatus.Processing => Color.DodgerBlue,
                TtsStatus.Error      => Color.Crimson,
                _                    => SystemColors.ControlText
            };
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            _gridSubtitles.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText =
                (item.Status == TtsStatus.Error && !string.IsNullOrEmpty(item.ErrorMessage))
                    ? item.ErrorMessage
                    : string.Empty;
        }
    }

    private void GridSubtitles_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _lines.Count) return;
        var item = _lines[e.RowIndex];

        if (item.Status == TtsStatus.Done && !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath))
        {
            try
            {
                _audioPlayer.PlayOrToggle(item.OutputPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phát âm thanh: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            EditRowAt(e.RowIndex);
        }
    }

    private void UpdateSubtitleHeader()
    {
        var done       = _lines.Count(l => l.Status == TtsStatus.Done);
        var processing = _lines.Count(l => l.Status == TtsStatus.Processing);
        _grpSubtitles.Text =
            $"Subtitles (Done: {done} | Processing: {processing} | Total: {_lines.Count} | Elapsed: {_stopwatch.Elapsed:hh\\:mm\\:ss})";
    }

    private void ElapsedTimer_Tick(object? sender, EventArgs e) => UpdateSubtitleHeader();

    // ─────────────────────────────────────────────────────────────────────────
    // Execution: Start / Stop
    // ─────────────────────────────────────────────────────────────────────────

    private bool ValidateProviderSettings()
    {
        var providerType = (ProviderType)_cmbEngine.SelectedIndex;
        if (providerType == ProviderType.LocalOpenAiApi)
        {
            if (string.IsNullOrWhiteSpace(_config.LocalApiUrl))
            {
                MessageBox.Show("Chưa cấu hình địa chỉ Local API URL.\nVui lòng bấm '⚙ Cài đặt API / FFmpeg' để nhập URL (ví dụ: http://localhost:8880/v1/audio/speech).",
                    "Thiếu cấu hình Local AI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
        else if (providerType == ProviderType.ElevenLabs)
        {
            if (string.IsNullOrWhiteSpace(_config.ElevenLabsApiKey))
            {
                MessageBox.Show("Chưa cấu hình API Key của ElevenLabs.\nVui lòng bấm '⚙ Cài đặt API / FFmpeg' để nhập ElevenLabs API Key.",
                    "Thiếu ElevenLabs API Key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        var voiceText = _cmbVoice.Text?.Trim();
        var voiceItem = _cmbVoice.SelectedItem as VoiceInfo;

        // Block if voice is still loading
        if (string.Equals(voiceText, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(voiceItem?.DisplayName, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Danh sách giọng đang được tải. Vui lòng đợi trong giây lát...",
                "Đang tải giọng", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        // Block if voice text is an error string
        if (voiceText != null && (voiceText.StartsWith("(Lỗi", StringComparison.OrdinalIgnoreCase) ||
                                  voiceText.StartsWith("(Error", StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("Chưa chọn được giọng đọc hợp lệ. Vui lòng kiểm tra lại kết nối hoặc chọn giọng khác.",
                "Giọng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // For Local AI or any provider, if no item selected and no text typed, block
        if (voiceItem == null && string.IsNullOrEmpty(voiceText))
        {
            MessageBox.Show("Vui lòng chọn hoặc nhập giọng đọc (Voice) trước khi bắt đầu.",
                "Chưa chọn giọng đọc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (_lines.Count == 0)
        {
            MessageBox.Show("Danh sách phụ đề đang rỗng. Hãy bấm 'Import File', 'Dán văn bản' hoặc 'Import Folder' trước.",
                "Chưa có kịch bản", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidateProviderSettings()) return;

        if (_runnerService.IsRunning) return;

        _audioPlayer.StopAndDispose();
        SetExecutionUiState(running: true);

        var provider = GetCurrentProvider();
        var settings = BuildVoiceSettings();
        bool useProxy = _cmbProxyMode.SelectedIndex == 1;

        var jobName = string.IsNullOrEmpty(_currentJobName)
            ? DateTime.Now.ToString("yyyyMMdd_HHmmss")
            : _currentJobName;
        var outputFolder = Path.Combine(ConfigService.BaseDir, "Output", jobName);
        _config.LastOutputDir = outputFolder;

        var itemProgress = new Progress<TtsLineItem>(UpdateGridRow);
        var overallProgress = new Progress<(int Done, int Total)>(progress =>
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateProgressBar(progress.Done, progress.Total)));
            }
            else
            {
                UpdateProgressBar(progress.Done, progress.Total);
            }
        });

        _batchCts = new CancellationTokenSource();
        _stopwatch.Restart();
        _elapsedTimer.Start();

        try
        {
            await _runnerService.RunBatchAsync(
                _lines,
                provider,
                settings,
                _proxyService,
                useProxy,
                outputFolder,
                (int)_nudThreads.Value,
                _chkAutoRetry.Checked,
                itemProgress,
                overallProgress,
                _batchCts.Token);

            UpdateSubtitleHeader();

            if (!_batchCts.IsCancellationRequested)
            {
                var doneCount = _lines.Count(l => l.Status == TtsStatus.Done);

                // Auto Create SRT
                if (_chkAutoSrt.Checked && doneCount > 0)
                {
                    try
                    {
                        var srtPath = Path.Combine(outputFolder, $"{jobName}_full.srt");
                        await AudioPostProcessService.CreateSrtAsync(_lines, (int)_nudSilenceGap.Value, srtPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AutoSrt] Lỗi tạo SRT: {ex.Message}");
                    }
                }

                // Auto Join MP3
                if (_chkAutoJoin.Checked && doneCount > 0 && ConfigService.IsFfmpegReady(_config.FfmpegPath))
                {
                    try
                    {
                        var mp3Path = Path.Combine(outputFolder, $"{jobName}_full.mp3");
                        await AudioPostProcessService.JoinAudioAsync(_lines, (int)_nudSilenceGap.Value, mp3Path, _config.FfmpegPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AutoJoin] Lỗi nối MP3: {ex.Message}");
                    }
                }

                var errorCount = _lines.Count(l => l.Status == TtsStatus.Error);
                if (errorCount == 0)
                {
                    MessageBox.Show($"Hoàn thành xuất sắc toàn bộ {_lines.Count} dòng âm thanh!\nThư mục: {outputFolder}",
                        "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Đã chạy xong với {errorCount} dòng bị lỗi. Bạn có thể bấm chuột phải chọn 'Tạo lại dòng này' để chạy lại.",
                        "Kết thúc", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình chạy: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _elapsedTimer.Stop();
            _stopwatch.Stop();
            SetExecutionUiState(running: false);
            UpdateSubtitleHeader();
        }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _runnerService.Stop();
        _batchCts?.Cancel();
        SetExecutionUiState(running: false);
    }

    private void SetExecutionUiState(bool running)
    {
        _btnStart.Enabled = !running;
        _btnStop.Enabled  = running;
        _btnImportFile.Enabled = !running;
        _btnPasteText.Enabled = !running;
        _btnImportFolder.Enabled = !running;
        _btnClearList.Enabled = !running;
        _btnRunBatch.Enabled = !running;

        _progressBar.Visible = running;
        if (!running)
        {
            _progressBar.Value = 0;
        }
    }

    private void UpdateProgressBar(int done, int total)
    {
        if (total > 0)
        {
            _progressBar.Maximum = total;
            _progressBar.Value = Math.Clamp(done, 0, total);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Batch Job: Folder execution
    // ─────────────────────────────────────────────────────────────────────────

    private async void BtnRunBatch_Click(object? sender, EventArgs e)
    {
        if (_lstBatchFiles.Items.Count == 0)
        {
            MessageBox.Show("Chưa chọn thư mục hoặc thư mục không chứa file .txt/.srt nào.",
                "Chưa chọn thư mục", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var folder = _txtBatchFolder.Text;
        if (!Directory.Exists(folder))
        {
            MessageBox.Show("Thư mục đã chọn không tồn tại trên ổ đĩa.", "Thư mục không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!ValidateProviderSettings()) return;

        var files = _lstBatchFiles.Items.Cast<string>().ToList();
        SetExecutionUiState(running: true);

        var provider = GetCurrentProvider();
        var settings = BuildVoiceSettings();
        bool useProxy = _cmbProxyMode.SelectedIndex == 1;

        _batchCts = new CancellationTokenSource();
        _stopwatch.Restart();
        _elapsedTimer.Start();

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (_batchCts.IsCancellationRequested) break;

                _lstBatchFiles.SelectedIndex = i;
                var fileName = files[i];
                var fullPath = Path.Combine(folder, fileName);

                _currentJobName = Path.GetFileNameWithoutExtension(fileName);
                var outputFolder = Path.Combine(ConfigService.BaseDir, "Output", _currentJobName);
                _config.LastOutputDir = outputFolder;

                // Import file into lines
                _lines.Clear();
                int startIndex = 1;
                var imported = TextImportService.ImportFile(fullPath, _chkAutoSplit.Checked, (int)_nudMaxChars.Value, ref startIndex);
                foreach (var it in imported)
                {
                    _lines.Add(it);
                }
                RefreshGrid();

                var itemProgress = new Progress<TtsLineItem>(UpdateGridRow);
                var overallProgress = new Progress<(int Done, int Total)>(p =>
                {
                    if (InvokeRequired)
                    {
                        BeginInvoke(new Action(() => UpdateProgressBar(p.Done, p.Total)));
                    }
                    else
                    {
                        UpdateProgressBar(p.Done, p.Total);
                    }
                });

                await _runnerService.RunBatchAsync(
                    _lines,
                    provider,
                    settings,
                    _proxyService,
                    useProxy,
                    outputFolder,
                    (int)_nudThreads.Value,
                    _chkAutoRetry.Checked,
                    itemProgress,
                    overallProgress,
                    _batchCts.Token);

                // Post-processing hook (SRT / Join MP3 to be implemented in Phase 4)
                OnBatchFileCompleted(fullPath, _lines.ToList(), outputFolder);
            }

            if (!_batchCts.IsCancellationRequested)
            {
                MessageBox.Show($"Đã chạy xong toàn bộ {files.Count} file trong Batch Job!",
                    "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình chạy Batch: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _elapsedTimer.Stop();
            _stopwatch.Stop();
            SetExecutionUiState(running: false);
            UpdateSubtitleHeader();
        }
    }

    private async void OnBatchFileCompleted(string filePath, List<TtsLineItem> items, string outputFolder)
    {
        var jobName = Path.GetFileNameWithoutExtension(filePath);
        int silenceGap = (int)_nudSilenceGap.Value;

        if (_chkAutoSrt.Checked)
        {
            try
            {
                var srtPath = Path.Combine(outputFolder, $"{jobName}_full.srt");
                await AudioPostProcessService.CreateSrtAsync(items, silenceGap, srtPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Batch Hook] Lỗi tạo SRT cho {jobName}: {ex.Message}");
            }
        }

        if (_chkAutoJoin.Checked && ConfigService.IsFfmpegReady(_config.FfmpegPath))
        {
            try
            {
                var mp3Path = Path.Combine(outputFolder, $"{jobName}_full.mp3");
                await AudioPostProcessService.JoinAudioAsync(items, silenceGap, mp3Path, _config.FfmpegPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Batch Hook] Lỗi Join MP3 cho {jobName}: {ex.Message}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button handlers — Tầng 1
    // ─────────────────────────────────────────────────────────────────────────

    private void VoiceSearch_Click(object? sender, EventArgs e) => LoadVoicesAsync();

    private void BtnFavoriteVoice_Click(object? sender, EventArgs e)
    {
        string? id;
        if (_cmbVoice.SelectedItem is VoiceInfo vi && !string.IsNullOrWhiteSpace(vi.Id))
            id = vi.Id.Trim();
        else
            id = ExtractVoiceIdFromText(_cmbVoice.Text?.Trim() ?? string.Empty);

        if (string.IsNullOrEmpty(id) ||
            string.Equals(id, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("(Lỗi", StringComparison.OrdinalIgnoreCase)) return;

        if (!_config.FavoriteVoiceIds.Contains(id))
        {
            _config.FavoriteVoiceIds.Add(id);
            ConfigService.Save(_config);
            MessageBox.Show($"Đã lưu giọng '{id}' vào danh sách yêu thích.", "Thành công",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show($"Giọng '{id}' đã có trong danh sách yêu thích.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ChkOnlyFavorites_CheckedChanged(object? sender, EventArgs e) => LoadVoicesAsync();

    private void BtnReset_Click(object? sender, EventArgs e)
    {
        _nudSpeed.Value      = 1.0m;
        _nudPitch.Value      = 0;
        _nudStability.Value  = 50;
        _nudSimilarity.Value = 75;
        _nudStyle.Value      = 0;
        _chkSpeakerBoost.Checked = false;
    }

    private void BtnLoadProxy_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Chọn file proxy (.txt)",
            Filter = "Text files|*.txt|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _proxyService.LoadFromFile(dlg.FileName);
        UpdateProxyStatDisplay();
        MessageBox.Show($"Đã nạp thành công {_proxyService.Proxies.Count} proxy.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BtnSettings_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsDialog(_config);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            ConfigService.Save(_config);
            _edgeProvider   = null;
            _localProvider  = null;
            _elevenProvider = null;
            CheckFfmpeg();

            if ((ProviderType)_cmbEngine.SelectedIndex == ProviderType.LocalOpenAiApi)
            {
                LoadVoicesAsync();
            }
        }
    }

    private void ChkCustomVoice_CheckedChanged(object? sender, EventArgs e)
    {
        _config.CustomVoiceParams = _chkCustomVoice.Checked;
        UpdateVoiceParamsEnabled();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button handlers — Tầng 2 & Import
    // ─────────────────────────────────────────────────────────────────────────

    private void BtnBatchChooseFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Chọn thư mục chứa file .txt/.srt" };
        if (!string.IsNullOrEmpty(_txtBatchFolder.Text) && Directory.Exists(_txtBatchFolder.Text))
            dlg.InitialDirectory = _txtBatchFolder.Text;

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        LoadBatchFolder(dlg.SelectedPath);
    }

    private void BtnImportFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Chọn thư mục chứa file .txt/.srt để import" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        LoadBatchFolder(dlg.SelectedPath);
    }

    private void LoadBatchFolder(string folderPath)
    {
        _txtBatchFolder.Text = folderPath;
        _lstBatchFiles.Items.Clear();

        var files = Directory.GetFiles(folderPath, "*.*")
            .Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToArray();

        foreach (var f in files)
        {
            _lstBatchFiles.Items.Add(Path.GetFileName(f));
        }

        // Auto load first file into the preview grid
        if (files.Length > 0)
        {
            _lstBatchFiles.SelectedIndex = 0;
            var firstFile = files[0];
            _currentJobName = Path.GetFileNameWithoutExtension(firstFile);

            _lines.Clear();
            int startIndex = 1;
            var imported = TextImportService.ImportFile(firstFile, _chkAutoSplit.Checked, (int)_nudMaxChars.Value, ref startIndex);
            foreach (var item in imported)
            {
                _lines.Add(item);
            }
            RefreshGrid();
        }
        else
        {
            _lines.Clear();
            RefreshGrid();
        }
    }

    private void BtnImportFile_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title       = "Import file kịch bản",
            Filter      = "Phụ đề & Văn bản (*.srt;*.txt)|*.srt;*.txt|SRT files (*.srt)|*.srt|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        int startIndex = _lines.Count + 1;
        if (dlg.FileNames.Length == 1)
        {
            _currentJobName = Path.GetFileNameWithoutExtension(dlg.FileNames[0]);
        }

        foreach (var file in dlg.FileNames)
        {
            var imported = TextImportService.ImportFile(file, _chkAutoSplit.Checked, (int)_nudMaxChars.Value, ref startIndex);
            foreach (var item in imported)
            {
                _lines.Add(item);
            }
        }
        RefreshGrid();
        UpdateSubtitleHeader();
    }

    private void BtnPasteText_Click(object? sender, EventArgs e)
    {
        using var dlg = new PasteTextDialog(_chkAutoSplit.Checked, (int)_nudMaxChars.Value);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var text = dlg.PastedText;
        if (string.IsNullOrWhiteSpace(text)) return;

        if (string.IsNullOrEmpty(_currentJobName))
        {
            _currentJobName = $"Script_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        int startIndex = _lines.Count + 1;
        var items = TextImportService.ImportRawText(text, dlg.AutoSplit, dlg.MaxChars, ref startIndex);
        if (items.Count == 0)
        {
            MessageBox.Show("Không tìm thấy nội dung hợp lệ nào trong văn bản vừa dán.",
                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        foreach (var item in items)
        {
            _lines.Add(item);
        }
        RefreshGrid();
        UpdateSubtitleHeader();
    }

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        if (_audioPlayer.IsPlaying)
        {
            _audioPlayer.StopAndDispose();
            return;
        }

        if (_lines.Count == 0)
        {
            MessageBox.Show("Danh sách phụ đề đang rỗng. Vui lòng nạp kịch bản và tạo âm thanh trước.",
                "Chưa có kịch bản", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx < 0 || rowIdx >= _lines.Count)
        {
            MessageBox.Show("Vui lòng chọn một dòng đã hoàn thành trên bảng để nghe thử.",
                "Chưa chọn dòng", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var item = _lines[rowIdx];
        if (string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            MessageBox.Show("Dòng này chưa có file âm thanh đầu ra. Hãy bấm '▶ Start' hoặc chuột phải chọn 'Tạo lại dòng này' trước.",
                "Chưa có audio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _audioPlayer.PlayOrToggle(item.OutputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi phát âm thanh: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnOpenOutput_Click(object? sender, EventArgs e)
    {
        string dir;
        if (!string.IsNullOrEmpty(_currentJobName))
        {
            var jobDir = Path.Combine(ConfigService.BaseDir, "Output", _currentJobName);
            dir = Directory.Exists(jobDir) ? jobDir : Path.Combine(ConfigService.BaseDir, "Output");
        }
        else if (!string.IsNullOrEmpty(_config.LastOutputDir) && Directory.Exists(_config.LastOutputDir))
        {
            dir = _config.LastOutputDir;
        }
        else
        {
            dir = Path.Combine(ConfigService.BaseDir, "Output");
        }

        Directory.CreateDirectory(dir);
        Process.Start("explorer.exe", dir);
    }

    private async void BtnCreateSrt_Click(object? sender, EventArgs e)
    {
        if (_lines.Count == 0)
        {
            MessageBox.Show("Danh sách phụ đề đang rỗng. Vui lòng nạp kịch bản và chạy tạo âm thanh trước.",
                "Chưa có kịch bản", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var validCount = _lines.Count(x => x.Status == TtsStatus.Done && !string.IsNullOrEmpty(x.OutputPath) && File.Exists(x.OutputPath));
        if (validCount == 0)
        {
            MessageBox.Show("Chưa có dòng nào hoàn thành (Done) có file âm thanh tồn tại. Vui lòng bấm '▶ Start' để tạo âm thanh trước khi tạo file SRT.",
                "Chưa có file âm thanh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var jobName = string.IsNullOrEmpty(_currentJobName) ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : _currentJobName;
        var outputDir = Path.Combine(ConfigService.BaseDir, "Output", jobName);
        var srtPath = Path.Combine(outputDir, $"{jobName}_full.srt");

        try
        {
            await AudioPostProcessService.CreateSrtAsync(_lines, (int)_nudSilenceGap.Value, srtPath);
            var res = MessageBox.Show(
                $"Đã tạo thành công file SRT với {validCount} câu phụ đề!\n\nFile: {srtPath}\n\nBạn có muốn mở thư mục chứa file không?",
                "Tạo SRT hoàn thành", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (res == DialogResult.Yes)
            {
                Process.Start("explorer.exe", $"/select,\"{srtPath}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tạo SRT: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExtractTimeline_Click(object? sender, EventArgs e)
    {
        using var form = new TimelineBuilderForm(_lines, (int)_nudSilenceGap.Value);
        form.ShowDialog(this);
    }

    private void BtnBatchImage_Click(object? sender, EventArgs e)
    {
        using var form = new BatchImageForm();
        form.ShowDialog(this);
    }

    private async void BtnJoinMp3_Click(object? sender, EventArgs e)
    {
        if (_lines.Count == 0)
        {
            MessageBox.Show("Danh sách phụ đề đang rỗng. Vui lòng nạp kịch bản và chạy tạo âm thanh trước.",
                "Chưa có kịch bản", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var validCount = _lines.Count(x => x.Status == TtsStatus.Done && !string.IsNullOrEmpty(x.OutputPath) && File.Exists(x.OutputPath));
        if (validCount == 0)
        {
            MessageBox.Show("Chưa có dòng nào hoàn thành (Done) có file âm thanh tồn tại. Vui lòng bấm '▶ Start' để tạo âm thanh trước khi nối.",
                "Chưa có file âm thanh", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ConfigService.IsFfmpegReady(_config.FfmpegPath))
        {
            var choice = MessageBox.Show(
                "Không tìm thấy ffmpeg.exe trên máy.\nBạn có muốn mở Cài đặt để thiết lập đường dẫn FFmpeg không?",
                "Thiếu FFmpeg", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (choice == DialogResult.Yes)
            {
                BtnSettings_Click(sender, e);
            }
            return;
        }

        _audioPlayer.StopAndDispose();

        var jobName = string.IsNullOrEmpty(_currentJobName) ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : _currentJobName;
        var outputDir = Path.Combine(ConfigService.BaseDir, "Output", jobName);
        var outputMp3 = Path.Combine(outputDir, $"{jobName}_full.mp3");

        var oldText = _btnJoinMp3.Text;
        _btnJoinMp3.Enabled = false;
        _btnJoinMp3.Text = "⏳ Đang nối...";

        var progress = new Progress<string>(msg =>
        {
            if (InvokeRequired) BeginInvoke(() => _lblStatusEngine.Text = msg);
            else _lblStatusEngine.Text = msg;
        });

        try
        {
            using var cts = new CancellationTokenSource();
            await AudioPostProcessService.JoinAudioAsync(_lines, (int)_nudSilenceGap.Value, outputMp3, _config.FfmpegPath, progress, cts.Token);

            var res = MessageBox.Show(
                $"Nối thành công {validCount} đoạn audio thành 1 file MP3 chất lượng cao!\n\nFile: {outputMp3}\n\nBạn có muốn mở thư mục chứa file không?",
                "Nối MP3 hoàn thành", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (res == DialogResult.Yes)
            {
                Process.Start("explorer.exe", $"/select,\"{outputMp3}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nối audio: {ex.Message}", "Lỗi FFmpeg", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnJoinMp3.Enabled = true;
            _btnJoinMp3.Text = oldText;
            UpdateControlsByProvider(loadVoices: false);
        }
    }

    private void BtnClearList_Click(object? sender, EventArgs e)
    {
        if (_lines.Count == 0) return;
        if (MessageBox.Show($"Xóa toàn bộ {_lines.Count} dòng khỏi bảng?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _lines.Clear();
            _gridSubtitles.Rows.Clear();
            UpdateSubtitleHeader();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Context menu handlers
    // ─────────────────────────────────────────────────────────────────────────

    private async void CtxRegenRow_Click(object? sender, EventArgs e)
    {
        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx < 0 || rowIdx >= _lines.Count) return;
        if (_runnerService.IsRunning)
        {
            MessageBox.Show("Tiến trình TTS đang chạy. Vui lòng dừng hoặc đợi hoàn thành.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidateProviderSettings()) return;

        _audioPlayer.StopAndDispose();

        var item = _lines[rowIdx];
        var provider = GetCurrentProvider();
        var settings = BuildVoiceSettings();
        bool useProxy = _cmbProxyMode.SelectedIndex == 1;

        var jobName = string.IsNullOrEmpty(_currentJobName) ? DateTime.Now.ToString("yyyyMMdd_HHmmss") : _currentJobName;
        var outputFolder = Path.Combine(ConfigService.BaseDir, "Output", jobName);

        var progress = new Progress<TtsLineItem>(UpdateGridRow);

        try
        {
            using var cts = new CancellationTokenSource();
            await _runnerService.RunSingleItemAsync(
                item,
                provider,
                settings,
                _proxyService,
                useProxy,
                outputFolder,
                _chkAutoRetry.Checked,
                progress,
                cts.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tạo lại dòng {item.Index}: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UpdateSubtitleHeader();
        }
    }

    private void CtxPreviewRow_Click(object? sender, EventArgs e)
    {
        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx < 0 || rowIdx >= _lines.Count) return;
        var item = _lines[rowIdx];

        if (string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            MessageBox.Show("Dòng này chưa có file âm thanh đầu ra.", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _audioPlayer.PlayOrToggle(item.OutputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi phát âm thanh: {ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CtxEditRow_Click(object? sender, EventArgs e)
    {
        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx >= 0) EditRowAt(rowIdx);
    }

    private void CtxViewError_Click(object? sender, EventArgs e)
    {
        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx < 0 || rowIdx >= _lines.Count) return;
        var item = _lines[rowIdx];

        if (string.IsNullOrWhiteSpace(item.ErrorMessage))
        {
            MessageBox.Show($"Dòng #{item.Index} hiện không có thông báo lỗi (Trạng thái: {item.StatusDisplay}).",
                "Chi tiết lỗi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show($"Chi tiết lỗi dòng #{item.Index}:\n\n{item.ErrorMessage}",
            "Chi tiết lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void CtxDeleteRow_Click(object? sender, EventArgs e)
    {
        int rowIdx = _gridSubtitles.CurrentCell?.RowIndex ?? -1;
        if (rowIdx < 0 || rowIdx >= _lines.Count) return;
        var item = _lines[rowIdx];

        if (MessageBox.Show($"Xóa dòng {item.Index}?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _lines.RemoveAt(rowIdx);
            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].Index = i + 1;
            }
            RefreshGrid();
        }
    }

    private void EditRowAt(int rowIdx)
    {
        if (rowIdx < 0 || rowIdx >= _lines.Count) return;
        var item = _lines[rowIdx];
        using var dlg = new EditLineDialog(item.Text);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            item.Text = dlg.EditedText;
            item.Status = TtsStatus.Pending;
            item.OutputPath = string.Empty;
            item.DurationSeconds = 0;
            RefreshGrid();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Form lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            _voiceCts?.Cancel();
            _voiceCts?.Dispose();
            _voiceCts = null;

            _batchCts?.Cancel();
            _batchCts?.Dispose();
            _batchCts = null;

            _runnerService.Stop();
            _audioPlayer.StopAndDispose();
            _audioPlayer.Dispose();

            AudioPostProcessService.KillActiveProcesses();

            _config.LastProvider      = (ProviderType)_cmbEngine.SelectedIndex;
            _config.AutoRetry         = _chkAutoRetry.Checked;
            _config.AutoSplit         = _chkAutoSplit.Checked;
            _config.AutoSplitMaxChars = (int)_nudMaxChars.Value;
            _config.MaxConcurrency    = (int)_nudThreads.Value;
            _config.SilenceGapMs      = (int)_nudSilenceGap.Value;
            _config.AutoCreateSrt     = _chkAutoSrt.Checked;
            _config.AutoJoinMp3       = _chkAutoJoin.Checked;
            _config.CustomVoiceParams = _chkCustomVoice.Checked;

            if ((ProviderType)_cmbEngine.SelectedIndex == ProviderType.LocalOpenAiApi)
            {
                string txt;
                if (_cmbVoice.SelectedItem is VoiceInfo vi && !string.IsNullOrWhiteSpace(vi.Id))
                    txt = vi.Id.Trim();
                else
                    txt = ExtractVoiceIdFromText(_cmbVoice.Text?.Trim() ?? string.Empty);

                if (!string.Equals(txt, LoadingVoicesPlaceholder, StringComparison.OrdinalIgnoreCase) &&
                    !txt.StartsWith("(Lỗi", StringComparison.OrdinalIgnoreCase))
                {
                    _config.LastVoiceText = txt;
                }
                _config.LocalApiModel = _cmbModel.SelectedItem?.ToString() ?? "kokoro";
            }
            else
            {
                _config.LastVoiceId = (_cmbVoice.SelectedItem as VoiceInfo)?.Id ?? string.Empty;
            }

            ConfigService.Save(_config);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FormClosing Cleanup Error]: {ex.Message}");
        }
    }
}
