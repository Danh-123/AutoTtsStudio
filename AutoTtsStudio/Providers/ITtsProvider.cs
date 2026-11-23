using AutoTtsStudio.Models;

namespace AutoTtsStudio.Providers;

/// <summary>
/// Contract for all TTS engine providers.
/// </summary>
public interface ITtsProvider
{
    ProviderType Type { get; }

    /// <summary>Fetch available voices, optionally filtered by language code (e.g. "en-US").</summary>
    Task<List<VoiceInfo>> GetVoicesAsync(string? languageFilter, CancellationToken ct);

    /// <summary>
    /// Synthesize text to file on disk.
    /// Returns the full path of the saved file (.mp3 or .wav).
    /// </summary>
    Task<string> SynthesizeToFileAsync(
        string text,
        VoiceSettings settings,
        string baseOutputPathWithoutExt,
        ProxyItem? proxy,
        CancellationToken ct);
}
