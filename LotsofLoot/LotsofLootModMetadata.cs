using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace LotsofLoot;

public record LotsofLootModMetadata : IModMetadata, IModBlazorMetadata
{
    /// <summary>
    /// After SVM, hopefully
    ///
    /// I have no idea why WTT armory has such a crazy high offset
    /// </summary>
    public const int LotsofLootPriorityOffset = 1000;

    public string ModGuid { get; init; } = "wtf.archangel.lotsoflootredux";
    public string Name { get; init; } = "Lots of Loot Redux";
    public string Author { get; init; } = "ArchangelWTF";
    public List<string>? Contributors { get; init; } = ["RainbowPC"];
    public Version Version { get; init; } = new(BuildInfo.Version);
    public Range SptVersion { get; init; } = new("~4.1");
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/ArchangelWTF/LotsofLoot";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;

    public string? WWWRootUrl { get; init; }
    public string? HomePage { get; init; } = "/lotsofloot";
    public string? HomePageDescription { get; init; } = "Configure loot generation, presets, and location loot from SIC.";
}
