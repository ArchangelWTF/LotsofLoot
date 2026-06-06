using LotsofLoot.Services;
using LotsofLoot.Utilities;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators.LootSpawnpointDeciders;

[Injectable]
// Just a copy of the marked room class for now, might change later
public sealed class RefRoom(ConfigService config, ICloner cloner, NewSPTRandomUtil randomUtil, ILogger<RefRoom> logger)
    : ILootSpawnpointDecider
{
    public List<Spawnpoint> Decide(
        string locationName,
        int desiredSpawnPointCount,
        ProbabilityObjectArray<string, Spawnpoint> spawnPointArray,
        List<Spawnpoint> guaranteedLoosePoints
    )
    {
        if (!config.LotsofLootPresetConfig.RefRoomConfig.Multiplier.ContainsKey(locationName.ToLowerInvariant()))
        {
            return [];
        }

        List<Spawnpoint> chosenSpawnPoints = [];
        var refRoomArray = new ProbabilityObjectArray<string, Spawnpoint>(cloner);

        foreach (var entry in spawnPointArray)
        {
            var spawnPoint = spawnPointArray.Data(entry.Key);

            if (spawnPoint.IsRefKeySpawnpoint(locationName))
            {
                refRoomArray.Add(entry);
            }
        }

        // Technically the Ref room can't have guaranteed points as all spawns should be below the probability of 1
        // But just in case a mod changes this we handle it here
        var guaranteedRefRoomPoints = guaranteedLoosePoints.Where(p => p.IsRefKeySpawnpoint(locationName)).ToList();
        chosenSpawnPoints.AddRange(guaranteedRefRoomPoints);

        // Derive the expected item count from the sum of spawn point probabilities, scaled by the multiplier.
        // Variance is calculated from the same probabilities to keep randomness proportional to the actual data.
        // Both mean and std are scaled using square roots to prevent extreme loot amounts at higher multipliers.
        var refRoomMultiplier = config.LotsofLootPresetConfig.RefRoomConfig.Multiplier[locationName];
        var expectedCount = 0.0;
        var variance = 0.0;
        foreach (var entry in refRoomArray)
        {
            var probability = spawnPointArray.Data(entry.Key).Probability;
            expectedCount += probability ?? 0;
            variance += probability * (1.0 - probability) ?? 0;
        }
        var std = Math.Sqrt(variance);
        var meanScale = Math.Sqrt(Math.Max(0.0, refRoomMultiplier));
        var stdScale = Math.Sqrt(meanScale);
        var refRoomDesiredCount = (int)
            Math.Round(Math.Max(0.0, randomUtil.GetNormallyDistributedRandomNumber(expectedCount * meanScale, std * stdScale)));

        var refRoomRandomCount = refRoomDesiredCount - guaranteedRefRoomPoints.Count;
        if (refRoomDesiredCount > 0 && refRoomArray.Count > 0)
        {
            if (!config.LotsofLootPresetConfig.General.AllowLootOverlay)
            {
                foreach (var si in refRoomArray.DrawAndRemove(refRoomRandomCount))
                {
                    chosenSpawnPoints.Add(refRoomArray.Data(si));
                }
            }
            else
            {
                // Draw without removing if we allow loot overlay
                // We also have to clone here to make sure we aren't using an original
                // spawnpoint's templates because those will get emptied after being used
                foreach (var si in refRoomArray.Draw(refRoomRandomCount))
                {
                    chosenSpawnPoints.Add(cloner.Clone(refRoomArray.Data(si)));
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
