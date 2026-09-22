using System.Text.RegularExpressions;

namespace Overlight.App.Terminal.Services;

/// <summary>
/// Parses shorthand durations like "30s", "5m", "1h", "1h30m" into a
/// TimeSpan for the "loop add" command.
/// </summary>
public static partial class IntervalParser
{
    [GeneratedRegex(@"(\d+)\s*(h|m|s)", RegexOptions.IgnoreCase)]
    private static partial Regex TokenPattern();

    public static bool TryParse(string text, out TimeSpan interval)
    {
        interval = TimeSpan.Zero;
        text = text.Trim();

        if (text.Length == 0)
        {
            return false;
        }

        MatchCollection matches = TokenPattern().Matches(text);
        if (matches.Count == 0)
        {
            return false;
        }

        // Every character of the input must belong to a matched token —
        // otherwise something like "5 bananas" would silently parse as 0.
        int matchedLength = matches.Sum(m => m.Length);
        if (matchedLength != text.Replace(" ", string.Empty).Length)
        {
            return false;
        }

        TimeSpan total = TimeSpan.Zero;
        foreach (Match match in matches)
        {
            int value = int.Parse(match.Groups[1].Value);
            total += match.Groups[2].Value.ToLowerInvariant() switch
            {
                "h" => TimeSpan.FromHours(value),
                "m" => TimeSpan.FromMinutes(value),
                "s" => TimeSpan.FromSeconds(value),
                _ => TimeSpan.Zero,
            };
        }

        if (total <= TimeSpan.Zero)
        {
            return false;
        }

        interval = total;
        return true;
    }
}
