using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Locales;

namespace LotsofLoot.Services;

[Injectable(InjectionType.Singleton)]
public sealed class LocaleCacheService(LocaleService localeService, TemplateTable templateTable)
{
    private readonly Dictionary<MongoId, string> _itemLocaleCache = [];
    private readonly Dictionary<MongoId, string> _itemgroupLocaleCache = new()
    {
        { BaseClasses.AMMO, "Ammo group" },
        { BaseClasses.AMMO_BOX, "Ammo box group" },
        //{ BaseClasses.ARM_BAND, "Arm band group" },
        { BaseClasses.ARMOR, "Armor group" },
        { BaseClasses.ARMOR_PLATE, "Armor plate group" },
        { BaseClasses.ARMORED_EQUIPMENT, "Armored equipment group" },
        { BaseClasses.ASSAULT_CARBINE, "Assault carbine group" },
        { BaseClasses.ASSAULT_RIFLE, "Assault rifle group" },
        { BaseClasses.ASSAULT_SCOPE, "Assault scope group" },
        { BaseClasses.AUXILIARY_MOD, "Auxiliary mod group" },
        { BaseClasses.BACKPACK, "Backpack group" },
        { BaseClasses.BARREL, "Barrel group" },
        { BaseClasses.BARTER_ITEM, "Barter item group" },
        { BaseClasses.BATTERY, "Battery group" },
        { BaseClasses.BIPOD, "Bipod group" },
        { BaseClasses.BUILDING_MATERIAL, "Building material group" },
        //{ BaseClasses.BUILT_IN_INSERTS, "Built-in inserts group" },
        { BaseClasses.CHARGE, "Charge group" },
        { BaseClasses.COLLIMATOR, "Collimator group" },
        { BaseClasses.COMPACT_COLLIMATOR, "Compact collimator group" },
        //{ BaseClasses.COMPASS, "Compass group" },
        { BaseClasses.COMPENSATOR, "Compensator group" },
        //{ BaseClasses.COMPOUND_ITEM, "Compound item group" },
        //{ BaseClasses.CULTIST_AMULET, "Cultist amulet group" },
        //{ BaseClasses.CYLINDER_MAGAZINE, "Cylinder magazine group" },
        { BaseClasses.DRINK, "Drink group" },
        { BaseClasses.DRUGS, "Drugs group" },
        { BaseClasses.ELECTRONICS, "Electronics group" },
        { BaseClasses.EQUIPMENT, "Equipment group" },
        { BaseClasses.FACE_COVER, "Face cover group" },
        { BaseClasses.FLASH_HIDER, "Flash hider group" },
        { BaseClasses.FLASHLIGHT, "Flashlight group" },
        { BaseClasses.FLYER, "Flyer group" },
        { BaseClasses.FOOD, "Food group" },
        { BaseClasses.FOOD_DRINK, "Food & drink group" },
        { BaseClasses.FOREGRIP, "Foregrip group" },
        { BaseClasses.FUEL, "Fuel group" },
        //{ BaseClasses.FUNCTIONAL_MOD, "Functional mod group" },
        { BaseClasses.GASBLOCK, "Gas block group" },
        { BaseClasses.GEAR_MOD, "Gear mod group" },
        { BaseClasses.GRENADE_LAUNCHER, "Grenade launcher group" },
        { BaseClasses.HANDGUARD, "Handguard group" },
        { BaseClasses.HEADPHONES, "Headphones group" },
        { BaseClasses.HEADWEAR, "Headwear group" },
        //{ BaseClasses.HIDEOUT_AREA_CONTAINER, "Hideout area container group" },
        { BaseClasses.HOUSEHOLD_GOODS, "Household goods group" },
        //{ BaseClasses.INFO, "Info group" },
        //{ BaseClasses.INVENTORY, "Inventory group" },
        //{ BaseClasses.IRON_SIGHT, "Iron sight group" },
        { BaseClasses.ITEM, "Item group" },
        { BaseClasses.JEWELRY, "Jewelry group" },
        { BaseClasses.KEY, "Key group" },
        { BaseClasses.KEY_MECHANICAL, "Mechanical key group" },
        { BaseClasses.KEYCARD, "Keycard group" },
        //{ BaseClasses.KNIFE, "Knife group" },
        { BaseClasses.LAUNCHER, "Launcher group" },
        { BaseClasses.LIGHT_LASER, "Light / laser group" },
        //{ BaseClasses.LOCKABLE_CONTAINER, "Lockable container group" },
        //{ BaseClasses.LOOT_CONTAINER, "Loot container group" },
        { BaseClasses.LUBRICANT, "Lubricant group" },
        { BaseClasses.MACHINE_GUN, "Machine gun group" },
        { BaseClasses.MAGAZINE, "Magazine group" },
        //{ BaseClasses.MAP, "Map group" },
        //{ BaseClasses.MARK_OF_UNKNOWN, "Mark of the unknown group" },
        { BaseClasses.MARKSMAN_RIFLE, "Marksman rifle group" },
        //{ BaseClasses.MASTER_MOD, "Master mod group" },
        { BaseClasses.MED_KIT, "Med kit group" },
        { BaseClasses.MEDICAL, "Medical group" },
        { BaseClasses.MEDICAL_SUPPLIES, "Medical supplies group" },
        { BaseClasses.MEDS, "Meds group" },
        //{ BaseClasses.MOB_CONTAINER, "Mob container group" },
        { BaseClasses.MOD, "Mod group" },
        { BaseClasses.MONEY, "Money group" },
        //{ BaseClasses.MOUNT, "Mount group" },
        //{ BaseClasses.MULTITOOLS, "Multitools group" },
        { BaseClasses.MUZZLE, "Muzzle group" },
        { BaseClasses.MUZZLE_COMBO, "Muzzle combo group" },
        { BaseClasses.NIGHT_VISION, "Night vision group" },
        { BaseClasses.OPTIC_SCOPE, "Optic scope group" },
        //{ BaseClasses.OTHER, "Other group" },
        { BaseClasses.PISTOL, "Pistol group" },
        { BaseClasses.PISTOL_GRIP, "Pistol grip group" },
        //{ BaseClasses.PLANTING_KITS, "Planting kits group" },
        //{ BaseClasses.PMS, "PMS group" },
        //{ BaseClasses.POCKETS, "Pockets group" },
        //{ BaseClasses.PORTABLE_RANGE_FINDER, "Portable range finder group" },
        //{ BaseClasses.RADIO_TRANSMITTER, "Radio transmitter group" },
        { BaseClasses.RAIL_COVERS, "Rail covers group" },
        //{ BaseClasses.RANDOM_LOOT_CONTAINER, "Random loot container group" },
        { BaseClasses.RECEIVER, "Receiver group" },
        { BaseClasses.REPAIR_KITS, "Repair kits group" },
        { BaseClasses.REVOLVER, "Revolver group" },
        { BaseClasses.ROCKET, "Rocket group" },
        { BaseClasses.ROCKET_LAUNCHER, "Rocket launcher group" },
        //{ BaseClasses.SEARCHABLE_ITEM, "Searchable item group" },
        { BaseClasses.SHAFT, "Shaft group" },
        { BaseClasses.SHOTGUN, "Shotgun group" },
        { BaseClasses.SIGHTS, "Sights group" },
        { BaseClasses.SILENCER, "Silencer group" },
        //{ BaseClasses.SIMPLE_CONTAINER, "Simple container group" },
        { BaseClasses.SMG, "SMG group" },
        { BaseClasses.SNIPER_RIFLE, "Sniper rifle group" },
        //{ BaseClasses.SORTING_TABLE, "Sorting table group" },
        //{ BaseClasses.SPEC_ITEM, "Special item group" },
        { BaseClasses.SPECIAL_SCOPE, "Special scope group" },
        //{ BaseClasses.SPECIAL_WEAPON, "Special weapon group" },
        //{ BaseClasses.SPRING_DRIVEN_CYLINDER, "Spring-driven cylinder group" },
        //{ BaseClasses.STACKABLE_ITEM, "Stackable item group" },
        //{ BaseClasses.STASH, "Stash group" },
        //{ BaseClasses.STATIONARY_CONTAINER, "Stationary container group" },
        { BaseClasses.STIMULATOR, "Stimulator group" },
        { BaseClasses.STOCK, "Stock group" },
        { BaseClasses.TACTICAL_COMBO, "Tactical combo group" },
        { BaseClasses.THERMAL_VISION, "Thermal vision group" },
        { BaseClasses.THROW_WEAP, "Throwable weapon group" },
        { BaseClasses.TOOL, "Tool group" },
        { BaseClasses.VEST, "Vest group" },
        { BaseClasses.VISORS, "Visors group" },
        { BaseClasses.WEAPON, "Weapon group" },
    };

