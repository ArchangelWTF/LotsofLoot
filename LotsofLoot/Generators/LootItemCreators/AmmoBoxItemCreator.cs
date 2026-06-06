using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class AmmoBoxItemCreator(ItemHelper itemHelper) : ILootItemCreator
{
    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.AMMO_BOX))
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
        itemHelper.AddCartridgesToAmmoBox(items, templateItem);
    }
}
