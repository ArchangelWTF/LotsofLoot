using LotsofLoot.Models.Preset;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LotsofLoot.OnPresetUpdate;

[Injectable(InjectionType.Singleton)]
public sealed class PriceCorrection(TemplateTable templateTable) : IOnPresetUpdate
{
    private readonly Dictionary<MongoId, double?> _backupPriceCorrection = [];

    public void Apply(LotsofLootPresetConfig preset)
    {
        foreach ((MongoId itemId, double adjustedPrice) in preset.General.PriceCorrection)
        {
            if (!_backupPriceCorrection.ContainsKey(itemId))
            {
                if (templateTable.Prices.TryGetValue(itemId, out double value))
                {
                    _backupPriceCorrection[itemId] = value;
                }
                else
                {
                    _backupPriceCorrection[itemId] = null;
                }

                templateTable.Prices[itemId] = adjustedPrice;
            }
        }
    }

    public void Revert()
    {
        foreach ((MongoId itemId, double? backupPrice) in _backupPriceCorrection)
        {
            if (backupPrice is not null)
            {
                templateTable.Prices[itemId] = backupPrice.Value;
            }
            else
            {
                templateTable.Prices.Remove(itemId);
            }
        }
    }
}
