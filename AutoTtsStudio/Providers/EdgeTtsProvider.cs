using System.Diagnostics;
using System.Text.Json;
using AutoTtsStudio.Models;
using EdgeTTS.DotNet;

namespace AutoTtsStudio.Providers;

/// <summary>
/// Edge-TTS provider:
/// 1. Tries EdgeTTS.DotNet
/// 2. If 403 / DRM error, seamlessly falls back to Node.js bridge (@andresaya/edge-tts)
/// 3. If Node is absent, falls back to Python edge-tts CLI with clear instructions.
/// </summary>
public sealed class EdgeTtsProvider : ITtsProvider
{
    public ProviderType Type => ProviderType.EdgeTts;

    // Pinned natural English narration voices
    private static readonly string[] PinnedVoiceIds =
    {
        "en-US-AndrewMultilingualNeural",
        "en-US-BrianNeural",
        "en-US-ChristopherNeural",
        "en-US-EricNeural",
        "en-GB-RyanNeural",
        "en-US-AriaNeural",
        "en-US-AvaMultilingualNeural",
        "en-US-GuyNeural"
    };

    public async Task<List<VoiceInfo>> GetVoicesAsync(string? languageFilter, CancellationToken ct)
    {
        // 1. Try Node.js bridge first for freshest list without 403
        var bridgeScript = FindBridgeScript();
        if (bridgeScript != null)
        {
            try
            {
                var voices = await GetVoicesFromBridgeAsync(bridgeScript, ct);
                if (voices != null && voices.Count > 0)
                    return SortAndFilterVoices(voices, languageFilter);
            }
            catch { }
        }

        // 2. Try EdgeTTS.DotNet
        try
        {
            var edgeVoices = await Voices.ListVoicesAsync(cancellationToken: ct);
            var list = new List<VoiceInfo>();
            foreach (var v in edgeVoices)
            {
                list.Add(new VoiceInfo
                {
                    Id = v.ShortName,
                    DisplayName = $"{v.FriendlyName} ({v.Locale})",
                    Language = v.Locale,
                    Gender = v.Gender,
                    Provider = ProviderType.EdgeTts
                });
            }
            if (list.Count > 0)
                return SortAndFilterVoices(list, languageFilter);
        }
        catch { }

        // 3. Fallback voices
        return SortAndFilterVoices(GetFallbackVoices(), languageFilter);
    }

    public async Task<string> SynthesizeToFileAsync(
        string text,
        VoiceSettings settings,
        string baseOutputPathWithoutExt,
        ProxyItem? proxy,
        CancellationToken ct)
    {
        var outputPath = baseOutputPathWithoutExt + ".mp3";
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var voice = string.IsNullOrWhiteSpace(settings.VoiceId)
            ? "en-US-AndrewMultilingualNeural"
            : settings.VoiceId;

        var rate = SpeedToRate(settings.Speed);
        var pitch = PitchToHz(settings.Pitch);
        var proxyUri = proxy?.ToUriString();

        // Step 1: Try EdgeTTS.DotNet C# library
        bool cSharpFailedWith403 = false;
        try
        {
            var comm = new Communicate(
                text: text,
                voice: voice,
                rate: rate,
                pitch: pitch,
                proxy: proxyUri);

            await comm.SaveAsync(outputPath, cancellationToken: ct);

            var fi = new FileInfo(outputPath);
            if (fi.Exists && fi.Length > 0)
                return outputPath;
        }
        catch (Exception ex) when (ex.Message.Contains("403") || (ex.InnerException != null && ex.InnerException.Message.Contains("403")))
        {
            cSharpFailedWith403 = true;
        }
        catch (Exception)
        {
            // If other error, also attempt bridge fallback
            cSharpFailedWith403 = true;
        }

        // Step 2: Fallback to Node.js bridge (@andresaya/edge-tts)
        var bridgeScript = FindBridgeScript();
        if (bridgeScript != null)
        {
            try
            {
                await SynthesizeWithBridgeAsync(bridgeScript, text, voice, rate, pitch, outputPath, proxyUri, ct);
                var fi = new FileInfo(outputPath);
                if (fi.Exists && fi.Length > 0)
                    return outputPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Lỗi tổng hợp qua Node.js Edge-TTS bridge: {ex.Message}", ex);
            }
        }

        // Step 3: Fallback to Python edge-tts CLI
        try
        {
            await SynthesizeWithPythonCliAsync(text, voice, rate, pitch, outputPath, proxyUri, ct);
            var fi = new FileInfo(outputPath);
            if (fi.Exists && fi.Length > 0)
                return outputPath;
        }
        catch { }

        // If all fallbacks failed, throw helpful error
        var errorMsg = cSharpFailedWith403
            ? "Microsoft Edge TTS trả về lỗi 403 (yêu cầu cập nhật token Sec-MS-GEC mới).\n" +
              "Đã thử gọi fallback Node.js nhưng chưa tìm thấy runtime hoặc gói @andresaya/edge-tts.\n" +
              "Cách khắc phục: Cài Node.js và chạy 'npm install' trong thư mục 'scripts/'."
            : $"Không thể tổng hợp giọng đọc Edge-TTS cho giọng '{voice}'. Vui lòng kiểm tra kết nối mạng hoặc thử lại.";

        throw new InvalidOperationException(errorMsg);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Node.js Bridge Execution
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task SynthesizeWithBridgeAsync(
        string bridgeScriptPath,
        string text,
        string voice,
        string rate,
        string pitch,
        string outputPath,
        string? proxyUri,
        CancellationToken ct)
    {
        // Write text to temp file to avoid Windows CLI escaping & length limits
        var tempTextFile = Path.Combine(Path.GetTempPath(), $"autotts_txt_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempTextFile, text, System.Text.Encoding.UTF8, ct);

        try
        {
            var workingDir = Path.GetDirectoryName(bridgeScriptPath)!;
            var args = $"\"{bridgeScriptPath}\" --text-file \"{tempTextFile}\" --voice \"{voice}\" --rate \"{rate}\" --pitch \"{pitch}\" --output \"{outputPath}\"";
            if (!string.IsNullOrEmpty(proxyUri))
                args += $" --proxy \"{proxyUri}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Node bridge exited with code {process.ExitCode}: {(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()}");
            }
        }
        finally
        {
            try { File.Delete(tempTextFile); } catch { }
        }
    }

