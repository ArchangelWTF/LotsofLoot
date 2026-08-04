using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Services;

/// <summary>
/// Rolls a new preset before each raid's loot is generated, when the user has opted in.
/// </summary>
[Injectable]
public sealed class PresetRandomizerService(ConfigService configService, RandomUtil randomUtil, LotsOfLootLogger logger)
{
    public void RollForRaid()
    {
        if (!ConfigService.LotsofLootConfig.RandomizePresets)
        {
            return;
        }

        // A preset can be deleted off disk after being opted in, so the list is filtered every roll
        var candidates = ConfigService
            .LotsofLootConfig.RandomizedPresets.Where(configService.PresetExists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            logger.Debug("Preset randomization is on, but no selected preset exists on disk");
            return;
        }

        var picked = randomUtil.GetRandomElement(candidates);

        if (string.Equals(picked, configService.CurrentlyLoadedPreset, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (configService.ApplyPresetForRaid(picked))
        {
            logger.Success($"Preset randomization switched to '{picked}' for this raid");
        }
    }
}
