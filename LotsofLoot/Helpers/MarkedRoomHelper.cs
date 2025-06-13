using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

namespace LotsofLoot.Helpers
{
    [Injectable]
    public class MarkedRoomHelper(ConfigService configService, HashUtil hashUtil, ItemHelper itemHelper, LotsOfLootLogger logger)
    {
        public void AdjustMarkedRooms(string locationId, Spawnpoint spawnpoint)
        {
            if (spawnpoint.IsMarkedRoomSpawnpoint(locationId.ToLower()))
            {
                logger.Debug($"Marked room ({locationId}) {spawnpoint.Template.Id}");
                spawnpoint.Probability *= configService.LotsOfLootConfig.MarkedRoomConfig.Multiplier[locationId.ToLower()];
                AddExtraItemsToMarkedRoom(spawnpoint);
                AdjustMarkedRoomItemGroups(spawnpoint);
            }
        }

        private void AddExtraItemsToMarkedRoom(Spawnpoint spawnpoint)
        {
            foreach((string itemTpl, int relativeProbability) in configService.LotsOfLootConfig.MarkedRoomConfig.ExtraItems)
            {
                if (spawnpoint.Template.Items.Any(item => item.Template == itemTpl))
                {
                    continue;
                }

                string mongoId = hashUtil.Generate();

                spawnpoint.Template.Items.Add(new()
                {
                    Id = mongoId,
                    Template = itemTpl
                });

                spawnpoint.ItemDistribution.Add(new()
                {
                    ComposedKey = new() { Key = mongoId },
                    RelativeProbability = relativeProbability
                });

                logger.Debug($"Added {itemTpl} to {spawnpoint.Template.Id}");
            }
        }

        private void AdjustMarkedRoomItemGroups(Spawnpoint spawnpoint)
        {
            foreach (Item item in spawnpoint.Template.Items)
            {
                foreach ((string tpl, double relativeProbability) in configService.LotsOfLootConfig.MarkedRoomConfig.ItemGroups)
                {
                    if (itemHelper.IsOfBaseclass(item.Template, tpl))
                    {
                        foreach (LooseLootItemDistribution itemDistribution in spawnpoint.ItemDistribution)
                        {
                            if (itemDistribution.ComposedKey.Key == item.Id)
                            {
                                itemDistribution.RelativeProbability *= relativeProbability;
                                logger.Debug($"markedItemGroups: Changed {item.Template} to {itemDistribution.RelativeProbability}");
                            }
                        }
                    }
                }
            }
        }
    }
}
