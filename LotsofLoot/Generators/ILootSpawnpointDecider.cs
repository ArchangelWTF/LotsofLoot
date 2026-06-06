using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Collections;

namespace LotsofLoot.Generators;

public interface ILootSpawnpointDecider
{
    public List<Spawnpoint> Decide(
        string locationName,
        int desiredSpawnPointCount,
        ProbabilityObjectArray<string, Spawnpoint> spawnPointArray,
        List<Spawnpoint> guaranteedLoosePoints
    );
}
