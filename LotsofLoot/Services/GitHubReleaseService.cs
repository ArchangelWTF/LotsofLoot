using System.Net.Http.Headers;
using System.Text.Json;
using LotsofLoot.Models.Web;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Utils;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace LotsofLoot.Services;

[Injectable(InjectionType.Singleton)]
public sealed class GitHubReleaseService(
    ConfigService configService,
    IHttpClientFactory httpClientFactory,
    ISptLogger<GitHubReleaseService> logger
)
{
    private const string ReleasesUrl = "https://api.github.com/repos/ArchangelWTF/LotsofLoot/releases?per_page=10";

    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions _serializerOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public event Action? FeedChanged;

    public ReleaseFeedState State { get; private set; } = ReleaseFeedState.Idle;
    public IReadOnlyList<GitHubRelease> Releases { get; private set; } = [];

    /// <summary>
    /// The newest stable release that runs on this server, which is what we compare the running version against
    /// </summary>
    public GitHubRelease? LatestStable
    {
        get { return Releases.FirstOrDefault(release => !release.Draft && !release.Prerelease && IsCompatible(release)); }
    }

    // A release with no versioned SPT tag tells us nothing either way, so it stays eligible
    public static bool IsCompatible(GitHubRelease release)
    {
        List<CompatibilityTag> sptTags =
        [
            .. ReleaseDirectives
                .ParseCompatibility(release.Body)
                .Where(tag => string.Equals(tag.Platform, "SPT", StringComparison.OrdinalIgnoreCase) && tag.Version is not null),
        ];

        return sptTags.Count == 0 || sptTags.Any(MatchesRunningPlatform);
    }

    /// <summary>
    /// True when GitHub knows about a stable release newer than the version we are running
    /// </summary>
    public bool UpdateAvailable
    {
        get
        {
            if (LatestStable is null || !TryParseVersion(LatestStable.TagName, out var latest))
            {
                return false;
            }

            return latest > configService.ModMetadata.Version;
        }
    }

    private string CachePath
    {
        get { return Path.Combine(configService.ModPath, "cache", "releases.json"); }
    }

    /// <summary>
    /// Loads the feed, hitting GitHub only when the cached copy has aged out
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (State == ReleaseFeedState.Loaded && DateTimeOffset.Now - _fetchedAt < _cacheLifetime)
        {
            return;
        }

        await RefreshAsync(false, cancellationToken);
    }

    /// <summary>
    /// Refreshes the feed from GitHub
    /// </summary>
    /// <param name="force">Ignore the cache lifetime and always go out to the network</param>
    public async Task RefreshAsync(bool force = true, CancellationToken cancellationToken = default)
    {
        if (!await _fetchLock.WaitAsync(0, cancellationToken))
        {
            // Another page already kicked off a fetch, its result will be broadcast to us too
            return;
        }

        try
        {
            if (!force && State == ReleaseFeedState.Loaded && DateTimeOffset.Now - _fetchedAt < _cacheLifetime)
            {
                return;
            }

            if (State != ReleaseFeedState.Loaded)
            {
                SetState(ReleaseFeedState.Loading);

                // A cached copy gets us something on screen immediately while the request is in flight
                CachedReleases? cached = await ReadCacheAsync(cancellationToken);

                if (cached is not null)
                {
                    Releases = cached.Releases;
                    _fetchedAt = cached.FetchedAt;
                    SetState(ReleaseFeedState.Loaded);

                    if (DateTimeOffset.Now - cached.FetchedAt < _cacheLifetime && !force)
                    {
                        return;
                    }
                }
            }

            List<GitHubRelease>? fetched = await FetchAsync(cancellationToken);

            if (fetched is null)
            {
                // Keep whatever the cache gave us, otherwise admit we have nothing
                SetState(Releases.Count > 0 ? ReleaseFeedState.Loaded : ReleaseFeedState.Unavailable);
                return;
            }

            Releases = fetched;
            _fetchedAt = DateTimeOffset.Now;
            SetState(ReleaseFeedState.Loaded);

            await WriteCacheAsync(cancellationToken);
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<List<GitHubRelease>?> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = httpClientFactory.CreateClient();

            client.Timeout = _requestTimeout;

            // GitHub rejects requests without a user agent outright
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LotsofLootRedux", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using HttpResponseMessage response = await client.GetAsync(ReleasesUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"[Lots of Loot Redux] GitHub returned {(int)response.StatusCode} for the release feed");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, _serializerOptions, cancellationToken);

            return releases?.Where(release => !release.Draft).ToList();
        }
        catch (Exception ex)
        {
            // Offline installs are the norm rather than the exception, so this never escalates past debug
            logger.Debug($"[Lots of Loot Redux] Could not reach GitHub for the release feed: {ex.Message}");
            return null;
        }
    }

    private async Task<CachedReleases?> ReadCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(CachePath);
            return await JsonSerializer.DeserializeAsync<CachedReleases>(stream, _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Debug($"[Lots of Loot Redux] Could not read the cached release feed: {ex.Message}");
            return null;
        }
    }

    private async Task WriteCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);

            var payload = new CachedReleases { FetchedAt = _fetchedAt, Releases = [.. Releases] };

            await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(payload, _serializerOptions), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Debug($"[Lots of Loot Redux] Could not cache the release feed: {ex.Message}");
        }
    }

    private void SetState(ReleaseFeedState state)
    {
        State = state;
        FeedChanged?.Invoke();
    }

    public static bool TryParseVersion(string tag, out Version version)
    {
        return Version.TryParse(tag.TrimStart('v', 'V'), out version);
    }

    /// <summary>
    /// True when a release's compatibility tag describes the server this mod is running on.
    /// </summary>
    public static bool MatchesRunningPlatform(CompatibilityTag tag)
    {
        if (!string.Equals(tag.Platform, "SPT", StringComparison.OrdinalIgnoreCase) || tag.Version is null)
        {
            return false;
        }

        Version? running = ProgramStatics.SPT_VERSION();

        if (running is null)
        {
            return false;
        }

        // Treated as a range so "4.1" covers every 4.1.x, and "~4.1" or "4.1.0" keep working too
        if (!Range.TryParse(tag.Version.TrimStart('v', 'V'), out Range supported))
        {
            return false;
        }

        // IsSatisfied only accepts a string, so the version has to go back through ToString here
        return supported.IsSatisfied(running.ToString());
    }
}
