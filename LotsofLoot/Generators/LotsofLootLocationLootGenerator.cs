using LotsofLoot.Helpers;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Inventory;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators;

[Injectable]
public class LotsofLootLocationLootGenerator(
    ItemHelper itemHelper,
    ItemFilterService itemFilterService,
    NewSPTRandomUtil randomUtil,
    SeasonalEventService seasonalEventService,
    LotsOfLootLogger logger,
    ConfigService config,
    CounterTrackerHelper counterTrackerHelper,
    ConfigServer configServer,
    ServerLocalisationService serverLocalisationService,
    LocationLootGeneratorReflectionHelper locationLootGeneratorReflectionHelper,
    ICloner cloner,
    IEnumerable<ILootSpawnpointDecider> lootSpawnpointDeciders,
    IEnumerable<ILootItemCreator> lootItemCreators
)
{
    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

    public List<SpawnpointTemplate> GenerateDynamicLoot(
        LooseLoot dynamicLootDist,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
        string locationName
    )
    {
        List<SpawnpointTemplate> loot = [];

        bool christmasEnabled = seasonalEventService.ChristmasEventEnabled();
        bool seasonalEventActive = seasonalEventService.SeasonalEventEnabled();
        var seasonalItemTplBlacklist = seasonalEventService.GetInactiveSeasonalEventItems();

        // Build the list of forced loot from `SpawnpointsForced`, remove christmas items if season is not active
        List<Spawnpoint> dynamicForcedSpawnPoints = christmasEnabled
            ? [.. dynamicLootDist.SpawnpointsForced]
            : dynamicLootDist
                .SpawnpointsForced.Where(point => !point.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase))
                .ToList();

        var blacklistedSpawnPoints = _locationConfig.LooseLootBlacklist.GetValueOrDefault(locationName);

        // Init empty array to hold spawn points, letting us pick them pseudo-randomly
        var spawnPointArray = new ProbabilityObjectArray<string, Spawnpoint>(cloner);

        // Positions not in forced but have 100% chance to spawn
        List<Spawnpoint> guaranteedLoosePoints = [];

        foreach (var spawnPoint in dynamicLootDist.Spawnpoints)
        {
            if (spawnPoint is null)
            {
                logger.Warning("Spawnpoint is null!");
                continue;
            }

            if (spawnPoint.Template?.Id is null)
            {
                logger.Warning("Spawnpoint template id is null!");
                continue;
            }

            if (!christmasEnabled && spawnPoint.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Point is blacklisted, skip
            if (blacklistedSpawnPoints?.Contains(spawnPoint.Template.Id) ?? false)
            {
                if (logger.IsDebug())
                {
                    logger.Debug($"Ignoring loose loot location: {spawnPoint.Template.Id}");
                }

                continue;
            }

            // Handle IsAlwaysSpawn, add to forced spawn points and skip these
            if (spawnPoint.Template.IsAlwaysSpawn.GetValueOrDefault())
            {
                dynamicForcedSpawnPoints.Add(spawnPoint);
                continue;
            }

            // 100%, add it to guaranteed
            if (spawnPoint.Probability == 1)
            {
                guaranteedLoosePoints.Add(spawnPoint);
                continue;
            }

            spawnPointArray.Add(new ProbabilityObject<string, Spawnpoint>(spawnPoint.Template.Id, spawnPoint.Probability ?? 0, spawnPoint));
        }

        loot.AddRange(locationLootGeneratorReflectionHelper.GetForcedDynamicLoot(dynamicForcedSpawnPoints, locationName, staticAmmoDist));

        var desiredSpawnPointCount = 0;

        if (config.LotsofLootPresetConfig.General.ReduceLowLooseLootRolls)
        {
            var mean = dynamicLootDist.SpawnpointCount.Mean;

            var rawValue = randomUtil.GetNormallyDistributedRandomNumber(mean, dynamicLootDist.SpawnpointCount.Std);

            if (rawValue < mean)
            {
                logger.Debug($"Value ({rawValue}) is lower than mean ({mean})");

                var deviation = mean - rawValue;

                // Lower multiplier means more, after having tested for a bit 0.45 seems a good sweet spot for now.
                rawValue = mean - (deviation * config.LotsofLootPresetConfig.General.ReduceLowLooseLootRollsAmount);
            }

            desiredSpawnPointCount = (int)
                Math.Round(locationLootGeneratorReflectionHelper.GetLooseLootMultiplierForLocation(locationName) * rawValue);
        }
        else
        {
            // Default SPT calculation
            desiredSpawnPointCount = (int)
                Math.Round(
                    locationLootGeneratorReflectionHelper.GetLooseLootMultiplierForLocation(locationName)
                        * randomUtil.GetNormallyDistributedRandomNumber(
                            dynamicLootDist.SpawnpointCount.Mean,
                            dynamicLootDist.SpawnpointCount.Std
                        )
                );
        }

        int lotsofLootDesiredSpawnPointCount = config.LotsofLootPresetConfig.Limits[locationName];

        if (desiredSpawnPointCount > lotsofLootDesiredSpawnPointCount)
        {
            logger.Warning("SPT desires a higher spawn point count than Lots of Loot! Clamping.");

            desiredSpawnPointCount = lotsofLootDesiredSpawnPointCount;
        }

        List<Spawnpoint> chosenSpawnPoints = [];

        foreach (var lootSpawnDecider in lootSpawnpointDeciders)
        {
            chosenSpawnPoints.AddRange(
                lootSpawnDecider.Decide(locationName, desiredSpawnPointCount, spawnPointArray, guaranteedLoosePoints)
            );
        }

        // Do we have enough items in pool to fulfill requirement
        var tooManySpawnPointsRequested = desiredSpawnPointCount - chosenSpawnPoints.Count > 0;
        if (tooManySpawnPointsRequested)
        {
            if (logger.IsDebug())
            {
                logger.Debug(
                    serverLocalisationService.GetText(
                        "location-spawn_point_count_requested_vs_found",
                        new
                        {
                            requested = desiredSpawnPointCount + guaranteedLoosePoints.Count,
                            found = chosenSpawnPoints.Count,
                            mapName = locationName,
                        }
                    )
                );
            }
        }

        // Iterate over spawnPoints
        foreach (var spawnPoint in chosenSpawnPoints)
        {
            // SpawnPoint is invalid, skip it
            if (spawnPoint.Template is null)
            {
                logger.Warning(serverLocalisationService.GetText("location-missing_dynamic_template", spawnPoint.LocationId));

                continue;
            }

            // Ensure no blacklisted lootable items are in pool
            // And ensure no seasonal items are in pool if not in-season
            spawnPoint.Template.Items = spawnPoint
                .Template.Items.Where(item =>
                    !itemFilterService.IsLootableItemBlacklisted(item.Template)
                    && (seasonalEventActive || !seasonalItemTplBlacklist.Contains(item.Template))
                )
                .ToList();

            // Spawn point has no items after filtering, skip
            if (spawnPoint.Template.Items is null || !spawnPoint.Template.Items.Any())
            {
                if (logger.IsDebug())
                {
                    logger.Debug(serverLocalisationService.GetText("location-spawnpoint_missing_items", spawnPoint.Template.Id));
                }

                continue;
            }

            // Get an array of allowed IDs after above filtering has occured
            var validComposedKeys = spawnPoint.Template.Items.Select(item => item.ComposedKey).ToHashSet();

            // Construct container to hold above filtered items, letting us pick an item for the spot
            var itemArray = new ProbabilityObjectArray<string, double?>(cloner);
            foreach (var itemDist in spawnPoint.ItemDistribution)
            {
                if (!validComposedKeys.Contains(itemDist.ComposedKey.Key))
                {
                    continue;
                }

                itemArray.Add(new ProbabilityObject<string, double?>(itemDist.ComposedKey.Key, itemDist.RelativeProbability ?? 0, null));
            }

            if (itemArray.Count == 0)
            {
                logger.Warning(serverLocalisationService.GetText("location-loot_pool_is_empty_skipping", spawnPoint.Template.Id));

                continue;
            }

            // Draw a random item from the spawn points possible items
            var chosenComposedKey = itemArray.Draw().FirstOrDefault();
            var chosenItem = spawnPoint.Template.Items.FirstOrDefault(item => item.ComposedKey == chosenComposedKey);
            if (chosenItem is null)
            {
                logger.Warning(
                    $"Unable to find item with composed key: {chosenComposedKey}, skipping spawn point: {spawnPoint.LocationId} "
                );
                continue;
            }

            var createItemResult = CreateStaticLootItem(chosenItem.Template, staticAmmoDist, null);

            // If count reaches max, skip adding item to loot
            if (counterTrackerHelper.IncrementCount(createItemResult.Items.FirstOrDefault().Template))
            {
                continue;
            }

            // Root id can change when generating a weapon, ensure ids match
            spawnPoint.Template.Root = createItemResult.Items.FirstOrDefault().Id;

            // Convert the processed items into the correct output type
            var convertedItems = createItemResult.Items.Select(item => item.ToLootItem()).ToList();

            // Overwrite entire pool with chosen item
            spawnPoint.Template.Items = convertedItems;

            loot.Add(spawnPoint.Template);
        }

        return loot;
    }

    //Todo: Fully needs implementing
    public Spawnpoint? HandleSpawningAlwaysSpawnSpawnpoint(List<Spawnpoint> spawnpoints, string location)
    {
        return null;
    }

    public ContainerItem CreateStaticLootItem(
        string chosenTpl,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
        string? parentId = null
    )
    {
        TemplateItem? itemTemplate = GetItemTemplate(chosenTpl);

        if (itemTemplate is null || itemTemplate.Properties is null)
        {
            logger.Warning($"{chosenTpl} has no template?");

            return new ContainerItem
            {
                Items = [],
                Width = 0,
                Height = 0,
            };
        }

        int? width = itemTemplate.Properties.Width;
        int? height = itemTemplate.Properties.Height;

        List<Item> items =
        [
            new()
            {
                Id = new MongoId(),
                Template = chosenTpl,
                ParentId = parentId,
            },
        ];

        foreach (var lootItemCreator in lootItemCreators)
        {
            if (lootItemCreator.CanCreateItem(chosenTpl))
            {
                Item rootItem = items[0];
                lootItemCreator.CreateItem(items, itemTemplate, staticAmmoDist, this);

                if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.WEAPON))
                {
                    ItemSize itemSize = itemHelper.GetItemSize(items, rootItem.Id);
                    width = itemSize.Width;
                    height = itemSize.Height;
                }

                break;
            }
        }

        return new ContainerItem
        {
            Items = items,
            Width = width,
            Height = height,
        };
    }

    private TemplateItem? GetItemTemplate(string itemTpl)
    {
        KeyValuePair<bool, TemplateItem?> item = itemHelper.GetItem(itemTpl);

        if (item.Key)
        {
            return item.Value;
        }
        else
        {
            return null;
        }
    }
}
