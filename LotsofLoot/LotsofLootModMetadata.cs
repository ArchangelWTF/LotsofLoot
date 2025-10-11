using SPTarkov.Server.Core.Models.Spt.Mod;
using Version = SemanticVersioning.Version;
using Range = SemanticVersioning.Range;

namespace LotsofLoot
{
    public record LotsofLootModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "wtf.archangel.lotsoflootredux";
        public override string Name { get; init; } = "Lots of Loot Redux";
        public override string Author { get; init; } = "ArchangelWTF";
        public override List<string>? Contributors { get; init; } = ["RainbowPC"];
        public override Version Version { get; init; } = new("4.0.0");
        public override Range SptVersion { get; init; } = new("~4.0");
        public override List<string>? Incompatibilities { get; init; } = [];
        public override Dictionary<string, Range>? ModDependencies { get; init; } = [];
        public override string? Url { get; init; } = "https://github.com/ArchangelWTF/LotsofLoot";
        public override bool? IsBundleMod { get; init; } = false;
        public override string License { get; init; } = "MIT";
    }
}
