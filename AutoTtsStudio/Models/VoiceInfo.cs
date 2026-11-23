namespace AutoTtsStudio.Models;

public class VoiceInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public ProviderType Provider { get; set; }

    public override string ToString() => $"{DisplayName} ({Language})";
}
