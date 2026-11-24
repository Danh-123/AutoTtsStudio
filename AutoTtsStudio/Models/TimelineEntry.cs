namespace AutoTtsStudio.Models;

/// <summary>
/// Đại diện cho một mốc timeline (câu thoại hoặc cảnh gom) kèm timestamp chính xác.
/// </summary>
public class TimelineEntry
{
    public int Index { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double DurationSeconds => Math.Round(Math.Max(0, EndSeconds - StartSeconds), 2);
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Định dạng mốc bắt đầu [mm:ss], nếu >= 60 phút thì hiển thị tổng phút [95:12] để đồng bộ video timeline.
    /// </summary>
    public string StartTag => FormatTag(StartSeconds);

    /// <summary>
    /// Định dạng mốc kết thúc [mm:ss].
    /// </summary>
    public string EndTag => FormatTag(EndSeconds);

    /// <summary>
    /// Dải thời gian chuẩn định dạng phụ đề SRT (hh:mm:ss,fff --> hh:mm:ss,fff).
    /// </summary>
    public string SrtRange => $"{FormatSrtTimestamp(StartSeconds)} --> {FormatSrtTimestamp(EndSeconds)}";

    public static string FormatTag(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int totalMinutes = (int)(totalSeconds / 60);
        int seconds = (int)(totalSeconds % 60);
        return $"[{totalMinutes:D2}:{seconds:D2}]";
    }

    public static string FormatSrtTimestamp(double totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        int hours = (int)ts.TotalHours;
        return $"{hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2},{ts.Milliseconds:D3}";
    }

    public TimelineEntry Clone() => new()
    {
        Index = Index,
        StartSeconds = StartSeconds,
        EndSeconds = EndSeconds,
        Text = Text
    };
}
