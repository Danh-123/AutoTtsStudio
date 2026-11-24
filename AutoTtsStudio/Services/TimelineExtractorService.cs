using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoTtsStudio.Models;
using NAudio.Wave;

namespace AutoTtsStudio.Services;

/// <summary>
/// Dịch vụ trích xuất và tối ưu hóa timeline chính xác cho sản xuất video ngắn/vừa.
/// </summary>
public static class TimelineExtractorService
{
    private static readonly Regex ChatterboxTagRegex = new(
        @"\[(?:laugh|chuckle|sigh|gasp|cough|clear throat|sniff|groan|shush)\]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Xóa sạch các thẻ cảm xúc của Chatterbox và chuẩn hóa khoảng trắng.
    /// </summary>
    public static string CleanEmotionTags(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var clean = ChatterboxTagRegex.Replace(text, "");
        return MultiSpaceRegex.Replace(clean, " ").Trim();
    }

    /// <summary>
    /// Hàm 1: Bóc timeline trực tiếp từ danh sách các dòng âm thanh của dự án hiện tại.
    /// Dùng NAudio.Wave.AudioFileReader lấy thời lượng chính xác tuyệt đối, cộng dồn kiểu double và bù silenceGapMs.
    /// </summary>
    public static List<TimelineEntry> ExtractFromProject(IEnumerable<TtsLineItem> items, int silenceGapMs)
    {
        var result = new List<TimelineEntry>();
        if (items == null) return result;

        var validItems = items
            .Where(i => !string.IsNullOrEmpty(i.OutputPath) && File.Exists(i.OutputPath))
            .OrderBy(i => i.Index)
            .ToList();

        if (validItems.Count == 0) return result;

        double currentTime = 0.0;
        double gapSeconds = Math.Max(0, silenceGapMs) / 1000.0;

        for (int i = 0; i < validItems.Count; i++)
        {
            var item = validItems[i];
            double duration = 0.0;

            try
            {
                using var reader = new AudioFileReader(item.OutputPath);
                duration = reader.TotalTime.TotalSeconds;
            }
            catch
            {
                duration = item.DurationSeconds > 0 ? item.DurationSeconds : 1.0;
            }

            double start = currentTime;
            double end = start + duration;

            result.Add(new TimelineEntry
            {
                Index = i + 1,
                StartSeconds = start,
                EndSeconds = end,
                Text = CleanEmotionTags(item.Text)
            });

            currentTime = end;
            if (i < validItems.Count - 1)
            {
                currentTime += gapSeconds;
            }
        }

        return result;
    }

    /// <summary>
    /// Hàm 2: Bóc timeline từ file audio ngoài (MP3/WAV) bằng script python faster-whisper.
    /// </summary>
    public static async Task<List<TimelineEntry>> ExtractFromAudioFileAsync(
        string audioPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(audioPath))
            throw new FileNotFoundException($"Không tìm thấy file âm thanh tại: {audioPath}");

        var scriptPath = FindScriptPath();
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            throw new FileNotFoundException("Không tìm thấy script 'scripts/extract_timeline.py'.");

        var pythonExe = FindPythonExecutable();
        if (string.IsNullOrEmpty(pythonExe))
        {
            throw new InvalidOperationException(
                "Chưa tìm thấy môi trường Python trên máy tính.\n" +
                "Vui lòng cài đặt Python (3.10 - 3.12) và chạy lệnh: pip install faster-whisper");
        }

        progress?.Report("Đang khởi động Whisper AI (chạy ngầm)...");

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{scriptPath}\" \"{audioPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                progress?.Report(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            throw;
        }

        var stderr = stderrBuilder.ToString();
        if (stderr.Contains("faster-whisper", StringComparison.OrdinalIgnoreCase) &&
            stderr.Contains("pip install", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Chưa cài đặt thư viện 'faster-whisper' trong môi trường Python.\n" +
                "Vui lòng mở Command Prompt / Terminal và chạy lệnh:\n" +
                "pip install faster-whisper");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Tiến trình trích xuất Whisper kết thúc với mã lỗi {process.ExitCode}.\nChi tiết: {stderr.Trim()}");
        }

