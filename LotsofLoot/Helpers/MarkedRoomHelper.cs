using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
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
                if (logger.IsDebug())
                {
                    logger.Debug($"Marked room ({locationId}) {spawnpoint.Template.Id}");
                }

                spawnpoint.Probability *= configService.LotsOfLootConfig.MarkedRoomConfig.Multiplier[locationId.ToLower()];
                AddExtraItemsToMarkedRoom(spawnpoint);

                AdjustMarkedRoomItemGroups(spawnpoint);
            }
        }

        private void AddExtraItemsToMarkedRoom(Spawnpoint spawnpoint)
        {
            var spawnpointTemplateItems = spawnpoint.Template.Items.ToList();
            var spawnpointItemDistribution = spawnpoint.ItemDistribution.ToList();

            foreach ((MongoId templateId, double relativeProbability) in configService.LotsOfLootConfig.MarkedRoomConfig.ExtraItems)
            {
                var existingItem = spawnpoint.Template.Items.FirstOrDefault(item => item.Template == templateId);

                // If the item already exists, add the new probability up on top of the already existing one
                if (existingItem != null && existingItem.ComposedKey != null)
                {
                    var existingItemDistribution = spawnpointItemDistribution.First(distrib => distrib.ComposedKey?.Key == existingItem.ComposedKey);

                    existingItemDistribution.RelativeProbability += relativeProbability;

                    if (logger.IsDebug())
                    {
                        logger.Debug($"Modified {templateId} to new probability {existingItemDistribution.RelativeProbability}");
                    }

                    // Continue the loop here, we don't need to add a new one
                    continue;
                }

                MongoId mongoId = new();

                spawnpointTemplateItems.Add(new() { Id = mongoId, Template = templateId, ComposedKey = mongoId });

                spawnpointItemDistribution.Add(
                    new()
                    {
                        ComposedKey = new() { Key = mongoId },
                        RelativeProbability = relativeProbability,
                    }
                );

                if (logger.IsDebug())
                {
                    logger.Debug($"Added {templateId} to {spawnpoint.Template.Id}");
                }
            }

            spawnpoint.Template.Items = spawnpointTemplateItems;
            spawnpoint.ItemDistribution = spawnpointItemDistribution;
        }

        private void AdjustMarkedRoomItemGroups(Spawnpoint spawnpoint)
        {
            if (spawnpoint?.Template?.Items is null)
            {
                logger.Warning("Spawnpoint template is null?");
                return;
            }

            // Delicious bracket slop, my favorite
            foreach (SptLootItem item in spawnpoint.Template.Items)
            {
                foreach ((MongoId templateId, double relativeProbability) in configService.LotsOfLootConfig.MarkedRoomConfig.ItemGroups)
                {
                    if (itemHelper.IsOfBaseclass(item.Template, templateId))
                    {
                        foreach (LooseLootItemDistribution itemDistribution in spawnpoint.ItemDistribution ?? [])
                        {
                            if (itemDistribution.ComposedKey is null)
                            {
                                continue;
                            }

                            if (itemDistribution.ComposedKey.Key == item.ComposedKey)
                            {
                                itemDistribution.RelativeProbability *= relativeProbability;

                                if (logger.IsDebug())
                                {
                                    logger.Debug($"markedItemGroups: Changed {item.Template} to {itemDistribution.RelativeProbability}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
