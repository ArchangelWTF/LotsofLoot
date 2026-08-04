using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class MagazineItemCreator(LocationConfig locationConfig, ItemHelper itemHelper, RandomUtil randomUtil) : ILootItemCreator
{
    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.MAGAZINE))
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
        if (!randomUtil.GetChance100(locationConfig.MagazineLootHasAmmoChancePercent))
        {
            return;
        }

        List<Item> magazineWithCartridges = [items[0]];

        itemHelper.FillMagazineWithRandomCartridge(
            magazineWithCartridges,
            templateItem,
            staticAmmoDictionary,
            null,
            locationConfig.MinFillStaticMagazinePercent / 100.0
        );

        items.RemoveAt(0);
        items.InsertRange(0, magazineWithCartridges);
    }
}
