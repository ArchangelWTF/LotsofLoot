using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Utilities
{
    [Injectable]
    public class LotsOfLootLogger(ISptLogger<LotsOfLootLogger> logger, DatabaseService databaseService, ConfigService configService)
    {
        public void Success(string data, Exception? ex = null)
        {
            logger.Success($"[Lots of Loot Redux] {data}", ex);
        }

        public void Error(string data, Exception? ex = null)
        {
            logger.Error($"[Lots of Loot Redux] {data}", ex);
        }

        public void Warning(string data, Exception? ex = null)
        {
            logger.Warning($"[Lots of Loot Redux] {data}", ex);
        }

        public void Info(string data, Exception? ex = null)
        {
            logger.Info($"[Lots of Loot Redux] {data}", ex);
        }

        public void Debug(string data, Exception? ex = null)
        {
            logger.Debug($"[Lots of Loot Redux] {data}", ex);
        }

        public void Critical(string data, Exception? ex = null)
        {
            logger.Critical($"[Lots of Loot Redux] {data}", ex);
        }

        public string WriteItemName(string itemId, bool writeTpl = false)
        {
            var enLocale = databaseService.GetLocales().Global["en"];
            string itemName = enLocale?.Value?[$"{itemId} Name"] ?? "Unknown";

            if (writeTpl)
            {
                return $"{itemName}({itemId})";
            }
            else
            {
                return itemName;
            }
        }

        public bool IsDebug()
        {
            if (ProgramStatics.DEBUG() || configService.LotsOfLootConfig.General.IsDebugEnabled)
            {
                return true;
            }

            return false;
        }
    }
}
