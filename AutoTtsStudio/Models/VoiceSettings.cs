namespace AutoTtsStudio.Models;

public class VoiceSettings
{
    public ProviderType Provider { get; set; } = ProviderType.EdgeTts;
    public string VoiceId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Language { get; set; } = "en-US";

    // Common
    public double Speed { get; set; } = 1.0;

    // Edge-TTS only
    public int Pitch { get; set; } = 0; // Hz offset (-50 to +50)

    // ElevenLabs only
    public int Stability { get; set; } = 50;      // 0-100 (CFG Weight for Local AI)
    public int Similarity { get; set; } = 75;     // 0-100
    public int Style { get; set; } = 0;           // 0-100 (Exaggeration for Local AI)
    public bool SpeakerBoost { get; set; } = false;

    // Custom params toggle
    public bool CustomParams { get; set; } = false;

    // Global
    public int SilenceGapMs { get; set; } = 250;
}
