using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class PriceCorrection(DatabaseServer databaseServer) : IOnPresetUpdate
{
    private readonly Dictionary<MongoId, double?> _backupPriceCorrection = [];

    public void Apply(LotsofLootPresetConfig preset)
    {
        Templates databaseTemplates = databaseServer.GetTables().Templates;

        foreach ((MongoId itemId, double adjustedPrice) in preset.General.PriceCorrection)
        {
            if (!_backupPriceCorrection.ContainsKey(itemId))
            {
                if (databaseTemplates.Prices.TryGetValue(itemId, out double value))
                {
                    _backupPriceCorrection[itemId] = value;
                }
                else
                {
                    _backupPriceCorrection[itemId] = null;
                }

                databaseTemplates.Prices[itemId] = adjustedPrice;
            }
        }
    }

    public void Revert()
    {
        Templates databaseTemplates = databaseServer.GetTables().Templates;

        foreach ((MongoId itemId, double? backupPrice) in _backupPriceCorrection)
        {
            if (backupPrice is not null)
            {
                databaseTemplates.Prices[itemId] = backupPrice.Value;
            }
            else
            {
                databaseTemplates.Prices.Remove(itemId);
            }
        }
    }
}
