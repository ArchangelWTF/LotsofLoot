using System.Reflection;
using LotsofLoot.Generators;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LotsofLoot.Overrides.Generators;

[Injectable]
public sealed class GenerateDynamicLootOverride : AbstractPatch
{
    private static LotsofLootLocationLootGenerator _lotsofLootLocationLootGenerator = default!;

    public GenerateDynamicLootOverride(LotsofLootLocationLootGenerator locationLootGenerator)
    {
        _lotsofLootLocationLootGenerator = locationLootGenerator;
    }

    protected override MethodBase GetTargetMethod()
    {
        return typeof(LocationLootGenerator).GetMethod(nameof(LocationLootGenerator.GenerateDynamicLoot))
            ?? throw new InvalidOperationException("Could not find LocationLootGenerator.GenerateDynamicLoot!");
        ;
    }

    [PatchPrefix]
    public static bool Prefix(
        LooseLoot dynamicLootDist,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
        string locationName,
        ref List<SpawnpointTemplate> __result
    )
    {
        __result = _lotsofLootLocationLootGenerator.GenerateDynamicLoot(dynamicLootDist, staticAmmoDist, locationName);

        return false;
    }
}

[Injectable]
public sealed class GenerateStaticLootOverride : AbstractPatch
{
    private static LotsofLootLocationLootGenerator _lotsofLootLocationLootGenerator = default!;

    public GenerateStaticLootOverride(LotsofLootLocationLootGenerator locationLootGenerator)
    {
        _lotsofLootLocationLootGenerator = locationLootGenerator;
    }

    protected override MethodBase GetTargetMethod()
    {
        return typeof(LocationLootGenerator).GetMethod("CreateStaticLootItem", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not find LocationLootGenerator.CreateStaticLootItem!");
    }

    [PatchPrefix]
    public static bool Prefix(
        MongoId chosenTpl,
        Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
        ref ContainerItem __result,
        string? parentId = null
    )
    {
        __result = _lotsofLootLocationLootGenerator.CreateStaticLootItem(chosenTpl, staticAmmoDist, parentId);

        return false;
    }
}
