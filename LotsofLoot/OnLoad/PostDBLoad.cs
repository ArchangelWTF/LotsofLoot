using LotsofLoot.Models.Preset;
using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace LotsofLoot.OnLoad;

//Todo: Merge this into PreLoad
[Injectable(TypePriority = OnLoadOrder.PostLoad + LotsofLootModMetadata.LotsofLootPriorityOffset)]
public class PostDBLoad(
    ConfigService configService,
    LocaleCacheService localeCacheService,
    LazyLoadHandlerService lazyLoadHandlerService,
    IEnumerable<IOnPresetUpdate> onPresetUpdates
) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        localeCacheService.HydrateCache();

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
