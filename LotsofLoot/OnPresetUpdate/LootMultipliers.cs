using LotsofLoot.Models.Preset;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class LootMultipliers(ConfigServer configServer, LotsOfLootLogger logger) : IOnPresetUpdate
{
    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

    public void Apply(LotsofLootPresetConfig preset)
    {
        foreach ((string map, double multiplier) in preset.LooseLootMultiplier)
        {
            _locationConfig.LooseLootMultiplier[map] = multiplier;

            _locationConfig.StaticLootMultiplier[map] = preset.StaticLootMultiplier[map];
            _locationConfig.ContainerRandomisationSettings.Enabled = preset.General.LootContainersRandom;

            if (logger.IsDebug())
            {
                logger.Debug($"Loose loot multiplier {map}: {_locationConfig.LooseLootMultiplier[map]}");
                logger.Debug($"Static loot multiplier {map}: {preset.StaticLootMultiplier[map]}");
            }
        }
    }

    public void Revert()
    {
        // Empty, these values can always be set to new ones without needing to be reverted first
    }
}
