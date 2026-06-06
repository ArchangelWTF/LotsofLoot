using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Preset;

public sealed class LootInLooseContainerPresetConfig
{
    /// <summary>
    /// Changes the max amount of items spawned in Loose Containers (things like item cases, docs cases and such..
    /// Setting this to 0 will turn this behavior off.) | Value: 0 - 1
    /// </summary>
    public required double LootInContainerModifier { get; set; }

    /// <summary>
    /// Changes the max amount of items spawned in Backpacks that are spawned in the world
    /// (Setting this to 0 will turn this off.) | Value: 0 - 1
    /// </summary>
    public required double LootInBackpackModifier { get; set; }

    /// <summary>
    /// This changes the limits of items spawned in loose containers (Think wallets, keycard holders,
    /// sicc organizational pouches, docs cases and more)
    /// Currently this only supports keys and keycards as limits.
    /// </summary>
    public required Dictionary<MongoId, LootInLooseContainerSpawnLimit> SpawnLimits { get; set; }

    /// <summary>
    /// This allows for adding items to the lootinLooseContainer blacklist,
    /// preventing these items from being selected for spawning
    /// </summary>
    public required Dictionary<MongoId, List<MongoId>> Blacklist { get; set; }
}

/// <summary>
/// Spawn limit configuration for keys and keycards
/// </summary>
public sealed class LootInLooseContainerSpawnLimit
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
