using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Config
{
    public class GeneralConfig
    {
        /// <summary>
        /// Enables debug logging
        /// </summary>
        public bool IsDebugEnabled { get; set; } = false;

        /// <summary>
        /// Allows the loot generator to pick multiple of the same spawnpoints so that loot can spawn on top of one another
        /// </summary>
        public bool AllowLootOverlay { get; set; } = false;

        /// <summary>
        /// Removes backpack restrictions, allowing you to pick items like the T H I C C item case that you might find in raid
        /// </summary>
        public bool RemoveBackpackRestrictions { get; set; } = true;

        /// <summary>
        /// Disables the BSG flea blacklist
        /// </summary>
        public bool DisableFleaRestrictions { get; set; } = false;

        /// <summary>
        /// Allows the rusted key room on Streets of Tarkov to also spawn keycards
        /// </summary>
        public bool RustedKeyRoomIncludesKeycards { get; set; } = false;

        /// <summary>
        /// Cheaper items are more likely to spawn in containers
        /// </summary>
        public bool ItemWeights { get; set; } = false;

        /// <summary>
        /// Some items don't have good or accurate data set for their price points, this changes the pricing on these items to be more realistic
        /// </summary>
        public Dictionary<MongoId, long> PriceCorrection { get; set; } =
            new Dictionary<MongoId, long>
            {
                // T H I C C item case
                { "5c0a840b86f7742ffa4f2482", 15000000 },
                // Weapon case
                { "59fb023c86f7746d0d4b423c", 7000000 },
                // Rusted bloody key
                { "64d4b23dc1b37504b41ac2b6", 1000000 },
            };

        /// <summary>
        /// Allows for setting if containers will spawn randomly, false will disable randomness.
        /// </summary>
        public bool LootContainersRandom { get; set; } = true;

        /// <summary>
        /// Raises the lower end of SPT's loose loot rolls for more consistent loose loot spawns
        /// </summary>
        public bool ReduceLowLooseLootRolls { get; set; } = false;
    }
}
