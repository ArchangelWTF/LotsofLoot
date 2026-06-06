using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators.LootSpawnpointDeciders;

[Injectable]
public sealed class MarkedRoom(ConfigService config, ICloner cloner, NewSPTRandomUtil randomUtil, ILogger<MarkedRoom> logger)
    : ILootSpawnpointDecider
{
    public List<Spawnpoint> Decide(
        string locationName,
        int desiredSpawnPointCount,
        ProbabilityObjectArray<string, Spawnpoint> spawnPointArray,
        List<Spawnpoint> guaranteedLoosePoints
    )
    {
        if (!config.LotsofLootPresetConfig.MarkedRoomConfig.Multiplier.ContainsKey(locationName.ToLowerInvariant()))
        {
            return [];
        }

        List<Spawnpoint> chosenSpawnPoints = [];
        var markedRoomArray = new ProbabilityObjectArray<string, Spawnpoint>(cloner);

        foreach (var entry in spawnPointArray)
        {
            var spawnPoint = spawnPointArray.Data(entry.Key);

            if (spawnPoint.IsMarkedRoomSpawnpoint(locationName))
            {
                markedRoomArray.Add(entry);
            }
        }

        // Technically the marked room can't have guaranteed points as all spawns should be below the probability of 1
        // But just in case a mod changes this we handle it here
        var guaranteedMarkedRoomPoints = guaranteedLoosePoints.Where(p => p.IsMarkedRoomSpawnpoint(locationName)).ToList();
        chosenSpawnPoints.AddRange(guaranteedMarkedRoomPoints);

        // Derive the expected item count from the sum of spawn point probabilities, scaled by the multiplier.
        // Variance is calculated from the same probabilities to keep randomness proportional to the actual data.
        // Both mean and std are scaled using square roots to prevent extreme loot amounts at higher multipliers.
        var markedRoomMultiplier = config.LotsofLootPresetConfig.MarkedRoomConfig.Multiplier[locationName];
        var expectedCount = 0.0;
        var variance = 0.0;
        foreach (var entry in markedRoomArray)
        {
            var probability = spawnPointArray.Data(entry.Key).Probability;
            expectedCount += probability ?? 0;
            variance += probability * (1.0 - probability) ?? 0;
        }
        var std = Math.Sqrt(variance);
        var meanScale = Math.Sqrt(Math.Max(0.0, markedRoomMultiplier));
        var stdScale = Math.Sqrt(meanScale);
        var markedRoomDesiredCount = (int)
            Math.Round(Math.Max(0.0, randomUtil.GetNormallyDistributedRandomNumber(expectedCount * meanScale, std * stdScale)));

        var markedRoomRandomCount = markedRoomDesiredCount - guaranteedMarkedRoomPoints.Count;
        if (markedRoomDesiredCount > 0 && markedRoomArray.Count > 0)
        {
            if (!config.LotsofLootPresetConfig.General.AllowLootOverlay)
            {
                foreach (var si in markedRoomArray.DrawAndRemove(markedRoomRandomCount))
                {
                    chosenSpawnPoints.Add(markedRoomArray.Data(si));
                }
            }
            else
            {
                // Draw without removing if we allow loot overlay
                // We also have to clone here to make sure we aren't using an original
                // spawnpoint's templates because those will get emptied after being used
                foreach (var si in markedRoomArray.Draw(markedRoomRandomCount))
                {
                    chosenSpawnPoints.Add(cloner.Clone(markedRoomArray.Data(si)));
                }
            }
        }

        /*
        if (!config.LotsofLootPresetConfig.General.AllowLootOverlay)
        {
            // Filter out duplicate locationIds // prob can be done better
            chosenSpawnPoints = chosenSpawnPoints.GroupBy(sp => sp.LocationId).Select(g => g.First()).ToList();
        }
        */

        logger.LogWarning(chosenSpawnPoints.Count.ToString());

        return chosenSpawnPoints;
    }
}
