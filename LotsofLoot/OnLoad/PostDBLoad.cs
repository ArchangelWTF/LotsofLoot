using LotsofLoot.Models.Preset;
using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnLoad;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + LotsofLootModMetadata.LotsofLootPriorityOffset)]
public class PostDBLoad(
    ConfigService configService,
    DatabaseServer databaseServer,
    LazyLoadHandlerService lazyLoadHandlerService,
    IEnumerable<IOnPresetUpdate> onPresetUpdates
) : IOnLoad
{
    public Task OnLoad()
    {
        Templates? databaseTemplates = databaseServer.GetTables().Templates;

        if (databaseTemplates is null || databaseTemplates.Items is null || databaseTemplates.Prices is null)
        {
            throw new InvalidOperationException("Database templates are null?");
        }

        // This only needs initialisation once, it will get the current values out of the config service when a raid is loaded
        lazyLoadHandlerService.OnPostDBLoad();

        // Apply the currently loaded preset
        foreach (IOnPresetUpdate presetUpdate in onPresetUpdates)
        {
            presetUpdate.Apply(configService.LotsofLootPresetConfig);
        }

        return Task.CompletedTask;
    }
}
