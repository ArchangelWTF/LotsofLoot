using System.Reflection;
using LotsofLoot.Generators;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;

namespace LotsofLoot.Overrides.Generators
{
    public class GenerateDynamicLootOverride : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(LocationLootGenerator).GetMethod(nameof(LocationLootGenerator.GenerateDynamicLoot));
        }

        [PatchPrefix]
        public static bool Prefix(
            LooseLoot dynamicLootDist,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            string locationName,
            ref List<SpawnpointTemplate> __result
        )
        {
            LotsofLootLocationLootGenerator lotsofLootLocationLootGenerator =
                ServiceLocator.ServiceProvider.GetService<LotsofLootLocationLootGenerator>()
                ?? throw new NullReferenceException("Could not get LotsofLootLocationLootGenerator");

            __result = lotsofLootLocationLootGenerator.GenerateDynamicLoot(dynamicLootDist, staticAmmoDist, locationName);

            return false;
        }
    }

    public class GenerateStaticLootOverride : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(LocationLootGenerator).GetMethod("CreateStaticLootItem", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [PatchPrefix]
        public static bool Prefix(
            MongoId chosenTpl,
            Dictionary<string, IEnumerable<StaticAmmoDetails>> staticAmmoDist,
            ref ContainerItem __result,
            string? parentId = null
        )
        {
            LotsofLootLocationLootGenerator lotsofLootLocationLootGenerator =
                ServiceLocator.ServiceProvider.GetService<LotsofLootLocationLootGenerator>()
                ?? throw new NullReferenceException("Could not get LotsofLootLocationLootGenerator");

            __result = lotsofLootLocationLootGenerator.CreateStaticLootItem(chosenTpl, staticAmmoDist, parentId);

            return false;
        }
    }
}
