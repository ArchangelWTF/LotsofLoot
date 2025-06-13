using LotsofLoot.Models.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils;
using System.Reflection;

namespace LotsofLoot.Services
{
    [Injectable(InjectionType.Singleton)]
    public class ConfigService(ModHelper modHelper, JsonUtil jsonUtil, ILogger<ConfigService> logger)
    {
        public LotsOfLootConfig LotsOfLootConfig { get; private set; } = new();

        public string GetModPath()
        {
            return modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        }

        public async Task LoadAsync()
        {
            LotsOfLootConfig? loadedConfig = await jsonUtil.DeserializeFromFileAsync<LotsOfLootConfig>(Path.Join(GetModPath(), "config/config.jsonc"));

            if (loadedConfig != null)
            {
                LotsOfLootConfig = loadedConfig;
            }
            else
            {
                logger.LogWarning("[Lots of Loot] No config file found, loading defaults!");
            }

        }
    }
}
