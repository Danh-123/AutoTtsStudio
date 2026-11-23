using System.Text.Json;
using AutoTtsStudio.Models;

namespace AutoTtsStudio.Services;

/// <summary>
/// Reads/writes AppConfig in config.json next to .exe and auto-detects FFmpeg path.
/// </summary>
public static class ConfigService
{
    public static readonly string BaseDir = AppContext.BaseDirectory;
    private static readonly string ConfigPath = Path.Combine(BaseDir, "config.json");

    public static AppConfig Load()
    {
        AppConfig cfg;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                cfg = JsonSerializer.Deserialize<AppConfig>(json, AppConfig.JsonOptions) ?? CreateDefaults();
            }
            else
            {
                cfg = CreateDefaults();
            }
        }
        catch
        {
            cfg = CreateDefaults();
        }

        // Auto-detect FFmpeg path if empty or invalid
        if (string.IsNullOrEmpty(cfg.FfmpegPath) || !File.Exists(cfg.FfmpegPath))
        {
            cfg.FfmpegPath = FindFfmpeg();
        }

        return cfg;
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, AppConfig.JsonOptions));
        }
        catch { /* never throw on background save */ }
    }

    /// <summary>
    /// Searches in priority order:
    /// 1. AppConfig configured path (if valid)
    /// 2. Tools/ffmpeg.exe relative to .exe
    /// 3. Same dir as .exe
    /// 4. Project/solution root Tools/ffmpeg.exe
    /// 5. PATH environment variable
    /// </summary>
    public static string FindFfmpeg()
    {
        // 1. App directory candidate
        var p1 = Path.Combine(BaseDir, "Tools", "ffmpeg.exe");
        if (File.Exists(p1)) return p1;

        var p2 = Path.Combine(BaseDir, "ffmpeg.exe");
        if (File.Exists(p2)) return p2;

        // 2. Development directories (solution root Tools/ffmpeg.exe)
        var devCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(BaseDir, "..", "..", "..", "..", "Tools", "ffmpeg.exe")),
            Path.GetFullPath(Path.Combine(BaseDir, "..", "..", "..", "Tools", "ffmpeg.exe")),
            Path.GetFullPath(Path.Combine(BaseDir, "..", "..", "Tools", "ffmpeg.exe")),
            Path.GetFullPath(Path.Combine(BaseDir, "..", "Tools", "ffmpeg.exe"))
        };

        foreach (var c in devCandidates)
        {
            if (File.Exists(c)) return c;
        }

        // 3. System PATH
        var onPath = FindOnPath("ffmpeg.exe");
        if (onPath != null) return onPath;

        return "ffmpeg.exe";
    }

    public static bool IsFfmpegReady(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return true;
        var found = FindFfmpeg();
        return File.Exists(found) || FindOnPath("ffmpeg.exe") != null;
    }

    private static AppConfig CreateDefaults() => new()
    {
        LocalApiUrl       = "http://localhost:8880/v1/audio/speech",
        LocalApiModel     = "kokoro",
        AutoSplitMaxChars = 350,
        MaxConcurrency    = 3,
        SilenceGapMs      = 250,
        FfmpegPath        = FindFfmpeg()
    };

    private static string? FindOnPath(string exe)
    {
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full)) return full;
            }
        }
        catch { }
        return null;
    }
}

