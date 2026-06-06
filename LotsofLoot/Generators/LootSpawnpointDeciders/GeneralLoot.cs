using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators.LootSpawnpointDeciders;

[Injectable]
public sealed class GeneralLoot(ConfigService config, ICloner cloner) : ILootSpawnpointDecider
{
    public List<Spawnpoint> Decide(
        string locationName,
        int desiredSpawnPointCount,
        ProbabilityObjectArray<string, Spawnpoint> spawnPointArray,
        List<Spawnpoint> guaranteedLoosePoints
    )
    {
        var regularArray = new ProbabilityObjectArray<string, Spawnpoint>(cloner);

        foreach (var entry in spawnPointArray)
        {
            var spawnPoint = spawnPointArray.Data(entry.Key);

            if (!spawnPoint.IsMarkedRoomSpawnpoint(locationName))
            {
                regularArray.Add(entry);
            }
        }

        // Select a number of spawn points to add loot to
        // Add ALL loose loot with 100% chance to pool
        List<Spawnpoint> chosenSpawnPoints = [];
        chosenSpawnPoints.AddRange(guaranteedLoosePoints.Where(p => !p.IsMarkedRoomSpawnpoint(locationName)));

        var regularRandomCount = desiredSpawnPointCount - guaranteedLoosePoints.Count;
        // Only draw random spawn points if needed
        if (regularRandomCount > 0 && regularArray.Count > 0)
        // Add randomly chosen spawn points
        {
            if (!config.LotsofLootPresetConfig.General.AllowLootOverlay)
            {
                foreach (var si in regularArray.DrawAndRemove(regularRandomCount))
                {
                    chosenSpawnPoints.Add(regularArray.Data(si));
                }
            }
            else
            {
                // Draw without removing if we allow loot overlay
                // We also have to clone here to make sure we aren't using an original
                // spawnpoint's templates because those will get emptied after being used
                foreach (var si in regularArray.Draw(regularRandomCount))
                {
                    chosenSpawnPoints.Add(cloner.Clone(regularArray.Data(si)));
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

        return chosenSpawnPoints;
    }
}
