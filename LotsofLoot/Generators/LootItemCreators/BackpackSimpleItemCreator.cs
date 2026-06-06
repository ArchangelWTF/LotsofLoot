using LotsofLoot.Helpers;
using LotsofLoot.Models.Preset;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class BackpackSimpleItemCreator(
    ConfigService configService,
    DatabaseService databaseService,
    ItemFilterService itemFilterService,
    ItemHelper itemHelper,
    LotsofLootItemHelper lotsofLootItemHelper,
    LotsOfLootLogger logger,
    NewSPTRandomUtil randomUtil,
    ICloner cloner
) : ILootItemCreator
{
    private static readonly Dictionary<string, List<MongoId>> _itemFilterIndexCache = [];
    private static readonly HashSet<MongoId> _foreignCurrencies =
    [
        // Dollars
        "5696686a4bdc2da3298b456a",
        // Euros
        "569668774bdc2da2298b4568",
    ];

    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER) && tpl != "5c093e3486f77430cb02e593")
        {
            return true;
        }

        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.BACKPACK))
        {
            return true;
        }

        return false;
    }

    public void CreateItem(
        List<Item> items,
        TemplateItem templateItem,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDictionary,
        LotsofLootLocationLootGenerator context
    )
    {
        List<Item> containerLoot = CreateLootInLooseContainer(
            items[0].Template,
            items[0].Id,
            staticAmmoDictionary,
            context,
            configService.LotsofLootPresetConfig.LootinLooseContainer.LootInContainerModifier
        );

        foreach (var containerItem in containerLoot)
        {
            items.Add(containerItem);
        }
    }

    public List<Item> CreateLootInLooseContainer(
        string tpl,
        MongoId id,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
        LotsofLootLocationLootGenerator context,
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
            if (configService.LotsofLootPresetConfig.LootinLooseContainer.Blacklist.TryGetValue(tpl, out var configBlacklist))
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
        LootInLooseContainerSpawnLimit? limits = configService.LotsofLootPresetConfig.LootinLooseContainer.SpawnLimits.TryGetValue(
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
            string drawnItemTpl = configService.LotsofLootPresetConfig.General.ItemWeights
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
                        if (configService.LotsofLootPresetConfig.General.ItemWeights)
                        {
                            itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEY_MECHANICAL));
                        }
                        else
                        {
                            whitelist = whitelist.Where(i => !itemHelper.IsOfBaseclass(i, BaseClasses.KEY_MECHANICAL)).ToList();
                        }

                        // Continue here as else we would still add this item, if the limit would be 3 it would put 4 in.
                        continue;
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
                        if (configService.LotsofLootPresetConfig.General.ItemWeights)
                        {
                            itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEYCARD));
                        }
                        else
                        {
                            whitelist = whitelist.Where(i => !itemHelper.IsOfBaseclass(i, BaseClasses.KEYCARD)).ToList();
                        }

                        // Continue here as else we would still add this item, if the limit would be 3 it would put 4 in.
                        continue;
                    }
                }
            }

            ContainerItem lootItem = context.CreateStaticLootItem(drawnItemTpl, staticAmmoDist, id);
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
            List<MongoId> childItems = lotsofLootItemHelper.FindAndReturnChildItemIdsByItems(items, content);
            expandedItems.AddRange(childItems);
        }

        return expandedItems;
    }
}
