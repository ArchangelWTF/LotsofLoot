namespace LotsofLoot.Models.Preset;

public sealed class NormalizedLootPresetConfig
{
    public bool Enabled { get; set; }

    /// <summary>
    /// How far eligible item weights move toward equal odds: 0 leaves them unchanged, 1 makes them equal.
    /// </summary>
    public double Strength { get; set; } = 0.5;

    internal bool IsActive => Enabled && double.IsFinite(Strength) && Strength > 0;

    /// <summary>
    /// Only positive weights above this threshold are normalized. Zero-weight entries stay excluded.
    /// </summary>
    public int MinProbability { get; set; }
}
