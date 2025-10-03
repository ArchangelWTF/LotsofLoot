using SPTarkov.Server.Core.Models.Spt.Mod;
using Version = SemanticVersioning.Version;

namespace LotsofLoot
{
    public record LotsofLootModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.archangelwtf.lotsoflootredux";
        public override string Name { get; init; } = "Lots of Loot Redux";
        public override string Author { get; init; } = "ArchangelWTF";
        public override List<string>? Contributors { get; init; } = ["RainbowPC"];
        public override Version Version { get; init; } = new("4.0.0");
        public override Version SptVersion { get; init; } = new("4.0.0");

        public override List<string>? Incompatibilities { get; init; } = [];
        public override Dictionary<string, Version>? ModDependencies { get; init; } = [];
        public override string? Url { get; init; } = "https://github.com/ArchangelWTF/LotsofLoot";
        public override bool? IsBundleMod { get; init; } = false;
        public override string License { get; init; } = "MIT";
    }
}
