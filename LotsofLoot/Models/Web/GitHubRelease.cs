using System.Text.Json.Serialization;

namespace LotsofLoot.Models.Web;

public sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; init; }

    [JsonIgnore]
    public string DisplayTitle
    {
        get { return string.IsNullOrWhiteSpace(Name) ? TagName : Name; }
    }
}

/// <summary>
/// What the release cache file on disk holds, so an offline start can still show the last known list
/// </summary>
public sealed record CachedReleases
{
    public DateTimeOffset FetchedAt { get; init; }
    public List<GitHubRelease> Releases { get; init; } = [];
}

public enum ReleaseFeedState
{
    /// <summary>Nothing has been requested yet</summary>
    Idle,
    Loading,
    Loaded,

    /// <summary>GitHub was unreachable and no cached copy exists</summary>
    Unavailable,
}
