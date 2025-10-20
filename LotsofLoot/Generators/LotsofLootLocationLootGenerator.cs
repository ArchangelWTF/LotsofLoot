using LotsofLoot.Helpers;
using LotsofLoot.Models.Config;
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
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators
{
    [Injectable(InjectionType.Singleton)]
    public class LotsofLootLocationLootGenerator(
        ItemHelper itemHelper,
        PresetHelper presetHelper,
        ItemFilterService itemFilterService,
        RandomUtil randomUtil,
        ICloner cloner,
        LotsofLootItemHelper LotsofLootItemHelper,
        SeasonalEventService seasonalEventService,
        LotsOfLootLogger logger,
        ConfigService config,
        DatabaseService databaseService,
        CounterTrackerHelper counterTrackerHelper,
        ConfigServer configServer,
        ServerLocalisationService serverLocalisationService,
        LocationLootGeneratorReflectionHelper locationLootGeneratorReflectionHelper
    )
    {
        private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();
        private readonly Dictionary<string, List<MongoId>> _itemFilterIndexCache = [];
        private readonly HashSet<MongoId> _foreignCurrencies =
        [
            // Dollars
            "5696686a4bdc2da3298b456a",
            // Euros
            "569668774bdc2da2298b4568",
        ];

        /// <summary>
        /// This method closely mirrors that of SPT
        /// The only difference being the bypass for loot overlay and using Lots of Loot's createStaticLootItem
        ///
        /// Todo: This might need more work? I only added back one check for loot overlay for now
        /// </summary>
        public List<SpawnpointTemplate> GenerateDynamicLoot(
            LooseLoot dynamicLootDist,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            string locationName
        )
        {
            List<SpawnpointTemplate> loot = [];
            List<Spawnpoint> dynamicForcedSpawnPoints = [];

            // Remove christmas items from loot data
            if (!seasonalEventService.ChristmasEventEnabled())
            {
                dynamicLootDist.Spawnpoints = dynamicLootDist.Spawnpoints.Where(point =>
                    !point.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase)
                );
                dynamicLootDist.SpawnpointsForced = dynamicLootDist.SpawnpointsForced.Where(point =>
                    !point.Template.Id.StartsWith("christmas", StringComparison.OrdinalIgnoreCase)
                );
            }

            // Build the list of forced loot from both `SpawnpointsForced` and any point marked `IsAlwaysSpawn`
            dynamicForcedSpawnPoints.AddRange(dynamicLootDist.SpawnpointsForced);
            dynamicForcedSpawnPoints.AddRange(dynamicLootDist.Spawnpoints.Where(point => point.Template.IsAlwaysSpawn.GetValueOrDefault()));

            loot.AddRange(
                locationLootGeneratorReflectionHelper.GetForcedDynamicLoot(dynamicForcedSpawnPoints, locationName, staticAmmoDist)
            );

            // Draw from random distribution
            var desiredSpawnPointCount = Math.Round(
                locationLootGeneratorReflectionHelper.GetLooseLootMultiplierForLocation(locationName)
                    * randomUtil.GetNormallyDistributedRandomNumber(
                        dynamicLootDist.SpawnpointCount.Mean,
                        dynamicLootDist.SpawnpointCount.Std
                    )
            );

            var blacklistedSpawnPoints = _locationConfig.LooseLootBlacklist.GetValueOrDefault(locationName);

            // Init empty array to hold spawn points, letting us pick them pseudo-randomly
            var spawnPointArray = new ProbabilityObjectArray<string, Spawnpoint>(cloner);

            // Positions not in forced but have 100% chance to spawn
            List<Spawnpoint> guaranteedLoosePoints = [];

            var allDynamicSpawnPoints = dynamicLootDist.Spawnpoints;
            foreach (var spawnPoint in allDynamicSpawnPoints)
            {
                // Point is blacklisted, skip
                if (blacklistedSpawnPoints?.Contains(spawnPoint.Template.Id) ?? false)
                {
                    if (logger.IsDebug())
                    {
                        logger.Debug($"Ignoring loose loot location: {spawnPoint.Template.Id}");
                    }

                    continue;
                }

                // We've handled IsAlwaysSpawn above, so skip them
                if (spawnPoint.Template.IsAlwaysSpawn ?? false)
                {
                    continue;
                }

                // 100%, add it to guaranteed
                if (spawnPoint.Probability == 1)
                {
                    guaranteedLoosePoints.Add(spawnPoint);
                    continue;
                }

                spawnPointArray.Add(
                    new ProbabilityObject<string, Spawnpoint>(spawnPoint.Template.Id, spawnPoint.Probability ?? 0, spawnPoint)
                );
            }

            // Select a number of spawn points to add loot to
            // Add ALL loose loot with 100% chance to pool
            List<Spawnpoint> chosenSpawnPoints = [];
            chosenSpawnPoints.AddRange(guaranteedLoosePoints);

            var randomSpawnPointCount = desiredSpawnPointCount - chosenSpawnPoints.Count;
            // Only draw random spawn points if needed
            if (randomSpawnPointCount > 0 && spawnPointArray.Count > 0)
            // Add randomly chosen spawn points
            {
                if (!config.LotsOfLootConfig.General.AllowLootOverlay)
                {
                    foreach (var si in spawnPointArray.DrawAndRemove((int)randomSpawnPointCount))
                    {
                        chosenSpawnPoints.Add(spawnPointArray.Data(si));
                    }
                }
                else
                {
                    // Draw without removing if we allow loot overlay
                    foreach (var si in spawnPointArray.Draw((int)randomSpawnPointCount))
                    {
                        chosenSpawnPoints.Add(spawnPointArray.Data(si));
                    }
                }
            }

            // Filter out duplicate locationIds // prob can be done better
            chosenSpawnPoints = chosenSpawnPoints.GroupBy(spawnPoint => spawnPoint.LocationId).Select(group => group.First()).ToList();

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
            var seasonalEventActive = seasonalEventService.SeasonalEventEnabled();
            var seasonalItemTplBlacklist = seasonalEventService.GetInactiveSeasonalEventItems();
            foreach (var spawnPoint in chosenSpawnPoints)
            {
                // SpawnPoint is invalid, skip it
                if (spawnPoint.Template is null)
                {
                    logger.Warning(serverLocalisationService.GetText("location-missing_dynamic_template", spawnPoint.LocationId));

                    continue;
                }

                // Ensure no blacklisted lootable items are in pool
                spawnPoint.Template.Items = spawnPoint
                    .Template.Items.Where(item => !itemFilterService.IsLootableItemBlacklisted(item.Template))
                    .ToList();

                // Ensure no seasonal items are in pool if not in-season
                if (!seasonalEventActive)
                {
                    spawnPoint.Template.Items = spawnPoint.Template.Items.Where(item => !seasonalItemTplBlacklist.Contains(item.Template));
                }

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

                    itemArray.Add(
                        new ProbabilityObject<string, double?>(itemDist.ComposedKey.Key, itemDist.RelativeProbability ?? 0, null)
                    );
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

            // Handle different item types
            if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.WEAPON))
            {
                Item rootItem = items[0];
                items = HandleWeaponItem(items, chosenTpl, staticAmmoDist);

                // Set proper width and height on weapon
                ItemSize itemSize = itemHelper.GetItemSize(items, rootItem.Id);
                width = itemSize.Width;
                height = itemSize.Height;
            }
            else if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.MONEY) || itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.AMMO))
            {
                int stackCount = randomUtil.GetInt(
                    (int)itemTemplate.Properties.StackMinRandom,
                    (int)itemTemplate.Properties.StackMaxRandom
                );
                items[0].Upd = new Upd { StackObjectsCount = stackCount };
            }
            else if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.AMMO_BOX))
            {
                itemHelper.AddCartridgesToAmmoBox(items, itemTemplate);
            }
            else if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.MAGAZINE))
            {
                HandleMagazineItem(items, itemTemplate, staticAmmoDist);
            }
            else if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.SIMPLE_CONTAINER) && chosenTpl != "5c093e3486f77430cb02e593")
            {
                HandleContainerOrBackpackItem(items, staticAmmoDist, config.LotsOfLootConfig.LootinLooseContainer.LootInContainerModifier);
            }
            else if (itemHelper.IsOfBaseclass(chosenTpl, BaseClasses.BACKPACK))
            {
                HandleContainerOrBackpackItem(items, staticAmmoDist, config.LotsOfLootConfig.LootinLooseContainer.LootInBackpackModifier);
            }
            else if (itemHelper.ArmorItemCanHoldMods(chosenTpl))
            {
                Preset? defaultPreset = presetHelper.GetDefaultPreset(chosenTpl);

                if (defaultPreset != null)
                {
                    List<Item> presetAndMods = defaultPreset.Items.ReplaceIDs().ToList();
                    presetAndMods.RemapRootItemId();
                    presetAndMods[0].ParentId = items[0].ParentId;
                    items = presetAndMods;
                }
                else if (itemTemplate.Properties.Slots?.Count() > 0)
                {
                    items = itemHelper.AddChildSlotItems(
                        items,
                        itemTemplate,
                        configServer.GetConfig<LocationConfig>().EquipmentLootSettings.ModSpawnChancePercent
                    );
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

        private List<Item> HandleWeaponItem(List<Item> items, string tpl, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist)
        {
            Item rootItem = items[0];

            // Get the original weapon preset
            Preset? weaponPreset = cloner.Clone(presetHelper.GetDefaultPreset(tpl));
            if (weaponPreset?.Items != null)
            {
                List<Item> itemWithChildren = itemHelper.ReparentItemAndChildren(weaponPreset.Items[0], weaponPreset.Items);

                if (itemWithChildren != null && itemWithChildren.Count > 0)
                {
                    items = itemHelper.ReparentItemAndChildren(rootItem, itemWithChildren);
                }
            }

            Item? magazine = items.Find(x => x.SlotId == "mod_magazine");

            if (magazine != null && randomUtil.GetChance100(configServer.GetConfig<LocationConfig>().MagazineLootHasAmmoChancePercent))
            {
                // Get required templates
                TemplateItem? magTemplate = itemHelper.GetItem(magazine.Template).Value;
                TemplateItem? weaponTemplate = itemHelper.GetItem(tpl).Value;
                TemplateItem? defaultWeapon = itemHelper.GetItem(rootItem.Template).Value;

                // Fill the magazine with cartridges
                List<Item> magazineWithCartridges = [magazine];

                itemHelper.FillMagazineWithRandomCartridge(
                    magazineWithCartridges,
                    magTemplate,
                    staticAmmoDist,
                    weaponTemplate.Properties.AmmoCaliber,
                    configServer.GetConfig<LocationConfig>().MinFillStaticMagazinePercent / 100.0,
                    defaultWeapon.Properties.DefAmmo,
                    defaultWeapon
                );

                // Replace the original magazine with the filled version
                var magIndex = items.IndexOf(magazine);
                items.RemoveAt(magIndex);
                items.InsertRange(magIndex, magazineWithCartridges);
            }

            return items;
        }

        private void HandleMagazineItem(
            List<Item> items,
            TemplateItem itemTemplate,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist
        )
        {
            if (!randomUtil.GetChance100(configServer.GetConfig<LocationConfig>().MagazineLootHasAmmoChancePercent))
            {
                return;
            }

            List<Item> magazineWithCartridges = [items[0]];

            itemHelper.FillMagazineWithRandomCartridge(
                magazineWithCartridges,
                itemTemplate,
                staticAmmoDist,
                null,
                configServer.GetConfig<LocationConfig>().MinFillStaticMagazinePercent / 100.0
            );

            items.RemoveAt(0);
            items.InsertRange(0, magazineWithCartridges);
        }

        private void HandleContainerOrBackpackItem(
            List<Item> items,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            double modifier
        )
        {
            List<Item> containerLoot = CreateLootInLooseContainer(items[0].Template, items[0].Id, staticAmmoDist, modifier);

            foreach (var containerItem in containerLoot)
            {
                items.Add(containerItem);
            }
        }

        public List<Item> CreateLootInLooseContainer(
            string tpl,
            MongoId id,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            double modifier = 0.5
        )
        {
            if (modifier == 0)
            {
                return [];
            }

            DatabaseTables tables = databaseService.GetTables();
            Dictionary<MongoId, TemplateItem>? items = databaseService.GetTables()?.Templates?.Items;

            if (!items.TryGetValue(tpl, out var item))
            {
                logger.Warning($"Template {tpl} not found in database.");
                return [];
            }

            // Ensure filters exist for item
            var firstGrid = item.Properties?.Grids?.FirstOrDefault();
            if (firstGrid == null)
            {
                return [];
            }

            var firstFilter = firstGrid.Properties?.Filters?.FirstOrDefault();

            if (firstFilter == null || !firstGrid.Properties.Filters.Any())
            {
                firstGrid.Properties.Filters = [new GridFilter { Filter = ["54009119af1c881c07000029"], ExcludedFilter = [] }];
                firstFilter = firstGrid.Properties.Filters.First(); // reset after assigning
            }

            // Clone filters
            List<MongoId> whitelist = [.. firstFilter.Filter];
            HashSet<MongoId> blacklist = [.. firstFilter.ExcludedFilter ?? []];

            int maxCells = (int)(firstGrid.Properties.CellsH * firstGrid.Properties.CellsV);
            int amount = randomUtil.GetInt(1, (int)(maxCells * modifier));

            // Use cache for whitelist if available, if not available, generate new cache
            if (!_itemFilterIndexCache.TryGetValue(tpl, out var cachedWhiteList))
            {
                // Expand items with children
                whitelist = ExpandItemsWithChildItemIds(whitelist, items);
                List<MongoId> expandedBlacklist = ExpandItemsWithChildItemIds(blacklist.ToList(), items);
                blacklist.UnionWith(expandedBlacklist);

                // Add config blacklist
                if (config.LotsOfLootConfig.LootinLooseContainer.Blacklist.TryGetValue(tpl, out var configBlacklist))
                {
                    blacklist.UnionWith(configBlacklist);
                }

                // Filter whitelist - single pass instead of multiple Where calls
                whitelist = whitelist
                    .Where(itemTpl =>
                        !blacklist.Contains(itemTpl)
                        && !itemHelper.IsOfBaseclass(itemTpl, BaseClasses.BUILT_IN_INSERTS)
                        && !itemFilterService.IsItemBlacklisted(itemTpl)
                        && !itemFilterService.IsItemRewardBlacklisted(itemTpl)
                        && itemHelper.IsValidItem(itemTpl)
                        && items.TryGetValue(itemTpl, out var itm)
                        && !string.IsNullOrEmpty(itm.Properties.Prefab?.Path)
                    )
                    .ToList();

                // Cache whitelist
                _itemFilterIndexCache[tpl] = whitelist;
            }
            else
            {
                whitelist = cachedWhiteList;
            }

            if (whitelist.Count == 0)
            {
                logger.Warning($"{tpl} whitelist is empty");
                return [];
            }

            // Build probability array with improved weight calculation
            ProbabilityObjectArray<MongoId, int?> itemArray = new(cloner);
            Dictionary<MongoId, double>? prices = tables.Templates?.Prices;

            foreach (var itemId in whitelist)
            {
                int itemWeight = 1;

                if (itemId == "5449016a4bdc2d6f028b456f")
                {
                    itemWeight = 500;
                }
                else if (_foreignCurrencies.Contains(itemId))
                {
                    itemWeight = 100;
                }
                else if (prices.TryGetValue(itemId, out var price))
                {
                    itemWeight = (int)Math.Round(1000 / Math.Pow(price, 1.0 / 3.0));
                }

                itemArray.Add(new ProbabilityObject<MongoId, int?>(itemId, itemWeight, null));
            }

            // Generate loot items
            List<Item> generatedItems = [];
            LootInLooseContainerSpawnLimit? limits = config.LotsOfLootConfig.LootinLooseContainer.SpawnLimits.TryGetValue(
                tpl,
                out LootInLooseContainerSpawnLimit? lim
            )
                ? lim
                : null;

            int fill = 0;
            int drawnKeys = 0;
            int drawnKeycards = 0;

            while (fill <= amount)
            {
                // Since we modify these with limits, check each loop if they are empty and if so break out from the while loop
                if (itemArray.Count == 0 || whitelist.Count == 0)
                {
                    break;
                }

                // Handle if we should draw an item from the ProbabilityObjectArray (Weighted) or from the whitelist
                string drawnItemTpl = config.LotsOfLootConfig.General.ItemWeights
                    ? itemArray.DrawAndRemove(1).FirstOrDefault()
                    : whitelist[randomUtil.GetInt(0, whitelist.Count - 1)];

                // Check limits if they exist
                if (limits != null)
                {
                    if (limits.Keys.HasValue && itemHelper.IsOfBaseclass(drawnItemTpl, BaseClasses.KEY_MECHANICAL))
                    {
                        if (drawnKeys < limits.Keys.Value)
                        {
                            drawnKeys++;
                        }
                        else
                        {
                            if (config.LotsOfLootConfig.General.ItemWeights)
                            {
                                itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEY_MECHANICAL));
                            }
                            else
                            {
                                whitelist = whitelist.Where(i => !itemHelper.IsOfBaseclass(i, BaseClasses.KEY_MECHANICAL)).ToList();
                            }
                        }
                    }

                    if (limits.Keycards.HasValue && itemHelper.IsOfBaseclass(drawnItemTpl, BaseClasses.KEYCARD))
                    {
                        if (drawnKeycards < limits.Keycards.Value)
                        {
                            drawnKeycards++;
                        }
                        else
                        {
                            if (config.LotsOfLootConfig.General.ItemWeights)
                            {
                                itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEYCARD));
                            }
                            else
                            {
                                whitelist = whitelist.Where(i => !itemHelper.IsOfBaseclass(i, BaseClasses.KEYCARD)).ToList();
                            }
                        }
                    }
                }

                ContainerItem lootItem = CreateStaticLootItem(drawnItemTpl, staticAmmoDist, id);
                lootItem.Items.First().SlotId = "main";
                fill += (int)(lootItem.Height * lootItem.Width);

                if (fill > amount)
                {
                    break;
                }

                generatedItems.AddRange(lootItem.Items);
            }

            return generatedItems;
        }

        private List<MongoId> ExpandItemsWithChildItemIds(List<MongoId> itemsToExpand, Dictionary<MongoId, TemplateItem> items)
        {
            List<MongoId> expandedItems = [];

            foreach (MongoId content in itemsToExpand)
            {
                List<MongoId> childItems = LotsofLootItemHelper.FindAndReturnChildItemIdsByItems(items, content);
                expandedItems.AddRange(childItems);
            }

            return expandedItems;
        }
    }
}
