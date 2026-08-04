using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class FleaRestrictions(TemplateTable templateTable, ItemHelper itemHelper) : IOnPresetUpdate
{
    public void Apply(LotsofLootPresetConfig preset)
    {
        if (preset.General.DisableFleaRestrictions)
        {
            foreach ((_, TemplateItem template) in templateTable.Items)
            {
                if (itemHelper.IsValidItem(template.Id) && template.Properties is not null)
                {
                    template.Properties.CanRequireOnRagfair = true;
                    template.Properties.CanSellOnRagfair = true;
                }
            }
        }
    }

    public void Revert()
    {
        //Todo: Implement
    }
}
