using System.Diagnostics;
using System.Text;
using AutoTtsStudio.Models;
using NAudio.Wave;

namespace AutoTtsStudio.Services;

/// <summary>
/// Service for audio post-processing: FFmpeg concatenation into full MP3
/// and timeline-synchronized SRT subtitle generation.
/// </summary>
public static class AudioPostProcessService
{
    /// <summary>
    /// Generates a timeline-synchronized .srt subtitle file matching the joined audio.
    /// Uses NAudio to obtain accurate audio durations.
    /// </summary>
    public static async Task<string> CreateSrtAsync(
        IEnumerable<TtsLineItem> items,
        int silenceGapMs,
        string outputSrtPath,
        CancellationToken ct = default)
    {
        var validItems = items
            .Where(x => x.Status == TtsStatus.Done && !string.IsNullOrEmpty(x.OutputPath) && File.Exists(x.OutputPath))
            .OrderBy(x => x.Index)
            .ToList();

        if (validItems.Count == 0)
        {
            throw new InvalidOperationException("Không có dòng audio hợp lệ nào để tạo SRT.");
        }

        var dir = Path.GetDirectoryName(outputSrtPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var sb = new StringBuilder();
        var currentStart = TimeSpan.Zero;
        var silenceGap = TimeSpan.FromMilliseconds(Math.Max(0, silenceGapMs));
        int subIndex = 1;

        await Task.Run(() =>
        {
            foreach (var item in validItems)
            {
                ct.ThrowIfCancellationRequested();

                // Obtain accurate duration
                TimeSpan duration;
                if (item.DurationSeconds > 0)
                {
                    duration = TimeSpan.FromSeconds(item.DurationSeconds);
                }
                else
                {
                    try
                    {
                        using var reader = new AudioFileReader(item.OutputPath);
                        duration = reader.TotalTime;
                        item.DurationSeconds = duration.TotalSeconds;
                    }
                    catch
                    {
                        duration = TimeSpan.FromSeconds(Math.Max(1.0, item.CharCount * 0.08));
                    }
                }

                var currentEnd = currentStart + duration;

                sb.AppendLine(subIndex.ToString());
                sb.AppendLine($"{FormatSrtTime(currentStart)} --> {FormatSrtTime(currentEnd)}");
                sb.AppendLine(item.Text.Trim());
                sb.AppendLine();

                currentStart = currentEnd + silenceGap;
                subIndex++;
            }

            File.WriteAllText(outputSrtPath, sb.ToString(), new UTF8Encoding(false));
        }, ct);

        return outputSrtPath;
    }

    /// <summary>
    /// Concatenates all Done audio items into a single high-quality MP3 using FFmpeg concat demuxer.
    /// Automatically normalizes all audio clips to 44100Hz 16-bit stereo PCM WAV before concatenation
    /// to avoid any sample rate or channel mismatch between different engines.
    /// </summary>
    public static async Task<string> JoinAudioAsync(
        IEnumerable<TtsLineItem> items,
        int silenceGapMs,
        string outputMp3Path,
        string? ffmpegPath = null,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var validItems = items
            .Where(x => x.Status == TtsStatus.Done && !string.IsNullOrEmpty(x.OutputPath) && File.Exists(x.OutputPath))
            .OrderBy(x => x.Index)
            .ToList();

        if (validItems.Count == 0)
        {
            throw new InvalidOperationException("Không có dòng audio nào có file đầu ra để nối.");
        }

        var resolvedFfmpeg = string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath)
            ? ConfigService.FindFfmpeg()
            : ffmpegPath;

        if (!ConfigService.IsFfmpegReady(resolvedFfmpeg))
        {
            throw new FileNotFoundException($"Không tìm thấy ffmpeg.exe tại '{resolvedFfmpeg}'. Vui lòng cấu hình đường dẫn FFmpeg trong Cài đặt.");
        }

        var outDir = Path.GetDirectoryName(outputMp3Path);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var tmpDir = Path.Combine(Path.GetTempPath(), "AutoTts_Concat_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);

        try
        {
            progress?.Report("Chuẩn hóa các đoạn âm thanh...");

            string? silenceWavPath = null;
            if (silenceGapMs > 0)
            {
                silenceWavPath = Path.Combine(tmpDir, "silence.wav");
                double silenceSeconds = silenceGapMs / 1000.0;
                var silenceArgs = $"-y -f lavfi -i anullsrc=r=44100:cl=stereo -t {silenceSeconds:F3} -c:a pcm_s16le \"{silenceWavPath}\"";
                await RunFfmpegProcessAsync(resolvedFfmpeg, silenceArgs, ct);
            }

            var concatListLines = new List<string>();

            for (int i = 0; i < validItems.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var item = validItems[i];
                progress?.Report($"Chuẩn hóa đoạn {i + 1}/{validItems.Count} (STT: {item.Index})...");

                var normalizedWav = Path.Combine(tmpDir, $"part_{i + 1:D5}.wav");
                var normalizeArgs = $"-y -i \"{item.OutputPath}\" -vn -ar 44100 -ac 2 -c:a pcm_s16le \"{normalizedWav}\"";
                await RunFfmpegProcessAsync(resolvedFfmpeg, normalizeArgs, ct);

                // Forward slashes for concat demuxer list
                concatListLines.Add($"file '{normalizedWav.Replace('\\', '/')}'");

                // Insert silence between sentences (not after the very last one)
                if (silenceWavPath != null && i < validItems.Count - 1)
                {
                    concatListLines.Add($"file '{silenceWavPath.Replace('\\', '/')}'");
                }
            }

            var concatListFile = Path.Combine(tmpDir, "concat_list.txt");
            await File.WriteAllLinesAsync(concatListFile, concatListLines, new UTF8Encoding(false), ct);

            progress?.Report("Đang xuất file MP3 hoàn chỉnh (192kbps 44100Hz)...");
            var mergeArgs = $"-y -f concat -safe 0 -i \"{concatListFile}\" -c:a libmp3lame -b:a 192k -ar 44100 -ac 2 \"{outputMp3Path}\"";
            await RunFfmpegProcessAsync(resolvedFfmpeg, mergeArgs, ct);

            progress?.Report("Hoàn thành nối file MP3.");
            return outputMp3Path;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tmpDir))
                {
                    Directory.Delete(tmpDir, recursive: true);
                }
            }
            catch { /* non-critical temp cleanup */ }
        }
    }

    private static readonly List<Process> _activeProcesses = new();
    private static readonly object _processLock = new();

    /// <summary>
    /// Kills any lingering FFmpeg processes created by the application (called upon FormClosing or cancel).
    /// </summary>
    public static void KillActiveProcesses()
    {
        lock (_processLock)
        {
            foreach (var p in _activeProcesses)
            {
                try
                {
                    if (!p.HasExited)
                    {
                        p.Kill(entireProcessTree: true);
                    }
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
            _activeProcesses.Clear();
        }
    }

    private static async Task RunFfmpegProcessAsync(string ffmpegExe, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = psi };
        lock (_processLock)
        {
            _activeProcesses.Add(process);
        }

        try
        {
            process.Start();

            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var err = await stderrTask;
                throw new InvalidOperationException($"FFmpeg lỗi (mã {process.ExitCode}): {err}");
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }
            throw;
        }
        finally
        {
            lock (_processLock)
            {
                _activeProcesses.Remove(process);
            }
            process.Dispose();
        }
    }

    private static string FormatSrtTime(TimeSpan t)
    {
        int hours = (int)t.TotalHours;
        return $"{hours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";
    }
}
