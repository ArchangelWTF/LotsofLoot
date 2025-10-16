using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Config
{
    public class MarkedRoomConfig
    {
        /// <summary>
        /// Marked room loot multiplier, higher = more loot probability
        /// </summary>
        public Dictionary<string, double> Multiplier { get; set; } =
            new Dictionary<string, double>()
            {
                // Customs
                { "bigmap", 1 },
                // Reserve
                { "reservbase", 1 },
                // Streets of Tarkov
                { "tarkovstreets", 1 },
                // Lighthouse
                { "lighthouse", 1 },
            };

        /// <summary>
        /// Adds these items to the marked room loot pool, lower number = rarer item
        /// </summary>
        public Dictionary<MongoId, double> ExtraItems { get; set; } =
            new Dictionary<MongoId, double>()
            {
                // Keycard holder case
                { "619cbf9e0a7c3a1a2731940a", 1 },
                // Weapon case
                { "59fb023c86f7746d0d4b423c", 2 },
                // Documents case
                { "590c60fc86f77412b13fddcf", 4 },
                // WZ Wallet
                { "60b0f6c058e0b0481a09ad11", 6 },
                // Injector case
                { "619cbf7d23893217ec30b689", 1 },
                // Key tool
                { "59fafd4b86f7745ca07e1232", 3 },
                // S I C C organizational pouch
                { "5d235bb686f77443f4331278", 3 },
                // Item case
                { "59fb042886f7746c5005a7b2", 2 },
                // T H I C C item case
                { "5c0a840b86f7742ffa4f2482", 1 },
                // T H I C C Weapon case
                { "5b6d9ce188a4501afc1b2b25", 1 },
            };

        /// <summary>
        /// Multiplies the chance for a group of items to spawn in the marked room, higher number = more common
        /// </summary>
        public Dictionary<MongoId, double> ItemGroups { get; set; } =
            new Dictionary<MongoId, double>()
            {
                // Weapons group
                { "5422acb9af1c889c16000029", 0.2d },
            };
    }
}
