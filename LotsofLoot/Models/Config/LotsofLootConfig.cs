using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Config
{
    public class LotsOfLootConfig
    {
        [JsonPropertyName("General")]
        public GeneralConfig General { get; set; } = new();

        /// <summary>
        /// Loose loot multiplier, higher number = more
        /// </summary>
        public Dictionary<string, double> LooseLootMultiplier { get; set; } =
            new Dictionary<string, double>()
            {
                { "bigmap", 5 },
                { "factory4_day", 5 },
                { "factory4_night", 5 },
                { "woods", 5 },
                { "interchange", 5 },
                { "laboratory", 5 },
                { "rezervbase", 5 },
                { "shoreline", 5 },
                { "tarkovstreets", 5 },
                { "lighthouse", 5 },
                { "sandbox", 5 },
                { "sandbox_high", 5 },
                { "labyrinth", 5 },
            };

        /// <summary>
        /// Static loot multiplier, higher number = more
        /// </summary>
        public Dictionary<string, double> StaticLootMultiplier { get; set; } =
            new Dictionary<string, double>()
            {
                { "bigmap", 2 },
                { "factory4_day", 2 },
                { "factory4_night", 2 },
                { "woods", 2 },
                { "interchange", 2 },
                { "laboratory", 2 },
                { "rezervbase", 2 },
                { "shoreline", 2 },
                { "tarkovstreets", 2 },
                { "lighthouse", 2 },
                { "sandbox", 2 },
                { "sandbox_high", 2 },
                { "labyrinth", 2 },
            };

        /// <summary>
        /// Maximum loot generation limits, higher number = higher limit (Would recommend leaving this as is)
        /// </summary>
        public Dictionary<string, int> Limits { get; set; } =
            new Dictionary<string, int>()
            {
                { "bigmap", 10000 },
                { "factory4_day", 1500 },
                { "factory4_night", 1500 },
                { "woods", 10000 },
                { "interchange", 30000 },
                { "laboratory", 25000 },
                { "rezervbase", 40000 },
                { "shoreline", 20000 },
                { "tarkovstreets", 50000 },
                { "lighthouse", 20000 },
                { "sandbox", 15000 },
                { "sandbox_high", 15000 },
                { "labyrinth", 2500 },
            };

        public MarkedRoomConfig MarkedRoomConfig { get; set; } = new();
        public LootInLooseContainerConfig LootinLooseContainer { get; set; } = new();

        /// <summary>
        /// Multiplies the spawn chance of a specific item in its loose loot pool, giving this item a higher chance of spawning.
        /// </summary>
        public Dictionary<MongoId, double> ChangeRelativeProbabilityInPool { get; set; } =
            new Dictionary<MongoId, double>()
            {
                // Graphics card
                { "57347ca924597744596b4e71", 2 },
            };

        /// <summary>
        /// Multiplies the spawn chance of a specific item in its loose loot pool, giving this item a higher chance of spawning.
        /// </summary>
        public Dictionary<MongoId, double> ChangeProbabilityOfPool { get; set; } =
            new Dictionary<MongoId, double>()
            {
                // LEDX Skin Transilluminator
                { "5c0530ee86f774697952d952", 1 },
            };

        /// <summary>
        /// Relative chance multiplier that no items spawn in this container, values: 0 = items every time, 1 = no change
        /// </summary>
        public Dictionary<MongoId, float> Containers { get; set; } =
            new Dictionary<MongoId, float>()
            {
                // Jacket
                { "578f8778245977358849a9b5", 1 },
                // Safe
                { "578f8782245977354405a1e3", 1 },
                // Cash Register
                { "578f879c24597735401e6bc6", 1 },
                // Duffle Bag
                { "578f87a3245977356274f2cb", 1 },
                // Drawer
                { "578f87b7245977356274f2cd", 1 },
                // Medbag SMU06
                { "5909d24f86f77466f56e6855", 1 },
                // Grenade Box
                { "5909d36d86f774660f0bb900", 1 },
                // Wooden ammo box
                { "5909d45286f77465a8136dc6", 1 },
                // Medcase
                { "5909d4c186f7746ad34e805a", 1 },
                // Toolbox
                { "5909d50c86f774659e6aaebe", 1 },
                // Weapon box
                { "5909d5ef86f77467974efbd8", 1 },
                // Weapon box
                { "5909d76c86f77471e53d2adf", 1 },
                // Weapon box
                { "5909d7cf86f77470ee57d75a", 1 },
                // Weapon box
                { "5909d89086f77472591234a0", 1 },
                // Dead Scav
                { "5909e4b686f7747f5b744fa4", 1 },
                // PC block
                { "59139c2186f77411564f8e42", 1 },
                // Jacket
                { "5914944186f774189e5e76c2", 1 },
                // Jacket
                { "5937ef2b86f77408a47244b3", 1 },
                // Jacket Machinery Key
                { "59387ac686f77401442ddd61", 1 },
                // Cash register TAR2-2
                { "5ad74cf586f774391278f6f0", 1 },
                // Plastic suitcase
                { "5c052cea86f7746b2101e8d8", 1 },
                // Common fund stash
                { "5d07b91b86f7745a077a9432", 1 },
                // Ground cache
                { "5d6d2b5486f774785c2ba8ea", 1 },
                // Buried barrel cache
                { "5d6d2bb386f774785b07a77a", 1 },
                // Ration supply crate
                { "5d6fd13186f77424ad2a8c69", 1 },
                // Technical supply crate
                { "5d6fd45b86f774317075ed43", 1 },
                // Medical supply crate
                { "5d6fe50986f77449d97f7463", 1 },
                // Airdrop supply crate
                { "61a89e5445a2672acf66c877", 1 },
                // Duffle bag
                { "61aa1e9a32a4743c3453d2cf", 1 },
                // Medbag SMU06
                { "61aa1ead84ea0800645777fd", 1 },
                // Weapon crate
                { "578f87ad245977356274f2cc", 1 },
            };
    }
}
