using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LotsofLoot.Generators;

public interface ILootItemCreator
{
    public bool CanCreateItem(MongoId tpl);
    public void CreateItem(
        List<Item> items,
        TemplateItem templateItem,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDictionary,
        LotsofLootLocationLootGenerator context
    );
}
