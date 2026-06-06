using SPTarkov.Server.Core.Models.Common;

namespace LotsofLoot.Models.Preset;

public class RefRoomPresetConfig
{
    /// <summary>
    /// Ref room loot multiplier, higher = more loot probability
    /// </summary>
    public required Dictionary<string, double> Multiplier { get; set; }

    /// <summary>
    /// Multiplies the chance for a group of items to spawn in the room, higher number = more common
    /// </summary>
    public required Dictionary<MongoId, double> ItemGroups { get; set; }
}
