using System.Reflection;
using LotsofLoot.Models.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils;

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

        public string GetConfigPath()
        {
            return Path.Combine(GetModPath(), "config", "config.jsonc");
        }

        public async Task LoadAsync()
        {
            string configPath = GetConfigPath();
            string configDir = Path.GetDirectoryName(configPath)!;

            LotsOfLootConfig? loadedConfig = await jsonUtil.DeserializeFromFileAsync<LotsOfLootConfig>(configPath);

            if (loadedConfig != null)
            {
                LotsOfLootConfig = loadedConfig;
            }
            else
            {
                logger.LogWarning("[Lots of Loot] No config file found, loading defaults!");

                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // Todo: This is still kind of bad as the comments for configs don't get carried over
                await File.WriteAllTextAsync(configPath, jsonUtil.Serialize(LotsOfLootConfig, true));
            }
        }
    }
}
