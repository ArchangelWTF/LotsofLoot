/* eslint-disable @typescript-eslint/naming-convention */
/* eslint-disable @typescript-eslint/indent */
/* eslint-disable no-constant-condition */
/* eslint-disable prefer-spread */
import pkg from "../package.json"
import JSON5 from "json5";
import path from "path";
import { DependencyContainer } from "tsyringe";
import { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { IPostDBLoadMod } from "@spt/models/external/IPostDBLoadMod";
import { IContainerItem, LocationGenerator } from "@spt/generators/LocationGenerator";
import { ProbabilityObject, ProbabilityObjectArray } from "@spt/utils/RandomUtil";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { ObjectId } from "@spt/utils/ObjectId";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { PresetHelper } from "@spt/helpers/PresetHelper";
import { BaseClasses } from "@spt/models/enums/BaseClasses";
import { Item } from "@spt/models/eft/common/tables/IItem";
import { RandomUtil } from "@spt/utils/RandomUtil";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { MathUtil } from "@spt/utils/MathUtil";
import { LocalisationService } from "@spt/services/LocalisationService";
import { ILooseLoot, Spawnpoint, SpawnpointsForced, SpawnpointTemplate } from "@spt/models/eft/common/ILooseLoot";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";
import { ITemplateItem, GridFilter } from "@spt/models/eft/common/tables/ITemplateItem";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";
import { SeasonalEventService } from "@spt/services/SeasonalEventService";
import { ILocation, IStaticAmmoDetails } from "@spt/models/eft/common/ILocation";
import { ILotsofLootConfig } from "./ILotsofLootConfig";
import { VFS } from "@spt/utils/VFS";

class Mod implements IPreSptLoadMod, IPostDBLoadMod
{
    private static config: ILotsofLootConfig = null;

    private static container: DependencyContainer;
    static filterIndex = [{
        tpl:"",
        entries:[""]
    }];

    public preSptLoad(container: DependencyContainer): void
    {
        Mod.container = container;

        // Get VFS to read in configs
        const vfs = container.resolve<VFS>("VFS");
        // Load config statically so that we don't have to keep reloading this across the entire file.
        Mod.config = JSON5.parse(vfs.readFile(path.resolve(__dirname, "../config/config.json5")));

        container.afterResolution("LocationGenerator", (_t, result: LocationGenerator) =>
        {
            //Temporary cast to get rid of protected error
            (result as any).createStaticLootItem = (tpl, staticAmmoDist, parentId) =>
            {
                return this.createStaticLootItem(tpl, staticAmmoDist, parentId);
            }
        }, {frequency: "Always"});

        container.afterResolution("LocationGenerator", (_t, result: LocationGenerator) => 
        {
            result.generateDynamicLoot = (dynamicLootDist, staticAmmoDist, locationName) => 
            {
                return this.generateDynamicLoot(dynamicLootDist, staticAmmoDist, locationName);
            }
        }, {frequency: "Always"});
    }

    public postDBLoad(container: DependencyContainer): void
    {
        const logger = container.resolve<ILogger>("WinstonLogger");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");
        const tables = databaseServer.getTables();
        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const locations = databaseServer.getTables().locations;
        const LocationConfig = configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        this.MarkedRoomChanges();
        this.addToRustedKeyRoom();
        
        for (const map in Mod.config.looseLootMultiplier)
        {
            LocationConfig.looseLootMultiplier[map] = Mod.config.looseLootMultiplier[map];
            if (Mod.config.general.debug) logger.info(`${map} ${LocationConfig.looseLootMultiplier[map]}`);
            LocationConfig.staticLootMultiplier[map] = Mod.config.staticLootMultiplier[map];
            if (Mod.config.general.debug) logger.info(`${map} ${LocationConfig.staticLootMultiplier[map]}`);
        }

        for(const locationId in locations)
        {
            if(locations.hasOwnProperty(locationId))
            {
                const location: ILocation = locations[locationId];
                //Location does not have any static loot pools, skip this map.
                if(!location.staticLoot)
                {
                    if(Mod.config.general.debug)
                    {
                        logger.info(`Skipping ${locationId} as it has no static loot`);
                    }

                    continue;
                }

                const staticLoot = location.staticLoot;

                for (const container in staticLoot)
                {
                    for (const possItemCount in staticLoot[container].itemcountDistribution)
                    {
                        if (staticLoot[container].itemcountDistribution[possItemCount].count == 0)
                        {
                            staticLoot[container].itemcountDistribution[possItemCount].relativeProbability = Math.round(staticLoot[container].itemcountDistribution[possItemCount].relativeProbability * Mod.config.containers[container]);
                            if (Mod.config.general.debug) logger.info(`Changed  Container ${container} chance to ${staticLoot[container].itemcountDistribution[possItemCount].relativeProbability}`);
                        }
                    }
                }
            }
        }
        
        /*
        for (const id in config.ChanceInPool)
        {
            this.changeChanceInPool(id, config.ChanceInPool[id]);
        }

        for (const id in config.ChangePool)
        {
            this.changeChancePool(id, config.ChangePool[id]);
        }
        */

        if (Mod.config.general.disableFleaRestrictions)
        {
            for (const item in tables.templates.items)
            {
                if (itemHelper.isValidItem(tables.templates.items[item]._id))
                {
                    tables.templates.items[item]._props.CanRequireOnRagfair = true;
                    tables.templates.items[item]._props.CanSellOnRagfair = true;
                }
            }
        }

        for (const id in Mod.config.general.priceCorrection)
        {
            tables.templates.prices[id] = Mod.config.general.priceCorrection[id];
        }

        logger.info(`Finished loading: ${pkg.name}`);
        return 
    }

    private generateDynamicLoot(dynamicLootDist: ILooseLoot, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, locationName: string): SpawnpointTemplate[]
    {
        const locationGenerator = Mod.container.resolve<LocationGenerator>("LocationGenerator");
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const jsonUtil = Mod.container.resolve<JsonUtil>("JsonUtil");
        const randomUtil = Mod.container.resolve<RandomUtil>("RandomUtil");
        const mathUtil = Mod.container.resolve<MathUtil>("MathUtil");
        const localisationService = Mod.container.resolve<LocalisationService>("LocalisationService");
        const seasonalEventService = Mod.container.resolve<SeasonalEventService>("SeasonalEventService");
        const configServer = Mod.container.resolve<ConfigServer>("ConfigServer");
        const LocationConfig = configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        const loot: SpawnpointTemplate[] = [];
        const dynamicForcedSpawnPoints: SpawnpointsForced[] = [];

        // Build the list of forced loot from both `spawnpointsForced` and any point marked `IsAlwaysSpawn`
        dynamicForcedSpawnPoints.push(...dynamicLootDist.spawnpointsForced);
        dynamicForcedSpawnPoints.push(...dynamicLootDist.spawnpoints.filter((point) => point.template.IsAlwaysSpawn));

        // Temporary cast to get rid of protected, add all forced loot to return array
        (locationGenerator as any).addForcedLoot(loot, dynamicLootDist.spawnpointsForced, locationName);

        const allDynamicSpawnpoints = dynamicLootDist.spawnpoints;

        // Temporary cast to get rid of protected, draw from random distribution
        let desiredSpawnpointCount = Math.round((locationGenerator as any).getLooseLootMultiplerForLocation(locationName) * randomUtil.getNormallyDistributedRandomNumber(dynamicLootDist.spawnpointCount.mean, dynamicLootDist.spawnpointCount.std));

        if (desiredSpawnpointCount > Mod.config.limits[locationName])
        {
            desiredSpawnpointCount = Mod.config.limits[locationName];
        }

        // Positions not in forced but have 100% chance to spawn
        const guaranteedLoosePoints: Spawnpoint[] = [];

        const blacklistedSpawnpoints = LocationConfig.looseLootBlacklist[locationName];
        const spawnpointArray = new ProbabilityObjectArray<string, Spawnpoint>(mathUtil, jsonUtil);

        for (const spawnpoint of allDynamicSpawnpoints)
        {
            if (blacklistedSpawnpoints?.includes(spawnpoint.template.Id))
            {
                logger.debug(`Ignoring loose loot location: ${spawnpoint.template.Id}`);
                continue;
            }

            // We've handled IsAlwaysSpawn above, so skip them
            if (spawnpoint.template.IsAlwaysSpawn)
            {
                continue;
            }

            if (spawnpoint.probability === 1)
            {
                guaranteedLoosePoints.push(spawnpoint);
            }

            spawnpointArray.push(new ProbabilityObject(spawnpoint.template.Id, spawnpoint.probability, spawnpoint));
        }

        // Select a number of spawn points to add loot to
        // Add ALL loose loot with 100% chance to pool
        let chosenSpawnpoints: Spawnpoint[] = [...guaranteedLoosePoints];

        const randomSpawnpointCount = desiredSpawnpointCount - chosenSpawnpoints.length;
        // Add randomly chosen spawn points
        if(randomSpawnpointCount)
        {
            for (const si of spawnpointArray.draw(randomSpawnpointCount, true))
            {
                chosenSpawnpoints.push(spawnpointArray.data(si));
            }
        }
        
        if (!Mod.config.general.allowLootOverlay)
        {
            // Filter out duplicate locationIds
            chosenSpawnpoints = [...new Map(chosenSpawnpoints.map((x) => [x.locationId, x])).values()];

            // Do we have enough items in pool to fulfill requirement
            const tooManySpawnPointsRequested = (desiredSpawnpointCount - chosenSpawnpoints.length) > 0;
            if (tooManySpawnPointsRequested)
            {
                logger.debug(
                    localisationService.getText("location-spawn_point_count_requested_vs_found", {
                        requested: desiredSpawnpointCount + guaranteedLoosePoints.length,
                        found: chosenSpawnpoints.length,
                        mapName: locationName
                    })
                );
            }
        }

        // Iterate over spawnpoints
        const seasonalEventActive = seasonalEventService.seasonalEventEnabled();
        const seasonalItemTplBlacklist = seasonalEventService.getInactiveSeasonalEventItems();
        for (const spawnPoint of chosenSpawnpoints)
        {
            if (!spawnPoint.template)
            {
                logger.warning(localisationService.getText("location-missing_dynamic_template", spawnPoint.locationId));
                continue;
            }

            if (!spawnPoint.template.Items || spawnPoint.template.Items.length === 0)
            {
                logger.error(localisationService.getText("location-spawnpoint_missing_items", spawnPoint.template.Id));
                continue;
            }

            const itemArray = new ProbabilityObjectArray<string>(mathUtil, jsonUtil);
            for (const itemDist of spawnPoint.itemDistribution)
            {
                if (!seasonalEventActive && seasonalItemTplBlacklist.includes(spawnPoint.template.Items.find((x) => x._id === itemDist.composedKey.key)._tpl))
                {
                    // Skip seasonal event items if they're not enabled
                    continue;
                }

                itemArray.push(new ProbabilityObject(itemDist.composedKey.key, itemDist.relativeProbability));
            }

            if (itemArray.length === 0)
            {
                logger.warning(`Loot pool for position: ${spawnPoint.template.Id} is empty. Skipping`);

                continue;
            }

            // Draw a random item from spawn points possible items
            const spawnPointClone = jsonUtil.clone(spawnPoint);
            const chosenComposedKey = itemArray.draw(1)[0];
            const chosenItem = spawnPointClone.template.Items.find(x => x._id === chosenComposedKey);
            const chosenTpl = chosenItem._tpl;
            const createItemResult = this.createStaticLootItem(chosenTpl, staticAmmoDist, undefined, spawnPointClone);

            // Root id can change when generating a weapon
            spawnPointClone.template.Root = createItemResult.items[0]._id;
            spawnPointClone.template.Items = createItemResult.items;

            loot.push(spawnPointClone.template);
        }

        return loot;
    }

    private createStaticLootItem(tpl: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, parentId: string = undefined, spawnPoint: Spawnpoint = undefined): IContainerItem
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");
        const objectId = Mod.container.resolve<ObjectId>("ObjectId");
        const jsonUtil = Mod.container.resolve<JsonUtil>("JsonUtil");
        const presetHelper = Mod.container.resolve<PresetHelper>("PresetHelper");
        const locationGenerator = Mod.container.resolve<LocationGenerator>("LocationGenerator");
        const randomUtil = Mod.container.resolve<RandomUtil>("RandomUtil");
        const localisationService = Mod.container.resolve<LocalisationService>("LocalisationService");
        const configServer = Mod.container.resolve<ConfigServer>("ConfigServer");
        const LocationConfig = configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        const gotItem = itemHelper.getItem(tpl);
        let itemTemplate:ITemplateItem;
        if (gotItem[0])
        {
            itemTemplate = gotItem[1];
        }
        else
        {
            return {
                "items": [],
                "width": 0,
                "height": 0
            };
        }

        let width = itemTemplate._props.Width;
        let height = itemTemplate._props.Height;
        let items: Item[] = [{
            _id: objectId.generate(),
            _tpl: tpl
        }];

        // container item has container's id as parentId
        if (parentId) 
        {
            items[0].parentId = parentId
        }

        if (itemHelper.isOfBaseclass(tpl, BaseClasses.WEAPON))
        {
            if (spawnPoint != undefined)
            {
                const chosenItem = spawnPoint.template.Items.find(x => x._tpl === tpl);
                // Get item + children and add into array we return
                const itemWithChildren = itemHelper.findAndReturnChildrenAsItems(spawnPoint.template.Items, chosenItem._id);
                // Temporary cast to get rid of protected error, we need to reparent to ensure ids are unique
                (locationGenerator as any).reparentItemAndChildren(itemWithChildren);
                items.splice(0,1);
                items.push(...itemWithChildren);
            }
            else
            {
                let children: Item[] = [];
                const defaultPreset = jsonUtil.clone(presetHelper.getDefaultPreset(tpl));
                if (defaultPreset)
                {
                    try
                    {
                        children = itemHelper.reparentItemAndChildren(defaultPreset._items[0], defaultPreset._items);
                    }
                    catch (error)
                    {
                        // this item already broke it once without being reproducible tpl = "5839a40f24597726f856b511"; AKS-74UB Default
                        // 5ea03f7400685063ec28bfa8 // ppsh default
                        // 5ba26383d4351e00334c93d9 //mp7_devgru
                        logger.warning(localisationService.getText("location-preset_not_found", { tpl: tpl, defaultId: defaultPreset._id, defaultName: defaultPreset._name, parentId: parentId }));
                        throw error;
                    }
                }
                else
                {
                    // RSP30 (62178be9d0050232da3485d9/624c0b3340357b5f566e8766) doesnt have any default presets and kills this code below as it has no chidren to reparent
                    logger.debug(`createItem() No preset found for weapon: ${tpl}`);
                }

                const rootItem = items[0];
                if (!rootItem)
                {
                    logger.error(localisationService.getText("location-missing_root_item", { tpl: tpl, parentId: parentId }));

                    throw new Error(localisationService.getText("location-critical_error_see_log"));
                }

                try
                {
                    if (children?.length > 0)
                    {
                        items = itemHelper.reparentItemAndChildren(rootItem, children);
                    }
                }
                catch (error)
                {
                    logger.error(localisationService.getText("location-unable_to_reparent_item", { tpl: tpl, parentId: parentId }));

                    throw error;
                }

                // Here we should use generalized BotGenerators functions e.g. fillExistingMagazines in the future since
                // it can handle revolver ammo (it's not restructured to be used here yet.)
                // General: Make a WeaponController for Ragfair preset stuff and the generating weapons and ammo stuff from
                // BotGenerator
                const magazine = items.filter(x => x.slotId === "mod_magazine")[0];
                // some weapon presets come without magazine; only fill the mag if it exists
                if (magazine)
                {
                    const magTemplate = itemHelper.getItem(magazine._tpl)[1];
                    const weaponTemplate = itemHelper.getItem(tpl)[1];

                    // Create array with just magazine
                    const magazineWithCartridges = [magazine];
                    itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, magTemplate, staticAmmoDist, weaponTemplate._props.ammoCaliber);

                    // Replace existing magazine with above array
                    items.splice(items.indexOf(magazine), 1, ...magazineWithCartridges);
                }

                const size = itemHelper.getItemSize(items, rootItem._id);
                width = size.width;
                height = size.height;
            }
            
        }
        else if (itemHelper.isOfBaseclass(tpl, BaseClasses.MONEY) || itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO)) 
        {
            const stackCount = randomUtil.getInt(itemTemplate._props.StackMinRandom, itemTemplate._props.StackMaxRandom);
            items[0].upd = { "StackObjectsCount": stackCount };
        }
        else if (itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO_BOX))
        {
            itemHelper.addCartridgesToAmmoBox(items, itemTemplate);
        }
        else if (itemHelper.isOfBaseclass(tpl, BaseClasses.MAGAZINE))
        {
            if (randomUtil.getChance100(LocationConfig.magazineLootHasAmmoChancePercent))
            {
                // Create array with just magazine
                const magazineWithCartridges = [items[0]];
                itemHelper.fillMagazineWithRandomCartridge(
                    magazineWithCartridges,
                    itemTemplate,
                    staticAmmoDist,
                    null,
                    LocationConfig.minFillStaticMagazinePercent / 100,
                );

                // Replace existing magazine with above array
                items.splice(items.indexOf(items[0]), 1, ...magazineWithCartridges);
            }

        }
        else if (itemHelper.isOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER) && (tpl != "5c093e3486f77430cb02e593"))
        {
            const contloot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, Mod.config.general.looseContainerModifier);
            if (Mod.config.general.debug) logger.info(`Container ${tpl} with`);
            for (const cont of contloot) 
            {
                if (Mod.config.general.debug) logger.info(`${cont._tpl}`);
                items.push(cont);
            }
        }
        else if (itemHelper.isOfBaseclass(tpl, BaseClasses.BACKPACK))
        {
            const contloot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, Mod.config.general.looseBackpackModifier);
            if (Mod.config.general.debug) logger.info(`Backpack ${tpl} with`);
            for (const cont of contloot) 
            {
                if (Mod.config.general.debug) logger.info(`${cont._tpl}`);
                items.push(cont);
            }
        }
        else if (itemHelper.armorItemCanHoldMods(tpl))
        {
            const defaultPreset = presetHelper.getDefaultPreset(tpl);
            if (defaultPreset)
            {
                const presetAndMods: Item[] = itemHelper.replaceIDs(defaultPreset._items);
                itemHelper.remapRootItemId(presetAndMods);

                // Use original items parentId otherwise item doesnt get added to container correctly
                presetAndMods[0].parentId = items[0].parentId;
                items = presetAndMods;
            }
            else
            {
                // We make base item above, at start of function, no need to do it here
                if (itemTemplate._props.Slots?.length > 0)
                {
                    items = itemHelper.addChildSlotItems(
                        items,
                        itemTemplate,
                        LocationConfig.equipmentLootSettings.modSpawnChancePercent,
                    );
                }
            }
        }

        return {
            "items": items,
            "width": width,
            "height": height
        };

    }
    

    private createLooseContainerLoot(tpl: string, id: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, modifier = 0.5): Item[]
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const randomUtil = Mod.container.resolve<RandomUtil>("RandomUtil");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");
        const mathUtil = Mod.container.resolve<MathUtil>("MathUtil");
        const jsonUtil = Mod.container.resolve<JsonUtil>("JsonUtil");

        const tables = databaseServer.getTables();

        const items = tables.templates.items;
        const item = items[tpl];
        const rturn:Item[] = [];

        if (item._props.Grids[0]._props.filters[0] === undefined)
        {
            if (Mod.config.general.debug) logger.info(`${item._name} doesn't have a filter`);
            const wow:GridFilter[] = [{Filter:["54009119af1c881c07000029"],
                                       ExcludedFilter:[]}];
            item._props.Grids[0]._props.filters = wow;
        }
        let whitelist = jsonUtil.clone(item._props.Grids[0]._props.filters[0].Filter);
        let blacklist = jsonUtil.clone(item._props?.Grids[0]._props.filters[0]?.ExcludedFilter) ?? [];
        const amount = randomUtil.getInt(1, item._props.Grids[0]._props.cellsH * item._props.Grids[0]._props.cellsV * modifier);
        let fill = 0;
        let match = 0;

        for (const filter of Mod.filterIndex)
        {
            if (filter.tpl == tpl)
            {
                whitelist = filter.entries;
                match++;
            }
        }

        if (match == 0)
        {
            if (Mod.config.general.debug) logger.info(`${tpl} is new, generating whitelist`);
            //If whitelist contains a parent instead of items the parent gets repaced by all its children
            const whitelist_new: string[] = [];
            for (const content of whitelist)
            {
                const h = this.findAndReturnChildrenByItems(items, content);
                whitelist_new.push(...h);
            }

            whitelist = whitelist_new;

            //If blacklist contains a parent instead of items the parent gets repaced by all its children
            const blacklist_new: string[] = [];
            for (const content of blacklist)
            {
                const h = this.findAndReturnChildrenByItems(items, content);
                blacklist_new.push(...h);
            }

            blacklist = blacklist_new;

            //If any entrys of black and whitelist match the whitelist entry should be removed
            for (const white in whitelist)
            {
                for (const black in blacklist)
                {
                    if (whitelist[white] == blacklist[black])
                    {
                        whitelist.splice(whitelist.indexOf(white), 1);
                    }
                }
            }

            //Extra restrictions to avoid errors
            for (let white = 0; white < whitelist.length; white++)
            {
                if (!itemHelper.isValidItem(whitelist[white]))                                                              //Checks if the Item can be in your Stash
                {
                    if (whitelist[white] == "5449016a4bdc2d6f028b456f")
                    {
                        continue;
                    }
                    whitelist.splice(white, 1);
                    white--;
                }
                else if (items[whitelist[white]]._props.Prefab.path == "")                                                  //If the Item has no model it cant be valid
                {
                    whitelist.splice(white, 1);
                    white--;
                }
            }
            
            Mod.filterIndex.push({
                tpl: tpl,
                entries: whitelist
            });
            
        }
        
        if (whitelist.length == 0)
        {
            if (Mod.config.general.debug) logger.info(`${tpl} whitelist is empty`);
            return rturn;
        }

        const weight: number[] = [];
        for (let i = 0; i < whitelist.length; i++)
        {
            if (tables.templates.prices[whitelist[i]])
            {
                weight.push(Math.round(1000/Math.pow(tables.templates.prices[whitelist[i]],(1/3))));
            }
            else if (whitelist[i] == "5449016a4bdc2d6f028b456f")
            {
                weight.push(500);
            }
            else if (whitelist[i] == "5696686a4bdc2da3298b456a")
            {
                weight.push(100);
            }
            else if (whitelist[i] == "569668774bdc2da2298b4568")
            {
                weight.push(100);
            }
            else
            {
                weight.push(1);
            }
        }

        const itemArray = new ProbabilityObjectArray<string>(mathUtil, jsonUtil);
        for (let i = 0; i < whitelist.length; i++)
        {
            itemArray.push(new ProbabilityObject(whitelist[i], weight[i]));
        }
        
        while (true)
        {
            let cont: string;
            if (Mod.config.general.itemWeights)
            {
                cont = itemArray.draw(1,true)[0];
            }
            else
            {
                cont = whitelist[randomUtil.getInt(0,whitelist.length-1)];
            }

            const positem = this.createStaticLootItem(cont, staticAmmoDist, id);
            positem.items[0].slotId = "main";
            fill += (positem.height * positem.width);

            if (fill > amount)
            {
                break;
            }
            
            for (const itm of positem.items)
            {
                rturn.push(itm);
            }
        }

        return rturn;
    }

    private findAndReturnChildrenByItems(items: Record<string, ITemplateItem>, itemID: string): string[]
    {
        const stack: string[] = [itemID];
        const result: string[] = [];
        let i = 0;
        
        if (itemID == "54009119af1c881c07000029")
        {
            for (const childItem of Object.keys(items))
            {
                result.push(childItem);
            }
            return result;
        }
        
        while (stack.length > 0)
        {
            i = 0;
            const currentItemId = stack.pop();
            for (const childItem of Object.keys(items))
            {
                if (items[childItem]._parent === currentItemId)
                {
                    stack.push(childItem);
                    i++;
                }
            }
            if (i == 0)
            {
                result.push(currentItemId);
            }
        }
        return result;
    }
    

    private changeChanceInPool(itemtpl: string, mult: number) : void
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const tables = databaseServer.getTables();
        const maps = ["bigmap", "woods", "factory4_day", "factory4_night", "interchange", "laboratory", "lighthouse", "rezervbase", "shoreline", "tarkovstreets", "sandbox", "sandbox_high"];
        for (const [name, temp] of Object.entries(tables.locations))
        {
            const mapdata:ILocation = temp;
            for (const Map of maps)
            {
                if (name === Map)
                {
                    for (const point of mapdata.looseLoot.spawnpoints)
                    {
                        for (const itm of point.template.Items)
                        {
                            if (itm._tpl == itemtpl)
                            {
                                const itmID = itm._id;
                                for (const dist of point.itemDistribution)
                                {
                                    if (dist.composedKey.key == itmID)
                                    {
                                        dist.relativeProbability *= mult
                                        if (Mod.config.general.debug) logger.info(`${name}, ${point.template.Id}, ${itm._tpl}, ${dist.relativeProbability}`);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private changeChancePool(itemtpl: string, mult: number) : void
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const tables = databaseServer.getTables();

        const maps = ["bigmap", "woods", "factory4_day", "factory4_night", "interchange", "laboratory", "lighthouse", "rezervbase", "shoreline", "tarkovstreets", "sandbox", "sandbox_high"];
        for (const [name, temp] of Object.entries(tables.locations))
        {
            const mapdata:ILocation = temp;
            for (const Map of maps)
            {
                if (name === Map)
                {
                    for (const point of mapdata.looseLoot.spawnpoints)
                    {
                        for (const itm of point.template.Items)
                        {
                            if (itm._tpl == itemtpl)
                            {
                                point.probability *= mult;
                                if (point.probability > 1)
                                {
                                    point.probability = 1;
                                }
                                if (Mod.config.general.debug) logger.info(`${name},   Pool:${point.template.Id},    Chance:${point.probability}`);
                            }
                        }
                    }
                }
            }
        }
    }

    private MarkedRoomChanges() : void
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const spawnPointscustoms = databaseServer.getTables().locations.bigmap.looseLoot.spawnpoints;
        const spawnPointsreserve = databaseServer.getTables().locations.rezervbase.looseLoot.spawnpoints;
        const spawnPointsstreets = databaseServer.getTables().locations.tarkovstreets.looseLoot.spawnpoints;
        
        for (const spawnpoint of spawnPointscustoms)
        {
            //Dorms 314 Marked Room
            if ((spawnpoint.template.Position.x > 180) && (spawnpoint.template.Position.x < 185) && (spawnpoint.template.Position.z > 180) && (spawnpoint.template.Position.z < 185) && (spawnpoint.template.Position.y > 6) && (spawnpoint.template.Position.y < 7))
            {
                if (Mod.config.general.debug) logger.info(`Customs ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.customs;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsreserve)
        {
            if ((spawnpoint.template.Position.x > -125) && (spawnpoint.template.Position.x < -120) && (spawnpoint.template.Position.z > 25) && (spawnpoint.template.Position.z < 30) && (spawnpoint.template.Position.y > -15) && (spawnpoint.template.Position.y < -14))
            {
                if (Mod.config.general.debug) logger.info(`Reserve ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.reserve;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
                
            }
            else if ((spawnpoint.template.Position.x > -155) && (spawnpoint.template.Position.x < -150) && (spawnpoint.template.Position.z > 70) && (spawnpoint.template.Position.z < 75) && (spawnpoint.template.Position.y > -9) && (spawnpoint.template.Position.y < -8))
            {
                if (Mod.config.general.debug) logger.info(`Reserve ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.reserve;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
            else if ((spawnpoint.template.Position.x > 190) && (spawnpoint.template.Position.x < 195) && (spawnpoint.template.Position.z > -230) && (spawnpoint.template.Position.z < -225) && (spawnpoint.template.Position.y > -6) && (spawnpoint.template.Position.y < -5))
            {
                if (Mod.config.general.debug) logger.info(`Reserve ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.reserve;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsstreets)
        {
            //Abandoned Factory Marked Room
            if ((spawnpoint.template.Position.x > -133) && (spawnpoint.template.Position.x < -129) && (spawnpoint.template.Position.z > 265) && (spawnpoint.template.Position.z < 275) && (spawnpoint.template.Position.y > 8.5) && (spawnpoint.template.Position.y < 11))
            {
                if (Mod.config.general.debug) logger.info(`Streets ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.streets;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
            //Chek 13 Marked Room
            else if ((spawnpoint.template.Position.x > 186) && (spawnpoint.template.Position.x < 191) && (spawnpoint.template.Position.z > 224) && (spawnpoint.template.Position.z < 229) && (spawnpoint.template.Position.y > -0.5) && (spawnpoint.template.Position.y < 1.5))
            {
                if (Mod.config.general.debug) logger.info(`Streets ${spawnpoint.template.Id}`);
                spawnpoint.probability *= Mod.config.markedRoom.multiplier.streets;
                this.markedExtraItemsFunc(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

    }

    private markedExtraItemsFunc(spawnpoint: Spawnpoint) : void
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        
        for (const ITEM of Object.entries(Mod.config.markedRoom.extraItems))
        {
            if (spawnpoint.template.Items.find(x => x._tpl === ITEM[0]))
            {
                continue;
            }
            const ID = Math.random()*10000;
            spawnpoint.template.Items.push({
                "_id": ID.toString(),
                "_tpl": ITEM[0]
            })
            spawnpoint.itemDistribution.push({
                "composedKey":{"key":ID.toString()},
                "relativeProbability": ITEM[1]
            });
            if (Mod.config.general.debug) logger.info(`Added ${ITEM[0]} to ${spawnpoint.template.Id}`);
        }
    }

    private markedItemGroups(spawnpoint: Spawnpoint) : void
    {
        const logger = Mod.container.resolve<ILogger>("WinstonLogger");
        const itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");

        for (const item of spawnpoint.template.Items)
        {
            for (const group in Mod.config.markedRoom.itemGroups)
            {
                if (itemHelper.isOfBaseclass(item._tpl, group))
                {
                    for (const dist of spawnpoint.itemDistribution)
                    {
                        if (dist.composedKey.key == item._id)
                        {
                            dist.relativeProbability *= Mod.config.markedRoom.itemGroups[group];
                            if (Mod.config.general.debug) logger.info(`Changed ${item._tpl} to ${dist.relativeProbability}`);
                        }
                    }
                }
            }
            
        }
        
    }

    private addToRustedKeyRoom() : void
    {
        const itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");
        const objectId = Mod.container.resolve<ObjectId>("ObjectId");
        const jsonUtil = Mod.container.resolve<JsonUtil>("JsonUtil");
        const databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        const tables = databaseServer.getTables();
        const streetsloot = tables.locations.tarkovstreets.looseLoot;
        const items = tables.templates.items;
        let keys:string[] = [];
        let valuables:string[] = [];
        
        for (const item in items)
        {
            try {
                if(Mod.config.general.rustedKeyRoomIncludesKeycards)
                {
                    if(itemHelper.isOfBaseclass(item,BaseClasses.KEY))
                    {
                        keys.push(item);
                    }
                }
                else
                {
                    if(itemHelper.isOfBaseclass(item,BaseClasses.KEY_MECHANICAL))
                    {
                        keys.push(item);
                    }
                }
                if(itemHelper.isOfBaseclass(item,BaseClasses.JEWELRY))
                {
                    valuables.push(item);
                }
            } 
            catch (error) {
                
            }
        }
        let point: Spawnpoint = {
            "locationId":"(185.087, 6.554, 63.721)",
            "probability": 0.25,
            "template": {
                "Id": "Keys1",
                "IsContainer":false,
                "useGravity": true,
                "randomRotation": true,
                "Position": {
                    "x": 185.087,
                    "y": 6.554,
                    "z": 63.721
                },
                "Rotation": {
                    "x": 0,
                    "y": 0,
                    "z": 0
                },
                "IsGroupPosition": false,
                "GroupPositions": [],
                "IsAlwaysSpawn": false,
                "Root": objectId.generate(),
                "Items": []
            },
            "itemDistribution":[]
        };
        for(let i = 0;i<keys.length;i++)
        {
            point.template.Items.push({
                "_id":i.toString(),
                "_tpl":keys[i]
            })
            point.itemDistribution.push({
                "composedKey":{"key":i.toString()},
                "relativeProbability":1
            })
        }
        streetsloot.spawnpoints.push(jsonUtil.clone(point));
        
        point.template.Root = objectId.generate();
        point.template.Id = "Keys2";
        point.template.Position = {
            "x": 185.125,
            "y": 6.554,
            "z": 63.186
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys3";
        point.template.Position = {
            "x": 185.164,
            "y": 6.554,
            "z": 62.241
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys4";
        point.template.Position = {
            "x": 185.154,
            "y": 6.554,
            "z": 62.686
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys5";
        point.template.Position = {
            "x": 185.210,
            "y": 6.935,
            "z": 60.860
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys6";
        point.template.Position = {
            "x": 185.205,
            "y": 6.935,
            "z": 60.560
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys7";
        point.template.Position = {
            "x": 185.208,
            "y": 6.580,
            "z": 60.857
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys8";
        point.template.Position = {
            "x": 185.211,
            "y": 6.562,
            "z": 60.562
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys9";
        point.template.Position = {
            "x": 185.202,
            "y": 6.175,
            "z": 60.551
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys10";
        point.template.Position = {
            "x": 185.200,
            "y": 6.234,
            "z": 60.872
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys11";
        point.template.Position = {
            "x": 182.683,
            "y": 6.721,
            "z": 57.813
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys12";
        point.template.Position = {
            "x": 182.683,
            "y": 6.721,
            "z": 60.073
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val1";
        point.template.Position = {
            "x": 185.037,
            "y": 5.831,
            "z": 53.836
        }
        point.template.Items = [];
        point.itemDistribution = [];
        for(let i = 0;i<valuables.length;i++)
        {
            point.template.Items.push({
                "_id":i.toString(),
                "_tpl":valuables[i]
            });
            point.itemDistribution.push({
                "composedKey":{"key":i.toString()},
                "relativeProbability":1
            });
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val2";
        point.template.Position = {
            "x": 183.064,
            "y": 5.831,
            "z": 53.767
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val3";
        point.template.Position = {
            "x": 185.146,
            "y": 5.831,
            "z": 60.114
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val4";
        point.template.Position = {
            "x": 185.085,
            "y": 5.831,
            "z": 65.393
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(jsonUtil.clone(point));
    }
}

module.exports = { mod: new Mod() }