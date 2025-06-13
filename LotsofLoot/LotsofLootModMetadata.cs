using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LotsofLoot
{
    public record LotsofLootModMetadata : AbstractModMetadata
    {
        public override string Name { get; set; } = "LotsOfLoot Redux";
        public override string Author { get; set; } = "ArchangelWTF";
        public override List<string>? Contributors { get; set; } = ["RainbowPC"];
        public override string Version { get; set; } = "4.0.0";
        public override string SptVersion { get; set; } = "4.0.0";
        public override List<string>? LoadBefore { get; set; } = [];
        public override List<string>? LoadAfter { get; set; } = ["[SVM] Server Value Modifier"];
        public override List<string>? Incompatibilities { get; set; } = [];
        public override Dictionary<string, string>? ModDependencies { get; set; } = [];
        public override string? Url { get; set; } = "https://github.com/ArchangelWTF/LotsofLoot";
        public override bool? IsBundleMod { get; set; } = false;
        public override string? Licence { get; set; } = "MIT";
    }
}
