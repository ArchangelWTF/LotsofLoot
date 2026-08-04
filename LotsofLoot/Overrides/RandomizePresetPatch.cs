using System.Reflection;
using LotsofLoot.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Services.InRaid;

namespace LotsofLoot.Overrides;

/// <summary>
/// Rolls the preset just before a raid's loot is generated, so the swap is picked up by the loot
/// generator on the very same call.
/// </summary>
[Injectable]
public sealed class RandomizePresetPatch : AbstractPatch
{
    private static PresetRandomizerService _randomizerService = default!;

    public RandomizePresetPatch(PresetRandomizerService randomizerService)
    {
        _randomizerService = randomizerService;
    }

    protected override MethodBase GetTargetMethod()
    {
        return typeof(LocationLifecycleService).GetMethod(nameof(LocationLifecycleService.GenerateLocationAndLoot))
            ?? throw new InvalidOperationException("Could not find LocationLifecycleService.GenerateLocationAndLoot!");
    }

    [PatchPrefix]
    public static void Prefix(string name, bool generateLoot)
    {
        if (generateLoot && !name.Equals("hideout", StringComparison.OrdinalIgnoreCase))
        {
            _randomizerService.RollForRaid();
        }
    }
}
