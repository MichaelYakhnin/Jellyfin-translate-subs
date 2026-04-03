using System.Text;
using System.Text.RegularExpressions;
using JellyfinSubtitleTranslator.Models;

namespace JellyfinSubtitleTranslator.Services;

public interface ISrtParser
{
    List<SubtitleEntry> Parse(string content);
    string Serialize(List<SubtitleEntry> entries);
}

public partial class SrtParser : ISrtParser
{
    private static readonly Regex TimeLineRegex = MyRegex();

    [GeneratedRegex(@"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})")]
    private static partial Regex MyRegex();

    public List<SubtitleEntry> Parse(string content)
    {
        var entries = new List<SubtitleEntry>();
        var blocks = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length < 3) continue;

            if (!int.TryParse(lines[0].Trim(), out var index)) continue;

            var timeMatch = TimeLineRegex.Match(lines[1]);
            if (!timeMatch.Success) continue;

            var startTime = new TimeSpan(
                0,
                int.Parse(timeMatch.Groups[1].Value),
                int.Parse(timeMatch.Groups[2].Value),
                int.Parse(timeMatch.Groups[3].Value),
                int.Parse(timeMatch.Groups[4].Value)
            );

            var endTime = new TimeSpan(
                0,
                int.Parse(timeMatch.Groups[5].Value),
                int.Parse(timeMatch.Groups[6].Value),
                int.Parse(timeMatch.Groups[7].Value),
                int.Parse(timeMatch.Groups[8].Value)
            );

            var textLines = lines.Skip(2).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            entries.Add(new SubtitleEntry
            {
                Index = index,
                StartTime = startTime,
                EndTime = endTime,
                Lines = textLines
            });
        }

        return entries;
    }

    public string Serialize(List<SubtitleEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.Append(entry.ToString());
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
