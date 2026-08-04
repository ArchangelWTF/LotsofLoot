using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class ArmorItemModsGenerator(
    LocationConfig locationConfig,
    ItemHelper itemHelper,
    PresetHelper presetHelper,
    ICloner cloner,
    ISptLogger<ArmorItemModsGenerator> logger
) : ILootItemCreator
{
    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.ArmorItemCanHoldMods(tpl))
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
        Preset? defaultPreset = cloner.Clone(presetHelper.GetDefaultPreset(templateItem.Id));

        if (defaultPreset != null)
        {
            List<Item> presetAndMods = defaultPreset.Items.ReplaceIDs().ToList();

            if (presetAndMods.Count == 0)
            {
                if (logger.IsLogEnabled(LogLevel.Warning))
                {
                    logger.Warning($"Template {templateItem.Id} has an empty preset! Unable to generate item mods!");
                }

                return;
            }

            presetAndMods.RemapRootItemId();
            presetAndMods[0].ParentId = items[0].ParentId;

            items.Clear();
            items.AddRange(presetAndMods);

            return;
        }

        if (templateItem.Properties is not null && templateItem.Properties.Slots?.Count() > 0)
        {
            List<Item> itemsWithChildren = itemHelper
                .AddChildSlotItems(items, templateItem, locationConfig.EquipmentLootSettings.ModSpawnChancePercent)
                .ToList();

            if (itemsWithChildren.Count > 0)
            {
                items.Clear();
                items.AddRange(itemsWithChildren);
            }
            else
            {
                if (logger.IsLogEnabled(LogLevel.Warning))
                {
                    logger.Warning($"Template {templateItem.Id} generated no armor child slot items!");
                }
            }
        }
    }
}
