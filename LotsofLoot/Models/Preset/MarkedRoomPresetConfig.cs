using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Preset;

public sealed class MarkedRoomPresetConfig
{
    /// <summary>
    /// Marked room loot multiplier, higher = more loot probability
    /// </summary>
    public required Dictionary<string, double> Multiplier { get; set; }

    /// <summary>
    /// Adds these items to the marked room loot pool, lower number = rarer item
    /// </summary>
    public required Dictionary<MongoId, double> ExtraItems { get; set; }

    /// <summary>
    /// Multiplies the chance for a group of items to spawn in the marked room, higher number = more common
    /// </summary>
    public required Dictionary<MongoId, double> ItemGroups { get; set; }
}
