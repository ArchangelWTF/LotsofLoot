using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class WeaponItemCreator(
    ConfigServer configServer,
    ItemHelper itemHelper,
    PresetHelper presetHelper,
    NewSPTRandomUtil randomUtil,
    ICloner cloner
) : ILootItemCreator
{
    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.WEAPON))
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
        Item rootItem = items[0];

        // Get the original weapon preset
        Preset? weaponPreset = cloner.Clone(presetHelper.GetDefaultPreset(templateItem.Id));
        if (weaponPreset?.Items != null)
        {
            List<Item> itemWithChildren = itemHelper.ReparentItemAndChildren(weaponPreset.Items[0], weaponPreset.Items);

            if (itemWithChildren != null && itemWithChildren.Count > 0)
            {
                var newItems = itemHelper.ReparentItemAndChildren(rootItem, itemWithChildren);

                // Clear the original list and re-add the reparented one
                items.Clear();
                items.AddRange(newItems);
            }
        }

        Item? magazine = items.Find(x => x.SlotId == "mod_magazine");

        if (magazine != null && randomUtil.GetChance100(_locationConfig.MagazineLootHasAmmoChancePercent))
        {
            // Get required templates
            TemplateItem? magTemplate = itemHelper.GetItem(magazine.Template).Value;
            TemplateItem? weaponTemplate = itemHelper.GetItem(templateItem.Id).Value;
            TemplateItem? defaultWeapon = itemHelper.GetItem(rootItem.Template).Value;

            // Fill the magazine with cartridges
            List<Item> magazineWithCartridges = [magazine];

            itemHelper.FillMagazineWithRandomCartridge(
                magazineWithCartridges,
                magTemplate,
                staticAmmoDictionary,
                weaponTemplate.Properties.AmmoCaliber,
                _locationConfig.MinFillStaticMagazinePercent / 100.0,
                defaultWeapon.Properties.DefAmmo,
                defaultWeapon
            );

            // Replace the original magazine with the filled version
            var magIndex = items.IndexOf(magazine);
            items.RemoveAt(magIndex);
            items.InsertRange(magIndex, magazineWithCartridges);
        }
    }
}
