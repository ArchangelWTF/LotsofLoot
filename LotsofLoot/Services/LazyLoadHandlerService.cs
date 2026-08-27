using System.Diagnostics;
using LotsofLoot.Helpers;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services;

namespace LotsofLoot.Services;

[Injectable(InjectionType.Singleton)]
public class LazyLoadHandlerService(
    LocationTable locationTable,
    ConfigService configService,
    LootRoomHelper lootRoomHelper,
    LotsOfLootLogger logger
)
{
    private readonly record struct PoolModifier(double Modifier, MongoId Template);

    public void OnPostDBLoad()
    {
        var locations = locationTable.GetDictionary();

        foreach ((string locationId, Location location) in locations)
        {
            if (location.StaticLoot is not null)
            {
                location.StaticLoot.AddTransformer(lazyloadedStaticLootData =>
                {
                    HandleStaticLootLazyLoad(locationId, lazyloadedStaticLootData);

                    return lazyloadedStaticLootData;
                });
            }

            if (location.LooseLoot is not null)
            {
                location.LooseLoot.AddTransformer(lazyLoadedLooseLootData =>
                {
                    HandleLooseLootLazyLoad(locationId, lazyLoadedLooseLootData);

                    return lazyLoadedLooseLootData;
                });
            }
        }
    }

    private void HandleStaticLootLazyLoad(string locationId, Dictionary<MongoId, StaticLootDetails>? staticLootData)
    {
        //This should not be null, but just in case.
        if (staticLootData is null)
        {
            return;
        }

        Stopwatch sw = Stopwatch.StartNew();
        foreach ((MongoId containerId, StaticLootDetails lootDetails) in staticLootData)
        {
            foreach (ItemDistribution itemDistribution in lootDetails.ItemDistribution)
            {
                if (itemDistribution.RelativeProbability == 0)
                {
                    logger.Warning($"Relative probability is 0? For container {containerId}");
                    continue;
                }

                if (!configService.LotsofLootPresetConfig.Containers.TryGetValue(containerId, out double configRelativeProbability))
                {
                    continue;
                }

                //Todo: Does this even work as intended? Check?
                itemDistribution.RelativeProbability = MathF.Round(
                    (float)((itemDistribution.RelativeProbability ?? 0) * configRelativeProbability)
                );
                if (logger.IsDebug())
                {
                    logger.Debug($"Changed container {containerId} chance to {itemDistribution.RelativeProbability}");
                }
            }
        }

        sw.Stop();
        logger.Info($"HandleStaticLootLazyLoad finished, took {sw.ElapsedMilliseconds}ms");
    }

    private void HandleLooseLootLazyLoad(string locationId, LooseLoot? looseLootData)
    {
        //This should not be null, but just in case.
        if (looseLootData is null || looseLootData.Spawnpoints is null)
        {
            return;
        }

        Stopwatch sw = Stopwatch.StartNew();
        foreach (var spawnpoint in looseLootData.Spawnpoints)
        {
            ChangeRelativeProbabilityInPool(locationId, spawnpoint);
            ChangeProbabilityOfPool(locationId, spawnpoint);

            lootRoomHelper.AdjustLootRooms(locationId, spawnpoint);

            //Todo: This still needs AddToRustedKeyRoom for streets
        }

        sw.Stop();
        logger.Info($"HandleLooseLootLazyLoad finished, took {sw.ElapsedMilliseconds}ms");
    }

    private void ChangeRelativeProbabilityInPool(string locationId, Spawnpoint spawnpoint)
    {
        var config = configService.LotsofLootPresetConfig.ChangeRelativeProbabilityInPool;

        if (config.Count == 0)
        {
            return;
        }

        Dictionary<string, PoolModifier>? modifiers = null;

        foreach (var item in spawnpoint.Template?.Items ?? [])
        {
            if (item.ComposedKey is null || !config.TryGetValue(item.Template, out double modifier))
            {
                continue;
            }

            modifiers ??= [];

            // Mods can inject a composed key that already exists in the pool, so we just stack rather than override
            // Yes this looks disgusting, I dont care
            modifiers[item.ComposedKey] = modifiers.TryGetValue(item.ComposedKey, out PoolModifier existing)
                ? existing with
                {
                    Modifier = existing.Modifier * modifier,
                }
                : new PoolModifier(modifier, item.Template);
        }

        // Most pools contain none of the configured items, so the distribution pass is usually skipped entirely
        if (modifiers is null)
        {
            return;
        }

        foreach (LooseLootItemDistribution itemDistribution in spawnpoint.ItemDistribution ?? [])
        {
            if (
                itemDistribution.ComposedKey?.Key is null
                || !modifiers.TryGetValue(itemDistribution.ComposedKey.Key, out PoolModifier match)
            )
            {
                continue;
            }

            itemDistribution.RelativeProbability *= match.Modifier;

            if (logger.IsDebug())
            {
                logger.Debug($"{locationId}, {spawnpoint.Template?.Id}, {match.Template}, {itemDistribution.RelativeProbability}");
            }
        }
    }

    private void ChangeProbabilityOfPool(string locationId, Spawnpoint spawnpoint)
    {
        foreach (var item in spawnpoint.Template?.Items ?? [])
        {
            if (configService.LotsofLootPresetConfig.ChangeProbabilityOfPool.TryGetValue(item.Template, out double probabilityMultiplier))
            {
                if (spawnpoint.Probability is null)
                {
                    continue;
                }

                var spawnpointProbability = spawnpoint.Probability ?? 0;

                spawnpoint.Probability = Math.Min(spawnpointProbability * probabilityMultiplier, 1);

                if (logger.IsDebug())
                {
                    logger.Debug($"{locationId}, Pool:{spawnpoint.Template!.Id}, Chance:{spawnpoint.Probability}");
                }

                // Only apply once per pool
                break;
            }
        }
    }
}
