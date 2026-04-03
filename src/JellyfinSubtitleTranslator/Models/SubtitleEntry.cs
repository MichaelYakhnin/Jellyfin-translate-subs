namespace JellyfinSubtitleTranslator.Models;

public class SubtitleEntry
{
    public int Index { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public List<string> Lines { get; set; } = new();

    public string Text => string.Join("\n", Lines);

    public override string ToString()
    {
        var start = FormatTime(StartTime);
        var end = FormatTime(EndTime);
        return $"{Index}\n{start} --> {end}\n{Text}\n";
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
    }
}
