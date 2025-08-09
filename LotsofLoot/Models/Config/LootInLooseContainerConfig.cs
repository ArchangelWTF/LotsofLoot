using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Config
{
    public class LootInLooseContainerConfig
    {
        /// <summary>
        /// Changes the max amount of items spawned in Loose Containers (things like item cases, docs cases and such.. 
        /// Setting this to 0 will turn this behavior off.) | Value: 0 - 1
        /// </summary>
        public double LootInContainerModifier { get; set; } = 1;

        /// <summary>
        /// Changes the max amount of items spawned in Backpacks that are spawned in the world 
        /// (Setting this to 0 will turn this off.) | Value: 0 - 1
        /// </summary>
        public double LootInBackpackModifier { get; set; } = 1;

        /// <summary>
        /// This changes the limits of items spawned in loose containers (Think wallets, keycard holders, 
        /// sicc organizational pouches, docs cases and more)
        /// Currently this only supports keys and keycards as limits.
        /// </summary>
        public Dictionary<MongoId, LootInLooseContainerSpawnLimit> SpawnLimits { get; set; } = new()
        {
            // WZ Wallet
            ["60b0f6c058e0b0481a09ad11"] =  new()
            {
                Keycards = 0
            },

            // Simple wallet
            ["5783c43d2459774bbe137486"] = new()
            {
                Keycards = 0 
            },

            // Keycard holder case
            ["619cbf9e0a7c3a1a2731940a"] = new() 
            { 
                Keycards = 2 
            },

            // Document case
            ["590c60fc86f77412b13fddcf"] = new() 
            { 
                Keys = 3,
                Keycards = 1
            },

            // S I C C organizational pouch
            ["5d235bb686f77443f4331278"] = new() 
            { 
                Keys = 3, 
                Keycards = 1
            },

            // Key tool
            ["59fafd4b86f7745ca07e1232"] = new() 
            { 
                Keys = 5
            },

            // Gingy keychain
            ["62a09d3bcf4a99369e262447"] = new() 
            { 
                Keys = 2
            }
        };

        /// <summary>
        /// This allows for adding items to the lootinLooseContainer blacklist, 
        /// preventing these items from being selected for spawning
        /// </summary>
        public Dictionary<MongoId, List<MongoId>> Blacklist { get; set; } = new()
        {
            // Documents case
            ["590c60fc86f77412b13fddcf"] =
            [
                "664d3db6db5dea2bad286955", // Shatun's hideout key
                "664d3dd590294949fe2d81b7", // Grumpy's hideout key
                "664d3ddfdda2e85aca370d75", // Voron's hideout key
                "664d3de85f2355673b09aed5"  // Leon's hideout key
            ],

            // S I C C organizational pouch
            ["5d235bb686f77443f4331278"] =
            [
                "664d3db6db5dea2bad286955", // Shatun's hideout key
                "664d3dd590294949fe2d81b7", // Grumpy's hideout key
                "664d3ddfdda2e85aca370d75", // Voron's hideout key
                "664d3de85f2355673b09aed5"  // Leon's hideout key
            ],

            // Key tool
            ["59fafd4b86f7745ca07e1232"] =
            [
                "664d3db6db5dea2bad286955", // Shatun's hideout key
                "664d3dd590294949fe2d81b7", // Grumpy's hideout key
                "664d3ddfdda2e85aca370d75", // Voron's hideout key
                "664d3de85f2355673b09aed5"  // Leon's hideout key
            ],

            // Gingy keychain
            ["62a09d3bcf4a99369e262447"] =
            [
                "664d3db6db5dea2bad286955", // Shatun's hideout key
                "664d3dd590294949fe2d81b7", // Grumpy's hideout key
                "664d3ddfdda2e85aca370d75", // Voron's hideout key
                "664d3de85f2355673b09aed5"  // Leon's hideout key
            ]
        };
    }

    /// <summary>
    /// Spawn limit configuration for keys and keycards
    /// </summary>
    public class LootInLooseContainerSpawnLimit
    {
        /// <summary>
        /// Maximum number of keys that can spawn
        /// </summary>
        public int? Keys { get; set; }

        /// <summary>
        /// Maximum number of keycards that can spawn
        /// </summary>
        public int? Keycards { get; set; }
    }
}
