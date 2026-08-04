namespace LotsofLoot.Models.Web;

/// <summary>
/// Matching rules for the settings search box. The text itself is cascaded down the page as a plain
/// string so Blazor's change detection picks up every keystroke.
/// </summary>
public static class SettingsFilter
{
    /// <summary>The cascading parameter name every Config* component listens on</summary>
    public const string CascadeName = "SettingsFilterText";

    /// <summary>
    /// True when the filter is empty, or any of the supplied strings contains the search text
    /// </summary>
    public static bool Matches(string? filter, params string?[] haystack)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return haystack.Any(candidate => candidate is not null && candidate.Contains(filter, StringComparison.InvariantCultureIgnoreCase));
    }
}
