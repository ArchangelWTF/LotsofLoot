using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace LotsofLoot;

public record LotsofLootModMetadata : AbstractModMetadata
{
    /// <summary>
    /// After SVM, hopefully
    ///
    /// I have no idea why WTT armory has such a crazy high offset
    /// </summary>
    public const int LotsofLootPriorityOffset = 1000;

    public override string ModGuid { get; init; } = "wtf.archangel.lotsoflootredux";
    public override string Name { get; init; } = "Lots of Loot Redux";
    public override string Author { get; init; } = "ArchangelWTF";
    public override List<string>? Contributors { get; init; } = ["RainbowPC"];
    public override Version Version { get; init; } = new("4.1.0");
    public override Range SptVersion { get; init; } = new("~4.0");
    public override List<string>? Incompatibilities { get; init; } = [];
    public override Dictionary<string, Range>? ModDependencies { get; init; } = [];
    public override string? Url { get; init; } = "https://github.com/ArchangelWTF/LotsofLoot";
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}
