using LotsofLoot.Helpers;
using LotsofLoot.Models.Preset;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class BackpackSimpleItemCreator(
    ConfigService configService,
    TemplateTable templateTable,
    ItemFilterService itemFilterService,
    ItemHelper itemHelper,
    RandomUtil randomUtil,
    LotsofLootItemHelper lotsofLootItemHelper,
    LotsOfLootLogger logger,
    ICloner cloner
) : ILootItemCreator
{
    private static readonly Dictionary<string, List<MongoId>> _itemFilterIndexCache = [];

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

        Dictionary<MongoId, TemplateItem>? items = templateTable.Items;

        if (items is null)
        {
            throw new InvalidOperationException("Could not generate item, the item templates database is empty?");
        }

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

            // Cache only the container's structural filters. Preset and item validity checks must stay live.
            whitelist = whitelist.Where(itemTpl => !blacklist.Contains(itemTpl)).Distinct().ToList();

            // Cache whitelist
            _itemFilterIndexCache[tpl] = whitelist;
        }
        else
        {
            whitelist = cachedWhiteList;
        }

        var preset = configService.LotsofLootPresetConfig;
        HashSet<MongoId> configBlacklist = [.. preset.LootinLooseContainer.Blacklist.GetValueOrDefault(tpl) ?? []];
        var candidates = whitelist
            .Where(itemTpl =>
                !configBlacklist.Contains(itemTpl)
                && !itemHelper.IsOfBaseclass(itemTpl, BaseClasses.BUILT_IN_INSERTS)
                && !itemFilterService.IsItemBlacklisted(itemTpl)
                && !itemFilterService.IsLootableItemBlacklisted(itemTpl)
                && !itemFilterService.IsItemRewardBlacklisted(itemTpl)
                && items.TryGetValue(itemTpl, out var itm)
                && itemHelper.IsValidItem(itm)
                && !string.IsNullOrEmpty(itm.Properties?.Prefab?.Path)
            )
            .Select(itemTpl => items[itemTpl]);

        var itemArray = LotsofLootItemHelper.CreateLooseContainerLootPool(
            candidates,
            templateTable.Prices,
            preset.ChangeRelativeProbabilityInPool,
            preset.General.ItemWeights,
            cloner
        );

        if (itemArray.Count == 0)
        {
            logger.Warning($"{tpl} whitelist is empty");
            return [];
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
            if (itemArray.Count == 0)
            {
                break;
            }

            // Explicit multipliers apply in both modes; ItemWeights controls the base price weighting.
            string drawnItemTpl = preset.General.ItemWeights
                ? itemArray.DrawAndRemove(1).FirstOrDefault()
                : itemArray.Draw().FirstOrDefault();

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
                        itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEY_MECHANICAL));

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
                        itemArray = itemArray.Filter(i => !itemHelper.IsOfBaseclass(i.Key, BaseClasses.KEYCARD));

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
