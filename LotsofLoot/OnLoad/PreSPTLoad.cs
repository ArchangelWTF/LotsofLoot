using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace LotsofLoot.OnLoad
{
    [Injectable(TypePriority = OnLoadOrder.PreSptModLoader)]
    public class PreSPTLoad(ConfigService configService) : IOnLoad
    {
        public async Task OnLoad()
        {
            await configService.LoadAsync();
        }
    }
}
