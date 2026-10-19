namespace LotsofLoot.Models.Preset;

public sealed class NormalizedLootPresetConfig
{
    /// <summary>
    /// Normalizes the spawn chances of all loose loot items in the pool, making rarer items have a fairer chance of spawning.
    /// </summary>
    public required bool Enabled { get; set; }

    /// <summary>
    /// The logarithmic base used for normalizing loot. Lower means less normalized, higher values makes all values closer to being equal.
    /// </summary>
    public required double LogBase { get; set; }

    /// <summary>
    /// The minimum relative probability of an item for it to be normalized.
    /// </summary>
    public required int MinProbability { get; set; }
}