        var json = stdoutBuilder.ToString().Trim();
        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return new List<TimelineEntry>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Kết quả JSON từ Whisper không đúng định dạng danh sách mốc câu.");

            var list = new List<TimelineEntry>();
            int idx = 1;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                double start = el.GetProperty("start").GetDouble();
                double end = el.GetProperty("end").GetDouble();
                string text = el.GetProperty("text").GetString() ?? string.Empty;

                list.Add(new TimelineEntry
                {
                    Index = idx++,
                    StartSeconds = start,
                    EndSeconds = end,
                    Text = CleanEmotionTags(text)
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lỗi phân tích JSON kết quả Whisper: {ex.Message}\nRaw JSON: {json}", ex);
        }
    }

    /// <summary>
    /// Hàm 3: Gom các câu liên tiếp theo khoảng thời lượng [minSec, maxSec] cho mỗi cảnh video.
    /// Đảm bảo EndSeconds của cảnh trước nối khít StartSeconds của cảnh sau (không hổng giây, không chồng lấn).
    /// </summary>
    public static List<TimelineEntry> GroupEntriesByDuration(
        List<TimelineEntry> source,
        double minSec,
        double maxSec)
    {
        var result = new List<TimelineEntry>();
        if (source == null || source.Count == 0) return result;
        if (minSec <= 0) minSec = 5.0;
        if (maxSec < minSec) maxSec = minSec + 3.0;

        var currentGroup = new List<TimelineEntry>();

        for (int i = 0; i < source.Count; i++)
        {
            var item = source[i];
            currentGroup.Add(item);

            double currentDur = currentGroup[^1].EndSeconds - currentGroup[0].StartSeconds;
            bool isLast = (i == source.Count - 1);
            bool canClose = currentDur >= minSec;

            if (canClose && !isLast)
            {
                var nextItem = source[i + 1];
                double durWithNext = nextItem.EndSeconds - currentGroup[0].StartSeconds;

                if (durWithNext > maxSec || currentDur >= (minSec + maxSec) / 2.0)
                {
                    CreateGroupEntry(currentGroup, result);
                    currentGroup.Clear();
                }
            }
            else if (isLast)
            {
                CreateGroupEntry(currentGroup, result);
                currentGroup.Clear();
            }
        }

        // Đảm bảo nối khít 100% giữa các cảnh kế tiếp: EndSeconds[k] == StartSeconds[k+1]
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].EndSeconds = result[i + 1].StartSeconds;
        }

        // Đánh lại số thứ tự từ 1
        for (int i = 0; i < result.Count; i++)
        {
            result[i].Index = i + 1;
        }

        return result;
    }

    private static void CreateGroupEntry(List<TimelineEntry> group, List<TimelineEntry> result)
    {
        if (group.Count == 0) return;

        double start = (result.Count > 0) ? result[^1].EndSeconds : group[0].StartSeconds;
        double end = group[^1].EndSeconds;
        if (end < start) end = start + 0.1;

        var text = string.Join(" ", group.Select(g => g.Text.Trim()).Where(t => !string.IsNullOrEmpty(t)));

        result.Add(new TimelineEntry
        {
            Index = result.Count + 1,
            StartSeconds = start,
            EndSeconds = end,
            Text = text
        });
    }

    /// <summary>
    /// Hàm 4: Tự động kiểm tra tính toàn vẹn của danh sách timeline.
    /// Trả về true nếu đạt chuẩn 100%, ngược lại ghi rõ lỗi vào out report.
    /// </summary>
    public static bool ValidateTimeline(List<TimelineEntry> entries, out string report)
    {
        if (entries == null || entries.Count == 0)
        {
            report = "Danh sách timeline đang rỗng. Vui lòng nạp dữ liệu trước.";
            return false;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var item = entries[i];

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                report = $"Dòng #{item.Index} ({item.StartTag}): Lời thoại đang bị rỗng.";
                return false;
            }

            if (item.EndSeconds <= item.StartSeconds)
            {
                report = $"Dòng #{item.Index} ({item.StartTag}): Thời lượng không hợp lệ (Bắt đầu: {item.StartSeconds:F2}s, Kết thúc: {item.EndSeconds:F2}s).";
                return false;
            }

            if (i > 0)
            {
                var prev = entries[i - 1];
                if (item.StartSeconds < prev.StartSeconds)
                {
                    report = $"Dòng #{item.Index} bị lùi giờ so với dòng trước (#{prev.Index}: {prev.StartTag} -> #{item.Index}: {item.StartTag}).";
                    return false;
                }

                if (item.StartSeconds < prev.EndSeconds - 0.05)
                {
                    report = $"Dòng #{item.Index} ({item.StartTag}) bị chồng lấn với dòng trước (Dòng trước kết thúc tại {prev.EndTag}).";
                    return false;
                }
            }
        }

        report = "Chuẩn xác 100% - Sẵn sàng xuất";
        return true;
    }

    /// <summary>
    /// Xuất định dạng Timeline văn bản thuần: mỗi dòng [MM:SS] Text.
    /// </summary>
    public static string BuildTimelineTxt(List<TimelineEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine($"{entry.StartTag} {entry.Text}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Xuất file khung prompt tạo ảnh khóa cứng từng cảnh kèm chỉ thị hệ thống cho AI.
    /// </summary>
    public static string BuildLockedImagePromptTemplate(List<TimelineEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ==============================================================================");
        sb.AppendLine("# HƯỚNG DẪN DÀNH CHO AI GENERATION (PROMPT TẠO ẢNH):");
        sb.AppendLine("# - BẮT BUỘC giữ nguyên 100% số thứ tự Cảnh và mốc thời gian [MM:SS].");
        sb.AppendLine("# - TUYỆT ĐỐI KHÔNG được gộp, tách, xóa hoặc bỏ sót bất kỳ cảnh nào trong danh sách.");
        sb.AppendLine("# - Hãy viết Prompt tạo ảnh chi tiết (Midjourney / Stable Diffusion / DALL-E) cho từng cảnh dựa trên nội dung lời thoại tương ứng.");
        sb.AppendLine("# ==============================================================================");
        sb.AppendLine();

        foreach (var entry in entries)
        {
            sb.AppendLine($"# Cảnh {entry.Index:D3} | {entry.StartTag} ({entry.DurationSeconds:F1}s) | Lời thoại: {entry.Text}");
            sb.AppendLine($"Prompt ảnh {entry.Index:D3}: ");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Xuất phụ đề định dạng chuẩn .srt.
    /// </summary>
    public static string BuildSrt(List<TimelineEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine(entry.Index.ToString());
            sb.AppendLine(entry.SrtRange);
            sb.AppendLine(entry.Text);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper tìm đường dẫn file script và Python executable
    // ─────────────────────────────────────────────────────────────────────────

    private static string? FindScriptPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "extract_timeline.py"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "scripts", "extract_timeline.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "scripts", "extract_timeline.py"),
            Path.Combine(Directory.GetCurrentDirectory(), "AutoTtsStudio", "scripts", "extract_timeline.py")
        };

        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    private static string? FindPythonExecutable()
    {
        // 1. Thử gọi nhanh lệnh python trong PATH
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "python",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            proc?.WaitForExit(1500);
            if (proc != null && proc.ExitCode == 0)
                return "python";
        }
        catch { }

        // 2. Thử gọi 'py' (Python Launcher for Windows)
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "py",
                Arguments = "-3 --version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            proc?.WaitForExit(1500);
            if (proc != null && proc.ExitCode == 0)
                return "py";
        }
        catch { }

        // 3. Quét các thư mục cài đặt phổ biến của Python trên Windows
        var commonDirs = new List<string>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var programsDir = Path.Combine(localAppData, "Programs", "Python");
            if (Directory.Exists(programsDir))
            {
                commonDirs.AddRange(Directory.GetDirectories(programsDir, "Python3*"));
            }
        }

        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        try
        {
            commonDirs.AddRange(Directory.GetDirectories(root, "Python3*"));
        }
        catch { }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles) && Directory.Exists(Path.Combine(programFiles, "Python")))
        {
            commonDirs.AddRange(Directory.GetDirectories(Path.Combine(programFiles, "Python"), "Python3*"));
        }

        foreach (var dir in commonDirs.OrderByDescending(d => d))
        {
            var exe = Path.Combine(dir, "python.exe");
            if (File.Exists(exe)) return exe;
        }

        return null;
    }
}
