using System.Text.RegularExpressions;

namespace LotsofLoot.Models.Web;

public sealed record CompatibilityTag(string Text, string Platform, string? Version);

public static class ReleaseDirectives
{
    private static readonly Regex _htmlComment = new(@"<!--(?<content>.*?)-->", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _compatDirective = new(
        @"^\s*compat(?:ibility)?\s*:\s*(?<entries>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline
    );

    private static readonly Regex _whitespaceRun = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Reads every compatibility entry out of the body, in the order they were written
    /// </summary>
    public static List<CompatibilityTag> ParseCompatibility(string? body)
    {
        List<CompatibilityTag> tags = [];

        if (string.IsNullOrWhiteSpace(body))
        {
            return tags;
        }

        foreach (Match comment in _htmlComment.Matches(body))
        {
            Match directive = _compatDirective.Match(comment.Groups["content"].Value);

            if (!directive.Success)
            {
                continue;
            }

            string[] entries = directive
                .Groups["entries"]
                .Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string entry in entries)
            {
                // A directive may wrap across lines, so collapse any run of whitespace into one space
                string cleaned = _whitespaceRun.Replace(entry, " ").Trim();

                if (cleaned.Length == 0 || tags.Any(tag => string.Equals(tag.Text, cleaned, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                tags.Add(ToTag(cleaned));
            }
        }

        return tags;
    }

    /// <summary>
    /// Splits on the last space, so "Some Other Loader v1.0.0" keeps its multi-word name
    /// </summary>
    private static CompatibilityTag ToTag(string entry)
    {
        int split = entry.LastIndexOf(' ');

        if (split <= 0)
        {
            return new CompatibilityTag(entry, entry, null);
        }

        return new CompatibilityTag(entry, entry[..split].Trim(), entry[(split + 1)..].Trim());
    }

    /// <summary>
    /// Drops every HTML comment, so directives never reach the rendered notes as literal text
    /// </summary>
    public static string? Strip(string? body)
    {
        if (body is null)
        {
            return null;
        }

        return _htmlComment.Replace(body, string.Empty);
    }
}
