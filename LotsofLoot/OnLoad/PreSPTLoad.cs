using LotsofLoot.Overrides.Generators;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;

namespace LotsofLoot.OnLoad
{
    [Injectable(TypePriority = OnLoadOrder.PreSptModLoader + LotsofLootLoadPriority.LotsofLootPriorityOffset)]
    public class PreSPTLoad(ConfigService configService, LotsOfLootLogger logger) : IOnLoad
    {
        private bool _overridesInjected = false;
        private readonly List<AbstractPatch> _patches = [new GenerateDynamicLootOverride(), new GenerateStaticLootOverride()];

        private void InjectOverrides()
        {
            if (_overridesInjected)
            {
                return;
            }

            try
            {
                foreach (AbstractPatch patch in _patches)
                {
                    logger.Debug($"Loading patch: {patch.GetType().Name}");
                    patch.Enable();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error applying patch: {ex.Message}");
                throw;
            }

            _overridesInjected = true;
        }

        public async Task OnLoad()
        {
            InjectOverrides();

            await configService.LoadAsync();
        }
    }
}
