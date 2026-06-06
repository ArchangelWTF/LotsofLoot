using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class ArmorItemModsGenerator(
    ConfigServer configServer,
    ItemHelper itemHelper,
    PresetHelper presetHelper,
    ILogger<ArmorItemModsGenerator> logger
) : ILootItemCreator
{
    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

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
        Preset? defaultPreset = presetHelper.GetDefaultPreset(templateItem.Id);

        if (defaultPreset != null)
        {
            List<Item> presetAndMods = defaultPreset.Items.ReplaceIDs().ToList();
            presetAndMods.RemapRootItemId();
            presetAndMods[0].ParentId = items[0].ParentId;

            if (presetAndMods.Count > 0)
            {
                items.Clear();
                items.AddRange(presetAndMods);
            }
            return;
        }

        if (templateItem.Properties.Slots?.Count() > 0)
        {
            List<Item> itemsWithChildren = itemHelper.AddChildSlotItems(
                items,
                templateItem,
                _locationConfig.EquipmentLootSettings.ModSpawnChancePercent
            );

            if (itemsWithChildren.Count > 0)
            {
                items.Clear();
                items.AddRange(itemsWithChildren);
            }
        }
    }
}
