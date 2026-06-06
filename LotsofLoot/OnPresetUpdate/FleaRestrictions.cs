using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class FleaRestrictions(DatabaseServer databaseServer, ItemHelper itemHelper) : IOnPresetUpdate
{
    public void Apply(LotsofLootPresetConfig preset)
    {
        if (preset.General.DisableFleaRestrictions)
        {
            Templates databaseTemplates = databaseServer.GetTables().Templates;

            foreach ((_, TemplateItem template) in databaseTemplates.Items)
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
