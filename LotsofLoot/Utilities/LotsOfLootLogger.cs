using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Utilities;

[Injectable]
public class LotsOfLootLogger(ISptLogger<LotsOfLootLogger> logger)
{
    private const string LotsofLootPrefix = "[Lots of Loot Redux]";

    public void Success(string data, Exception? ex = null)
    {
        logger.Success(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public void Error(string data, Exception? ex = null)
    {
        logger.Error(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public void Warning(string data, Exception? ex = null)
    {
        logger.Warning(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public void Info(string data, Exception? ex = null)
    {
        logger.Info(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public void Debug(string data, Exception? ex = null)
    {
        logger.Debug(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public void Critical(string data, Exception? ex = null)
    {
        logger.Critical(string.Concat(LotsofLootPrefix, " ", data), ex);
    }

    public bool IsDebug()
    {
        if (ProgramStatics.DEBUG() || ConfigService.LotsofLootConfig.IsDebugEnabled)
        {
            return true;
        }

        return false;
    }
}
