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
}