    private static async Task<List<VoiceInfo>?> GetVoicesFromBridgeAsync(string bridgeScriptPath, CancellationToken ct)
    {
        var workingDir = Path.GetDirectoryName(bridgeScriptPath)!;
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{bridgeScriptPath}\" --list-voices",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            return null;

        var list = new List<VoiceInfo>();
        using var doc = JsonDocument.Parse(stdout);
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var shortName = el.TryGetProperty("ShortName", out var sn) ? sn.GetString() ?? "" : "";
            var friendly = el.TryGetProperty("FriendlyName", out var fn) ? fn.GetString() ?? shortName : shortName;
            var locale = el.TryGetProperty("Locale", out var lc) ? lc.GetString() ?? "" : "";
            var gender = el.TryGetProperty("Gender", out var gd) ? gd.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(shortName))
            {
                list.Add(new VoiceInfo
                {
                    Id = shortName,
                    DisplayName = $"{friendly} ({locale})",
                    Language = locale,
                    Gender = gender,
                    Provider = ProviderType.EdgeTts
                });
            }
        }

        return list;
    }

    private static async Task SynthesizeWithPythonCliAsync(
        string text,
        string voice,
        string rate,
        string pitch,
        string outputPath,
        string? proxyUri,
        CancellationToken ct)
    {
        var args = $"--voice {voice} --rate={rate} --pitch={pitch} --text \"{text.Replace("\"", "\\\"")}\" --write-media \"{outputPath}\"";
        if (!string.IsNullOrEmpty(proxyUri))
            args += $" --proxy {proxyUri}";

        var psi = new ProcessStartInfo
        {
            FileName = "edge-tts",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);
    }

    private static string? FindBridgeScript()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "edge_tts_bridge.mjs"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "scripts", "edge_tts_bridge.mjs"),
            Path.Combine(Directory.GetCurrentDirectory(), "scripts", "edge_tts_bridge.mjs")
        };

        foreach (var c in candidates)
        {
            try
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Voice Sorting & Fallback Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<VoiceInfo> SortAndFilterVoices(List<VoiceInfo> voices, string? languageFilter)
    {
        var pinned = new List<VoiceInfo>();
        var rest = new List<VoiceInfo>();

        foreach (var v in voices)
        {
            if (Array.IndexOf(PinnedVoiceIds, v.Id) >= 0)
                pinned.Add(v);
            else
                rest.Add(v);
        }

        pinned.Sort((a, b) => Array.IndexOf(PinnedVoiceIds, a.Id).CompareTo(Array.IndexOf(PinnedVoiceIds, b.Id)));
        rest.Sort((a, b) => string.Compare(a.Language + a.DisplayName, b.Language + b.DisplayName, StringComparison.OrdinalIgnoreCase));

        var result = pinned.Concat(rest).ToList();

        if (!string.IsNullOrWhiteSpace(languageFilter) && languageFilter != "All")
        {
            result = result.Where(v => v.Language.StartsWith(languageFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return result;
    }

    public static string SpeedToRate(double speed)
    {
        var pct = (int)Math.Round((speed - 1.0) * 100);
        return pct >= 0 ? $"+{pct}%" : $"{pct}%";
    }

    public static string PitchToHz(int pitch)
    {
        return pitch >= 0 ? $"+{pitch}Hz" : $"{pitch}Hz";
    }

    private static List<VoiceInfo> GetFallbackVoices() => new()
    {
        new() { Id = "en-US-AndrewMultilingualNeural", DisplayName = "Andrew Multilingual (en-US)",   Language = "en-US", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-BrianNeural",              DisplayName = "Brian (en-US)",                 Language = "en-US", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-ChristopherNeural",        DisplayName = "Christopher (en-US)",           Language = "en-US", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-EricNeural",               DisplayName = "Eric (en-US)",                  Language = "en-US", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "en-GB-RyanNeural",               DisplayName = "Ryan (en-GB)",                  Language = "en-GB", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-AriaNeural",               DisplayName = "Aria (en-US)",                  Language = "en-US", Gender = "Female", Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-AvaMultilingualNeural",    DisplayName = "Ava Multilingual (en-US)",      Language = "en-US", Gender = "Female", Provider = ProviderType.EdgeTts },
        new() { Id = "en-US-GuyNeural",                DisplayName = "Guy (en-US)",                   Language = "en-US", Gender = "Male",   Provider = ProviderType.EdgeTts },
        new() { Id = "vi-VN-HoaiMyNeural",             DisplayName = "Hoài My (vi-VN)",               Language = "vi-VN", Gender = "Female", Provider = ProviderType.EdgeTts },
        new() { Id = "vi-VN-NamMinhNeural",            DisplayName = "Nam Minh (vi-VN)",              Language = "vi-VN", Gender = "Male",   Provider = ProviderType.EdgeTts }
    };
}