    public void HydrateCache()
    {
        var localeDb = localeService.GetLocaleDb();

        foreach ((MongoId itemId, TemplateItem item) in templateTable.Items)
        {
            var localeId = $"{itemId} Name";

            if (localeDb.TryGetValue(localeId, out var locale))
            {
                _itemLocaleCache.TryAdd(itemId, locale);
            }
            else
            {
                if (item.Name is not null)
                {
                    _itemLocaleCache.TryAdd(itemId, item.Name);
                }
            }
        }
    }

    public IEnumerable<MongoId> FetchCacheKeys()
    {
        return _itemLocaleCache.Keys;
    }

    public IEnumerable<MongoId> FetchItemGroupCacheKeys()
    {
        return _itemgroupLocaleCache.Keys;
    }

    public string FetchLocale(string id)
    {
        if (_itemLocaleCache.TryGetValue(id, out var cachedLocale))
        {
            return cachedLocale;
        }

        var localeDb = localeService.GetLocaleDb();
        var localeId = $"{id} Name";

        if (localeDb.TryGetValue(localeId, out var locale))
        {
            _itemLocaleCache[id] = locale;
            return locale;
        }

        return id;
    }

    public string FetchItemGroupLocale(string id)
    {
        if (_itemgroupLocaleCache.TryGetValue(id, out var cachedLocale))
        {
            return cachedLocale;
        }

        return id;
    }
}
