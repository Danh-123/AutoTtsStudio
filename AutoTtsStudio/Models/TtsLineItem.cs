namespace AutoTtsStudio.Models;

public enum TtsStatus
{
    Pending,
    Processing,
    Done,
    Error
}

public class TtsLineItem
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
    public int CharCount => Text?.Length ?? 0;
    public double DurationSeconds { get; set; }
    public TtsStatus Status { get; set; } = TtsStatus.Pending;
    public string OutputPath { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    public string StatusDisplay
    {
        get
        {
            if (Status == TtsStatus.Done) return "Done";
            if (Status == TtsStatus.Processing) return "Processing...";
            if (Status == TtsStatus.Error)
            {
                if (string.IsNullOrWhiteSpace(ErrorMessage))
                    return "Error";

                var clean = ErrorMessage.Replace("\r", " ").Replace("\n", " ").Trim();
                if (clean.Length > 28)
                    clean = clean[..25] + "...";

                return clean.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
                    ? clean
                    : $"Error: {clean}";
            }
            return "Pending";
        }
    }

    public string DurationDisplay =>
        DurationSeconds > 0 ? $"{DurationSeconds:F2}s" : string.Empty;
}
