using LotsofLoot.Helpers;
using LotsofLoot.Utilities;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Json;
using System.Diagnostics;

namespace LotsofLoot.Services
{
    [Injectable(InjectionType.Singleton)]
    public class LazyLoadHandlerService(DatabaseService databaseService, ConfigService configService,
        MarkedRoomHelper markedRoomHelper, LotsOfLootLogger logger)
    {
        public void OnPostDBLoad()
        {
            var locations = databaseService.GetLocations().GetAllPropsAsDict();

            foreach ((string locationId, object? locationObject) in locations)
            {
                if (locationObject is Location location)
                {
                    if (location.StaticLoot is not null)
                    {
                        location.StaticLoot.OnLazyLoad += (sender, eventArgs) =>
                        {
                            HandleStaticLootLazyLoad(locationId, eventArgs);
                        };
                    }

                    if (location.LooseLoot is not null)
                    {
                        location.LooseLoot.OnLazyLoad += (sender, eventArgs) =>
                        {
                            HandleLooseLootLazyLoad(locationId, eventArgs);
                        };
                    }
                }
            }
        }

        private void HandleStaticLootLazyLoad(string locationId, OnLazyLoadEventArgs<Dictionary<string, StaticLootDetails>> eventArgs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            foreach ((string containerId, StaticLootDetails lootDetails) in eventArgs.Value)
            {
                foreach (ItemDistribution itemDistribution in lootDetails.ItemDistribution)
                {
                    if (!configService.LotsOfLootConfig.Containers.TryGetValue(containerId, out float configRelativeProbability))
                    {
                        continue;
                    }

                    //Todo: Does this even work as intended? Check?
                    itemDistribution.RelativeProbability = MathF.Round((float)(itemDistribution.RelativeProbability *
                        configRelativeProbability));

                    logger.Debug($"Changed container {containerId} chance to {itemDistribution.RelativeProbability}");
                }
            }

            sw.Stop();
            logger.Info($"HandleStaticLootLazyLoad finished, took {sw.ElapsedMilliseconds}ms");
        }

        private void HandleLooseLootLazyLoad(string locationId, OnLazyLoadEventArgs<LooseLoot> eventArgs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            foreach (var spawnpoint in eventArgs.Value.Spawnpoints)
            {
                ChangeRelativeProbabilityInPool(locationId, spawnpoint);
                ChangeProbabilityOfPool(locationId, spawnpoint);

                markedRoomHelper.AdjustMarkedRooms(locationId, spawnpoint);

                //Todo: This still needs AddToRustedKeyRoom for streets
            }

            sw.Stop();
            logger.Info($"HandleLooseLootLazyLoad finished, took {sw.ElapsedMilliseconds}ms");
        }

        private void ChangeRelativeProbabilityInPool(string locationId, Spawnpoint spawnpoint)
        {
            Dictionary<string, LooseLootItemDistribution> distributionLookup = spawnpoint.ItemDistribution.ToDictionary(d => d.ComposedKey.Key);

            foreach (var item in spawnpoint.Template.Items)
            {
                if (configService.LotsOfLootConfig.ChangeRelativeProbabilityInPool.TryGetValue(item.Template, out int RelativeProbabilityInPoolModifier) &&
                    distributionLookup.TryGetValue(item.Id, out var itemDistribution))
                {
                    itemDistribution.RelativeProbability *= RelativeProbabilityInPoolModifier;
                    logger.Debug($"{locationId}, {spawnpoint.Template.Id}, {item.Template}, {itemDistribution.RelativeProbability}");
                }
            }
        }

        private void ChangeProbabilityOfPool(string locationId, Spawnpoint spawnpoint)
        {
            foreach (var item in spawnpoint.Template.Items)
            {
                if (configService.LotsOfLootConfig.ChangeProbabilityOfPool.TryGetValue(item.Template, out int probabilityMultiplier))
                {
                    spawnpoint.Probability = Math.Min((double)spawnpoint.Probability * probabilityMultiplier, 1);
                    logger.Debug($"{locationId}, Pool:{spawnpoint.Template.Id}, Chance:{spawnpoint.Probability}");

                    // Only apply once per pool
                    break;
                }
            }
        }
    }
}
