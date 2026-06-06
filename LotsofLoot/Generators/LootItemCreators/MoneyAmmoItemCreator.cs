using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LotsofLoot.Generators.LootItemCreators;

[Injectable]
public class MoneyAmmoItemCreator(ItemHelper itemHelper, NewSPTRandomUtil randomUtil) : ILootItemCreator
{
    public bool CanCreateItem(MongoId tpl)
    {
        if (itemHelper.IsOfBaseclass(tpl, BaseClasses.MONEY) || itemHelper.IsOfBaseclass(tpl, BaseClasses.AMMO))
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
        int stackCount = randomUtil.GetInt((int)templateItem.Properties.StackMinRandom, (int)templateItem.Properties.StackMaxRandom);

        items[0].Upd = new Upd { StackObjectsCount = stackCount };
    }
}
