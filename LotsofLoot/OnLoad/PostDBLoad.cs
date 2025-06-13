using LotsofLoot.Helpers;
using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace LotsofLoot.OnLoad
{
    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader)]
    public class PostDBLoad(ModificationHelper modificationHelper, LazyLoadHandlerService lazyLoadHandlerService,
        ItemHelper itemHelper, DatabaseServer databaseServer, ConfigServer configServer,
        ConfigService configService, LotsOfLootLogger logger) : IOnLoad
    {
        private readonly LocationConfig _locationConfig = configServer.GetConfig<LocationConfig>();

        public Task OnLoad()
        {
            if (configService.LotsOfLootConfig.General.RemoveBackpackRestrictions)
            {
                modificationHelper.RemoveBackpackRestrictions();
            }

            foreach ((string map, int multiplier) in configService.LotsOfLootConfig.LooseLootMultiplier)
            {
                // When allow loot overlay is disabled, amplify the loose loot ever so slightly so more items spawn in other spawn points.
                if (!configService.LotsOfLootConfig.General.AllowLootOverlay)
                {
                    _locationConfig.LooseLootMultiplier[map] = Math.Round(multiplier * 1.5);
                }
                else
                {
                    _locationConfig.LooseLootMultiplier[map] = multiplier;
                }
                logger.Debug($"{map}: {multiplier}");

                _locationConfig.StaticLootMultiplier[map] = configService.LotsOfLootConfig.StaticLootMultiplier[map];
                logger.Debug($"{map}: {configService.LotsOfLootConfig.StaticLootMultiplier[map]}");
            }

            lazyLoadHandlerService.OnPostDBLoad();

            if (configService.LotsOfLootConfig.General.DisableFleaRestrictions)
            {
                foreach ((string templateId, TemplateItem template) in databaseServer.GetTables().Templates.Items)
                {
                    if (itemHelper.IsValidItem(template.Id))
                    {
                        template.Properties.CanRequireOnRagfair = true;
                        template.Properties.CanSellOnRagfair = true;
                    }
                }
            }

            foreach ((string itemId, long adjustedPrice) in configService.LotsOfLootConfig.General.PriceCorrection)
            {
                databaseServer.GetTables().Templates.Prices[itemId] = adjustedPrice;
            }

            return Task.CompletedTask;
        }
    }
}
