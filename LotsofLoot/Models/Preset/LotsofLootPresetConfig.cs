using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Preset;

public sealed class LotsofLootPresetConfig
{
    [JsonPropertyName("General")]
    public required GeneralPresetConfig General { get; set; }

    /// <summary>
    /// Loose loot multiplier, higher number = more
    /// </summary>
    public required Dictionary<string, double> LooseLootMultiplier { get; set; }

    /// <summary>
    /// Static loot multiplier, higher number = more
    /// </summary>
    public required Dictionary<string, double> StaticLootMultiplier { get; set; }

    /// <summary>
    /// Maximum loot generation limits, higher number = higher limit (Would recommend leaving this as is)
    /// </summary>
    public required Dictionary<string, int> Limits { get; set; }

    public required MarkedRoomPresetConfig MarkedRoomConfig { get; set; }
    public required RefRoomPresetConfig RefRoomConfig { get; set; }
    public required LootInLooseContainerPresetConfig LootinLooseContainer { get; set; }

    /// <summary>
    /// Multiplies the spawn chance of a specific item in its loose loot pool, giving this item a higher chance of spawning.
    /// </summary>
    public required Dictionary<MongoId, double> ChangeRelativeProbabilityInPool { get; set; }

    /// <summary>
    /// Multiplies the spawn chance of a specific item in its loose loot pool, giving this item a higher chance of spawning.
    /// </summary>
    public required Dictionary<MongoId, double> ChangeProbabilityOfPool { get; set; }

    /// <summary>
    /// Relative chance multiplier that no items spawn in this container, values: 0 = items every time, 1 = no change
    /// </summary>
    public required Dictionary<MongoId, float> Containers { get; set; }
}
