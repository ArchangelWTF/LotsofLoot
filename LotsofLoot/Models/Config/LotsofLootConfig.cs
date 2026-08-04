namespace LotsofLoot.Models.Config;

public sealed class LotsofLootConfig
{
    /// <summary>
    /// The name of the current preset that is supposed to be loaded
    /// </summary>
    public string PresetName { get; set; } = "default";

    /// <summary>
    /// Enables debug logging
    /// </summary>
    public bool IsDebugEnabled { get; set; } = false;

    /// <summary>
    /// Picks a random preset from <see cref="RandomizedPresets"/> before each raid's loot is generated.
    ///
    /// Only settings the loot generator reads per raid actually change. Flea restrictions, backpack
    /// restrictions and price correction are baked into the item database the client caches when it
    /// connects, so those stay on whatever preset was active at that point.
    /// </summary>
    public bool RandomizePresets { get; set; } = false;

    /// <summary>
    /// Presets that take part in randomization. Opt-in, so an unfinished preset can sit on disk without
    /// turning up in a raid.
    /// </summary>
    public List<string> RandomizedPresets { get; set; } = [];
}
