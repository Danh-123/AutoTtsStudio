using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoTtsStudio.Models;

public class AppConfig
{
    // ── Local AI ───────────────────────────────────────────────────────────────
    public string LocalApiUrl   { get; set; } = "http://localhost:8880/v1/audio/speech";
    public string LocalApiModel { get; set; } = "kokoro";
    public string LocalApiKey   { get; set; } = string.Empty;

    // ── ElevenLabs ─────────────────────────────────────────────────────────────
    public string ElevenLabsApiKey { get; set; } = string.Empty;

    // ── FFmpeg ─────────────────────────────────────────────────────────────────
    public string FfmpegPath { get; set; } = string.Empty;

    // ── Voice preferences ──────────────────────────────────────────────────────
    public List<string> FavoriteVoiceIds { get; set; } = new();
    public string LastVoiceId   { get; set; } = string.Empty;
    public string LastVoiceText { get; set; } = string.Empty;
    public ProviderType LastProvider { get; set; } = ProviderType.EdgeTts;

    // ── Output ─────────────────────────────────────────────────────────────────
    public string LastOutputDir { get; set; } = string.Empty;

    // ── Processing ─────────────────────────────────────────────────────────────
    public int  MaxConcurrency    { get; set; } = 3;
    public int  AutoSplitMaxChars { get; set; } = 350;
    public int  SilenceGapMs      { get; set; } = 250;
    public bool AutoRetry         { get; set; } = true;
    public bool AutoSplit         { get; set; } = true;
    public bool AutoCreateSrt     { get; set; } = false;
    public bool AutoJoinMp3       { get; set; } = false;
    public bool CustomVoiceParams { get; set; } = false;

    // ── JSON Options ──────────────────────────────────────────────────────────
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
