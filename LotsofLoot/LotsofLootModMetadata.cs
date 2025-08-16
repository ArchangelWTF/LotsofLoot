using SPTarkov.Server.Core.Models.Spt.Mod;
using Version = SemanticVersioning.Version;

namespace LotsofLoot
{
    public record LotsofLootModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.archangelwtf.lotsoflootredux";
        public override string Name { get; init; } = "Lots of Loot Redux";
        public override string Author { get; init; } = "ArchangelWTF";
        public override List<string>? Contributors { get; set; } = ["RainbowPC"];
        public override Version Version { get; } = new("4.0.0");
        public override Version SptVersion { get; } = new("4.0.0");
        public override List<string>? LoadBefore { get; set; } = [];
        public override List<string>? LoadAfter { get; set; } = ["[SVM] Server Value Modifier"];
        public override List<string>? Incompatibilities { get; set; } = [];
        public override Dictionary<string, Version>? ModDependencies { get; set; } = [];
        public override string? Url { get; set; } = "https://github.com/ArchangelWTF/LotsofLoot";
        public override bool? IsBundleMod { get; set; } = false;
        public override string License { get; init; } = "MIT";
    }
}
