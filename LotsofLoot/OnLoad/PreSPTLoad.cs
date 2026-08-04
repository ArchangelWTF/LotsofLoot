using LotsofLoot.Overrides.Generators;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;

namespace LotsofLoot.OnLoad;

[Injectable(TypePriority = OnLoadOrder.Preload + LotsofLootModMetadata.LotsofLootPriorityOffset)]
public class PreSPTLoad(IEnumerable<IRuntimePatch> patches, ConfigService configService) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        foreach (var patch in patches)
        {
            patch.Enable();
        }

        await configService.LoadAsync(cancellationToken);
    }
}
