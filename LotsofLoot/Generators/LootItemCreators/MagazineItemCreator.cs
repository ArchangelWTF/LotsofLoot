using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class MagazineItemCreator(ConfigServer configServer, ItemHelper itemHelper, NewSPTRandomUtil randomUtil) : ILootItemCreator
{
    private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

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
        if (!randomUtil.GetChance100(_locationConfig.MagazineLootHasAmmoChancePercent))
        {
            return;
        }

        List<Item> magazineWithCartridges = [items[0]];

        itemHelper.FillMagazineWithRandomCartridge(
            magazineWithCartridges,
            templateItem,
            staticAmmoDictionary,
            null,
            _locationConfig.MinFillStaticMagazinePercent / 100.0
        );

        items.RemoveAt(0);
        items.InsertRange(0, magazineWithCartridges);
    }
}
