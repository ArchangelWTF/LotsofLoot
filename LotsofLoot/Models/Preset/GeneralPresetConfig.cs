using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Preset;

public sealed class GeneralPresetConfig
{
    /// <summary>
    /// Allows the loot generator to pick multiple of the same spawnpoints so that loot can spawn on top of one another
    /// </summary>
    public required bool AllowLootOverlay { get; set; }

    /// <summary>
    /// Removes backpack restrictions, allowing you to pick items like the T H I C C item case that you might find in raid
    /// </summary>
    public required bool RemoveBackpackRestrictions { get; set; }

    /// <summary>
    /// Disables the BSG flea blacklist
    /// </summary>
    public required bool DisableFleaRestrictions { get; set; }

    /// <summary>
    /// Allows the rusted key room on Streets of Tarkov to also spawn keycards
    /// </summary>
    public required bool RustedKeyRoomIncludesKeycards { get; set; }

    /// <summary>
    /// Cheaper items are more likely to spawn in containers
    /// </summary>
    public required bool ItemWeights { get; set; }

    /// <summary>
    /// Some items don't have good or accurate data set for their price points, this changes the pricing on these items to be more realistic
    /// </summary>
    public required Dictionary<MongoId, double> PriceCorrection { get; set; }

    /// <summary>
    /// Allows for setting if containers will spawn randomly, false will disable randomness.
    /// </summary>
    public required bool LootContainersRandom { get; set; }

    /// <summary>
    /// Raises the lower end of SPT's loose loot rolls for more consistent loose loot spawns
    /// </summary>
    public required bool ReduceLowLooseLootRolls { get; set; }

    /// <summary>
    /// Controls how strongly low loose-loot rolls are adjusted upward.
    /// Lower values smooth out low rolls (less randomness), while higher values
    /// allow low rolls to stay low (more randomness).
    /// </summary>
    public required double ReduceLowLooseLootRollsAmount { get; set; }
}
