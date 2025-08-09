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
using SPTarkov.Server.Core.Models.Enums;
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
    public class LotsofLootLocationLootGenerator(ItemHelper itemHelper, PresetHelper presetHelper, ItemFilterService itemFilterService,
        HashUtil hashUtil, RandomUtil randomUtil, ICloner cloner, LotsofLootItemHelper LotsofLootItemHelper,
        LotsOfLootLogger logger, ConfigService config, DatabaseService databaseService, ConfigServer configServer)
    {
        private readonly Dictionary<string, List<MongoId>> _itemFilterIndexCache = [];
        private readonly HashSet<MongoId> _foreignCurrencies =
        [
            // Dollars
            "5696686a4bdc2da3298b456a",
            // Euros
            "569668774bdc2da2298b4568"
         ];

        /// <summary>
        /// This method closely mirrors that of SPT
        /// The only difference being the bypass for loot overlay and using Lots of Loot's createStaticLootItem
        /// </summary>
        public List<SpawnpointTemplate> GenerateDynamicLoot(LooseLoot dynamicLootDist, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist, string locationName)
        {
            return [];
        }

        //Todo: Fully needs implementing
        public Spawnpoint? HandleSpawningAlwaysSpawnSpawnpoint(List<Spawnpoint> spawnpoints, string location)
        {
            return null;
        }

        public ContainerItem CreateStaticLootItem(string chosenTpl, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            string? parentId = null)
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
                new() {
                    Id = new MongoId(),
                    Template = chosenTpl,
                    ParentId = parentId,
                }
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
                int stackCount = randomUtil.GetInt((int)itemTemplate.Properties.StackMinRandom, (int)itemTemplate.Properties.StackMaxRandom);
                items[0].Upd = new Upd
                {
                    StackObjectsCount = stackCount 
                };
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
                    items = itemHelper.AddChildSlotItems(items, itemTemplate, configServer.GetConfig<LocationConfig>().EquipmentLootSettings.ModSpawnChancePercent);
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
                    defaultWeapon);

                // Replace the original magazine with the filled version
                var magIndex = items.IndexOf(magazine);
                items.RemoveAt(magIndex);
                items.InsertRange(magIndex, magazineWithCartridges);
            }

            return items;
        }

        private void HandleMagazineItem(List<Item> items, TemplateItem itemTemplate, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist)
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
                configServer.GetConfig<LocationConfig>().MinFillStaticMagazinePercent / 100.0);

            items.RemoveAt(0);
            items.InsertRange(0, magazineWithCartridges);
        }

        private void HandleContainerOrBackpackItem(List<Item> items, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist, double modifier)
        {
            List<Item> containerLoot = CreateLootInLooseContainer(items[0].Template, items[0].Id, staticAmmoDist, modifier);

            foreach (var containerItem in containerLoot)
            {
                items.Add(containerItem);
            }
        }

        public List<Item> CreateLootInLooseContainer(string tpl, MongoId id, Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist, double modifier = 0.5)
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
            var firstGrid = item.Properties.Grids.FirstOrDefault();
            if (firstGrid == null)
                return [];

            var firstFilter = firstGrid.Props?.Filters?.FirstOrDefault();

            if (firstFilter == null || !firstGrid.Props.Filters.Any())
            {
                logger.Debug($"{item.Name} doesn't have a filter, setting default filter!");
                firstGrid.Props.Filters =
                [
                    new GridFilter
                    {
                        Filter = ["54009119af1c881c07000029" ],
                        ExcludedFilter = []
                    }
                ];
                firstFilter = firstGrid.Props.Filters.First(); // reset after assigning
            }

            // Clone filters
            List<MongoId> whitelist = [.. firstFilter.Filter];
            HashSet<MongoId> blacklist = [.. firstFilter.ExcludedFilter ?? []];

            int maxCells = (int)(firstGrid.Props.CellsH * firstGrid.Props.CellsV);
            int amount = randomUtil.GetInt(1, (int)(maxCells * modifier));

            // Use cache for whitelist if available, if not available, generate new cache
            if (!_itemFilterIndexCache.TryGetValue(tpl, out var cachedWhiteList))
            {
                logger.Debug($"{tpl} is new, generating whitelist");

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
                whitelist = whitelist.Where(itemTpl =>
                    !blacklist.Contains(itemTpl) &&
                    !itemHelper.IsOfBaseclass(itemTpl, BaseClasses.BUILT_IN_INSERTS) &&
                    !itemFilterService.IsItemBlacklisted(itemTpl) &&
                    !itemFilterService.IsItemRewardBlacklisted(itemTpl) &&
                    itemHelper.IsValidItem(itemTpl) &&
                    items.TryGetValue(itemTpl, out var itm) &&
                    !string.IsNullOrEmpty(itm.Properties.Prefab?.Path)
                ).ToList();

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
            LootInLooseContainerSpawnLimit? limits = config.LotsOfLootConfig.LootinLooseContainer.SpawnLimits.TryGetValue(tpl, out LootInLooseContainerSpawnLimit? lim) ? lim : null;

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
                        if (drawnKeys >= limits.Keys.Value)
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
                        else
                        {
                            drawnKeys++;
                        }
                    }

                    if (limits.Keycards.HasValue && itemHelper.IsOfBaseclass(drawnItemTpl, BaseClasses.KEYCARD))
                    {
                        if (drawnKeycards >= limits.Keycards.Value)
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
                        else
                        {
                            drawnKeycards++;
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
