namespace LotsofLoot.Models.Preset;

public interface IOnPresetUpdate
{
    public void Apply(LotsofLootPresetConfig preset);
    public void Revert();
}
