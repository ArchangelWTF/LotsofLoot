using LotsofLoot.Models.Preset;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class LootMultipliers(LocationConfig locationConfig, LotsOfLootLogger logger) : IOnPresetUpdate
{
    private Dictionary<string, bool>? _originalReduceScavLoot;

    public void Apply(LotsofLootPresetConfig preset)
    {
        // SPT scales the loot multipliers down when a scav raid starts late. A map the server config already exempts stays exempt.
        var scavMaps = locationConfig.ScavRaidTimeSettings.Maps;
        _originalReduceScavLoot ??= scavMaps
            .Where(m => m.Value is not null)
            .ToDictionary(m => m.Key, m => m.Value!.ReduceLootByPercent);

        foreach ((string map, bool reduceLoot) in _originalReduceScavLoot)
        {
            scavMaps[map]!.ReduceLootByPercent = reduceLoot && !preset.General.KeepFullScavRaidLoot;
        }

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
