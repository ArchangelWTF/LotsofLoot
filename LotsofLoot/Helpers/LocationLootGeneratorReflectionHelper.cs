using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LotsofLoot.Helpers;

[Injectable]
public class LocationLootGeneratorReflectionHelper(LocationLootGenerator locationLootGenerator)
{
    private static MethodInfo? _forcedDynamicLootMethod = null;
    private static MethodInfo? _looseLootMultiplierForLocationMethod = null;

    public List<SpawnpointTemplate> GetForcedDynamicLoot(
        IEnumerable<Spawnpoint> forcedSpawnPoints,
        string locationName,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist
    )
    {
        if (_forcedDynamicLootMethod is null)
        {
            var type = locationLootGenerator.GetType();
            var method = type.GetMethod("GetForcedDynamicLoot", BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new MissingMethodException($"Could not find 'GetForcedDynamicLoot' on {type.FullName}");
            }

            _forcedDynamicLootMethod = method;
        }

        if (
            _forcedDynamicLootMethod.Invoke(locationLootGenerator, [forcedSpawnPoints, locationName, staticAmmoDist])
            is not List<SpawnpointTemplate> result
        )
        {
            throw new NullReferenceException("SpawnpointTemplate List is null");
        }

        return result;
    }

    public double GetLooseLootMultiplierForLocation(string location)
    {
        if (_looseLootMultiplierForLocationMethod is null)
        {
            var type = locationLootGenerator.GetType();
            var method = type.GetMethod("GetLooseLootMultiplierForLocation", BindingFlags.Instance | BindingFlags.NonPublic);

            if (method == null)
            {
                throw new MissingMethodException($"Could not find 'GetLooseLootMultiplierForLocation' on {type.FullName}");
            }

            _looseLootMultiplierForLocationMethod = method;
        }

        if (_looseLootMultiplierForLocationMethod.Invoke(locationLootGenerator, [location]) is not double result)
        {
            throw new NullReferenceException("Double is null");
        }

        return result;
    }
}
