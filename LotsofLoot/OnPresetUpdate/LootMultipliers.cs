using LotsofLoot.Models.Preset;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class LootMultipliers(LocationConfig locationConfig, LotsOfLootLogger logger) : IOnPresetUpdate
{
    public void Apply(LotsofLootPresetConfig preset)
    {
        foreach ((string map, double multiplier) in preset.LooseLootMultiplier)
        {
            locationConfig.LooseLootMultiplier[map] = multiplier;

            locationConfig.StaticLootMultiplier[map] = preset.StaticLootMultiplier[map];
            locationConfig.ContainerRandomisationSettings.Enabled = preset.General.LootContainersRandom;

            if (logger.IsDebug())
            {
                logger.Debug($"Loose loot multiplier {map}: {locationConfig.LooseLootMultiplier[map]}");
                logger.Debug($"Static loot multiplier {map}: {preset.StaticLootMultiplier[map]}");
            }
        }
    }

    public void Revert()
    {
        // Empty, these values can always be set to new ones without needing to be reverted first
    }
}
