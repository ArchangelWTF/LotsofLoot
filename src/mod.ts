/* eslint-disable @typescript-eslint/naming-convention */
/* eslint-disable @typescript-eslint/indent */
/* eslint-disable no-constant-condition */
/* eslint-disable prefer-spread */
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
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";
import { SeasonalEventService } from "@spt/services/SeasonalEventService";
import { ILocation, IStaticAmmoDetails } from "@spt/models/eft/common/ILocation";
import { VFS } from "@spt/utils/VFS";
import { MarkedRoom } from "./MarkedRoom";
import { HashUtil } from "@spt/utils/HashUtil";
import { ICloner } from "@spt/utils/cloners/ICloner";
import { ILotsofLootConfig } from "./ILotsofLootConfig";
import { LotsofLootLogger } from "./LotsofLootLogger";
import { LotsofLootHelper } from "./LotsofLootHelper";

class Mod implements IPreSptLoadMod, IPostDBLoadMod
{
    private static config: ILotsofLootConfig = null;

    private static container: DependencyContainer;
    private logger: LotsofLootLogger;
    private databaseServer: DatabaseServer
    private itemHelper: ItemHelper;
    private cloner: ICloner;

    private markedRoom: MarkedRoom;
    private lotsoflootHelper: LotsofLootHelper;

    public preSptLoad(container: DependencyContainer): void
    {
        Mod.container = container;

        // Get VFS to read in configs
        const vfs = container.resolve<VFS>("VFS");
        // Load config statically so that we don't have to keep reloading this across the entire file.
        Mod.config = JSON5.parse(vfs.readFile(path.resolve(__dirname, "../config/config.json5")));

        this.logger = new LotsofLootLogger(container.resolve<ILogger>("WinstonLogger"), Mod.config.general.debug)
        this.markedRoom = new MarkedRoom(Mod.config.markedRoom, container.resolve<DatabaseServer>("DatabaseServer"), container.resolve<ItemHelper>("ItemHelper"), container.resolve<HashUtil>("HashUtil"), this.logger);
        this.lotsoflootHelper = new LotsofLootHelper(Mod.config, container.resolve<DatabaseServer>("DatabaseServer"), container.resolve<ItemHelper>("ItemHelper"), this.logger);
        
        container.afterResolution("LocationGenerator", (_t, result: LocationGenerator) =>
        {
            //Temporary cast to get rid of protected error
            (result as any).createStaticLootItem = (tpl, staticAmmoDist, parentId) =>
            {
                return this.createStaticLootItem(tpl, staticAmmoDist, parentId);
            }

            result.generateDynamicLoot = (dynamicLootDist, staticAmmoDist, locationName) => 
            {
                return this.generateDynamicLoot(dynamicLootDist, staticAmmoDist, locationName);
            }
        }, {frequency: "Always"});
    }

    public postDBLoad(container: DependencyContainer): void
    {
        this.databaseServer = Mod.container.resolve<DatabaseServer>("DatabaseServer");
        this.itemHelper = Mod.container.resolve<ItemHelper>("ItemHelper");
        this.cloner = Mod.container.resolve<ICloner>("PrimaryCloner");

        const tables = this.databaseServer.getTables();
        const configServer = container.resolve<ConfigServer>("ConfigServer");
        const locations = this.databaseServer.getTables().locations;
        const LocationConfig = configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        this.markedRoom.doMarkedRoomChanges();
        this.addToRustedKeyRoom();

        if(Mod.config.general.removeBackpackRestrictions)
        {
            this.lotsoflootHelper.removeBackpackRestrictions();
        }
        
        for (const map in Mod.config.looseLootMultiplier)
        {
            LocationConfig.looseLootMultiplier[map] = Mod.config.looseLootMultiplier[map];
            this.logger.logDebug(`${map}: ${LocationConfig.looseLootMultiplier[map]}`);
            LocationConfig.staticLootMultiplier[map] = Mod.config.staticLootMultiplier[map];
            this.logger.logDebug(`${map}: ${LocationConfig.staticLootMultiplier[map]}`)
        }

        for(const locationId in locations)
        {
            if(locations.hasOwnProperty(locationId))
            {
                const location: ILocation = locations[locationId];
                //Location does not have any static loot pools, skip this map.
                if(!location.staticLoot)
                {
                    this.logger.logDebug(`Skipping ${locationId} as it has no static loot`);

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
                            
                            this.logger.logDebug(`Changed container ${container} chance to ${staticLoot[container].itemcountDistribution[possItemCount].relativeProbability}`)
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
                if (this.itemHelper.isValidItem(tables.templates.items[item]._id))
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

        this.logger.logInfo(`Finished loading`);
    }

    private generateDynamicLoot(dynamicLootDist: ILooseLoot, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, locationName: string): SpawnpointTemplate[]
    {
        const locationGenerator = Mod.container.resolve<LocationGenerator>("LocationGenerator");
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
                this.logger.logDebug(`Ignoring loose loot location: ${spawnpoint.template.Id}`);
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
                this.logger.logDebug(
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
                this.logger.logWarning(localisationService.getText("location-missing_dynamic_template", spawnPoint.locationId));
                continue;
            }

            if (!spawnPoint.template.Items || spawnPoint.template.Items.length === 0)
            {
                this.logger.logError(localisationService.getText("location-spawnpoint_missing_items", spawnPoint.template.Id));
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
                this.logger.logWarning(`Loot pool for position: ${spawnPoint.template.Id} is empty. Skipping`);

                continue;
            }

            // Draw a random item from spawn points possible items
            const spawnPointClone = this.cloner.clone(spawnPoint);
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
        const objectId = Mod.container.resolve<ObjectId>("ObjectId");
        const presetHelper = Mod.container.resolve<PresetHelper>("PresetHelper");
        const locationGenerator = Mod.container.resolve<LocationGenerator>("LocationGenerator");
        const randomUtil = Mod.container.resolve<RandomUtil>("RandomUtil");
        const localisationService = Mod.container.resolve<LocalisationService>("LocalisationService");
        const configServer = Mod.container.resolve<ConfigServer>("ConfigServer");
        const LocationConfig = configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);
        
        const gotItem = this.itemHelper.getItem(tpl);
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

        if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.WEAPON))
        {
            if (spawnPoint != undefined)
            {
                const chosenItem = spawnPoint.template.Items.find(x => x._tpl === tpl);
                // Get item + children and add into array we return
                const itemWithChildren = this.itemHelper.findAndReturnChildrenAsItems(spawnPoint.template.Items, chosenItem._id);
                // Temporary cast to get rid of protected error, we need to reparent to ensure ids are unique
                (locationGenerator as any).reparentItemAndChildren(itemWithChildren);
                items.splice(0,1);
                items.push(...itemWithChildren);
            }
            else
            {
                let children: Item[] = [];
                const defaultPreset = this.cloner.clone(presetHelper.getDefaultPreset(tpl));
                if (defaultPreset)
                {
                    try
                    {
                        children = this.itemHelper.reparentItemAndChildren(defaultPreset._items[0], defaultPreset._items);
                    }
                    catch (error)
                    {
                        // this item already broke it once without being reproducible tpl = "5839a40f24597726f856b511"; AKS-74UB Default
                        // 5ea03f7400685063ec28bfa8 // ppsh default
                        // 5ba26383d4351e00334c93d9 //mp7_devgru
                        this.logger.logWarning(localisationService.getText("location-preset_not_found", { tpl: tpl, defaultId: defaultPreset._id, defaultName: defaultPreset._name, parentId: parentId }));
                        throw error;
                    }
                }
                else
                {
                    // RSP30 (62178be9d0050232da3485d9/624c0b3340357b5f566e8766) doesnt have any default presets and kills this code below as it has no chidren to reparent
                    this.logger.logDebug(`createItem() No preset found for weapon: ${tpl}`);
                }

                const rootItem = items[0];
                if (!rootItem)
                {
                    this.logger.logError(localisationService.getText("location-missing_root_item", { tpl: tpl, parentId: parentId }));

                    throw new Error(localisationService.getText("location-critical_error_see_log"));
                }

                try
                {
                    if (children?.length > 0)
                    {
                        items = this.itemHelper.reparentItemAndChildren(rootItem, children);
                    }
                }
                catch (error)
                {
                    this.logger.logError(localisationService.getText("location-unable_to_reparent_item", { tpl: tpl, parentId: parentId }));

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
                    const magTemplate = this.itemHelper.getItem(magazine._tpl)[1];
                    const weaponTemplate = this.itemHelper.getItem(tpl)[1];

                    // Create array with just magazine
                    const magazineWithCartridges = [magazine];
                    this.itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, magTemplate, staticAmmoDist, weaponTemplate._props.ammoCaliber);

                    // Replace existing magazine with above array
                    items.splice(items.indexOf(magazine), 1, ...magazineWithCartridges);
                }

                const size = this.itemHelper.getItemSize(items, rootItem._id);
                width = size.width;
                height = size.height;
            }
            
        }
        else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MONEY) || this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO)) 
        {
            const stackCount = randomUtil.getInt(itemTemplate._props.StackMinRandom, itemTemplate._props.StackMaxRandom);
            items[0].upd = { "StackObjectsCount": stackCount };
        }
        else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO_BOX))
        {
            this.itemHelper.addCartridgesToAmmoBox(items, itemTemplate);
        }
        else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MAGAZINE))
        {
            if (randomUtil.getChance100(LocationConfig.magazineLootHasAmmoChancePercent))
            {
                // Create array with just magazine
                const magazineWithCartridges = [items[0]];
                this.itemHelper.fillMagazineWithRandomCartridge(
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
        else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER) && (tpl != "5c093e3486f77430cb02e593"))
        {
            const contloot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, Mod.config.general.looseContainerModifier);
            this.logger.logDebug(`Container ${tpl} with`);
            for (const cont of contloot) 
            {
                this.logger.logDebug(`${cont._tpl}`);
                items.push(cont);
            }
        }
        else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.BACKPACK))
        {
            const contloot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, Mod.config.general.looseBackpackModifier);
            this.logger.logDebug(`Backpack ${tpl} with`);
            for (const cont of contloot) 
            {
                this.logger.logDebug(`${cont._tpl}`);
                items.push(cont);
            }
        }
        else if (this.itemHelper.armorItemCanHoldMods(tpl))
        {
            const defaultPreset = presetHelper.getDefaultPreset(tpl);
            if (defaultPreset)
            {
                const presetAndMods: Item[] = this.itemHelper.replaceIDs(defaultPreset._items);
                this.itemHelper.remapRootItemId(presetAndMods);

                // Use original items parentId otherwise item doesnt get added to container correctly
                presetAndMods[0].parentId = items[0].parentId;
                items = presetAndMods;
            }
            else
            {
                // We make base item above, at start of function, no need to do it here
                if (itemTemplate._props.Slots?.length > 0)
                {
                    items = this.itemHelper.addChildSlotItems(
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

    private looseContainerItemFilterIndex: Record<string, string[]> = {};
    
    private createLooseContainerLoot(tpl: string, id: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, modifier = 0.5): Item[]
    {
        const randomUtil = Mod.container.resolve<RandomUtil>("RandomUtil");
        const mathUtil = Mod.container.resolve<MathUtil>("MathUtil");
        const jsonUtil = Mod.container.resolve<JsonUtil>("JsonUtil");

        const tables = this.databaseServer.getTables();

        const items = tables.templates.items;
        const item = items[tpl];

        if (item._props.Grids[0]._props.filters[0] === undefined)
        {
            this.logger.logWarning(`${item._name} doesn't have a filter, setting default filter!`);
            item._props.Grids[0]._props.filters =
            [
                {
                    Filter: ["54009119af1c881c07000029"],
                    ExcludedFilter: []
                }
            ];
        }

        let whitelist = this.cloner.clone(item._props.Grids[0]._props.filters[0].Filter);
        let blacklist = this.cloner.clone(item._props?.Grids[0]._props.filters[0]?.ExcludedFilter) ?? [];
        const amount = randomUtil.getInt(1, item._props.Grids[0]._props.cellsH * item._props.Grids[0]._props.cellsV * modifier);
        let fill = 0;

        if(this.looseContainerItemFilterIndex[tpl])
        {
            whitelist = this.looseContainerItemFilterIndex[tpl];
        }
        else
        {
            this.logger.logDebug(`${tpl} is new, generating whitelist`);

            const newWhiteList: string[] = [];
            const newBlackList: string[] = [];

            //If whitelist contains a parent instead of items, replace the parent by all its children.
            for (const content of whitelist)
            {
                const childItems = this.findAndReturnChildrenByItems(items, content);
                newWhiteList.push(...childItems);
            }

            whitelist = newWhiteList;

            //If blacklist contains a parent instead of items, replace the parent by all its children.
            for (const content of blacklist)
            {
                const childItems = this.findAndReturnChildrenByItems(items, content);
                newBlackList.push(...childItems);
            }

            blacklist = newBlackList;

            for (const whitelistEntry in whitelist)
            {
                //If whitelist contains entries that are in the blacklist, remove them.
                if(blacklist[whitelistEntry])
                {
                    whitelist.splice(whitelist.indexOf(whitelistEntry), 1);
                }
            }

            //Extra restrictions to avoid errors
            for (let white = 0; white < whitelist.length; white++)
            {
                if (!this.itemHelper.isValidItem(whitelist[white])) //Checks if the Item can be in your Stash
                {
                    if (whitelist[white] == "5449016a4bdc2d6f028b456f")
                    {
                        continue;
                    }
                    whitelist.splice(white, 1);
                    white--;
                }
                else if (items[whitelist[white]]._props.Prefab.path == "") //If the Item has no model it cant be valid
                {
                    whitelist.splice(white, 1);
                    white--;
                }
            }

            //Write new entry for later re-use.
            this.looseContainerItemFilterIndex[tpl] = whitelist;
        }
        
        if (whitelist.length == 0)
        {
            this.logger.logWarning(`${tpl} whitelist is empty`);
            return [];
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

        const generatedItems:Item[] = [];

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
                generatedItems.push(itm);
            }
        }

        return generatedItems;
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
        const tables = this.databaseServer.getTables();
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

                                        this.logger.logDebug(`${name}, ${point.template.Id}, ${itm._tpl}, ${dist.relativeProbability}`);
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
        const tables = this.databaseServer.getTables();

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

                                this.logger.logDebug(`${name},   Pool:${point.template.Id},    Chance:${point.probability}`);
                            }
                        }
                    }
                }
            }
        }
    }

    private addToRustedKeyRoom() : void
    {
        const objectId = Mod.container.resolve<ObjectId>("ObjectId");
        const tables = this.databaseServer.getTables();
        const streetsloot = tables.locations.tarkovstreets.looseLoot;
        const items = tables.templates.items;
        let keys:string[] = [];
        let valuables:string[] = [];
        
        for (const item in items)
        {
            try {
                if(Mod.config.general.rustedKeyRoomIncludesKeycards)
                {
                    if(this.itemHelper.isOfBaseclass(item,BaseClasses.KEY))
                    {
                        keys.push(item);
                    }
                }
                else
                {
                    if(this.itemHelper.isOfBaseclass(item,BaseClasses.KEY_MECHANICAL))
                    {
                        keys.push(item);
                    }
                }
                if(this.itemHelper.isOfBaseclass(item,BaseClasses.JEWELRY))
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
        streetsloot.spawnpoints.push(this.cloner.clone(point));
        
        point.template.Root = objectId.generate();
        point.template.Id = "Keys2";
        point.template.Position = {
            "x": 185.125,
            "y": 6.554,
            "z": 63.186
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys3";
        point.template.Position = {
            "x": 185.164,
            "y": 6.554,
            "z": 62.241
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys4";
        point.template.Position = {
            "x": 185.154,
            "y": 6.554,
            "z": 62.686
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys5";
        point.template.Position = {
            "x": 185.210,
            "y": 6.935,
            "z": 60.860
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys6";
        point.template.Position = {
            "x": 185.205,
            "y": 6.935,
            "z": 60.560
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys7";
        point.template.Position = {
            "x": 185.208,
            "y": 6.580,
            "z": 60.857
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys8";
        point.template.Position = {
            "x": 185.211,
            "y": 6.562,
            "z": 60.562
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys9";
        point.template.Position = {
            "x": 185.202,
            "y": 6.175,
            "z": 60.551
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys10";
        point.template.Position = {
            "x": 185.200,
            "y": 6.234,
            "z": 60.872
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys11";
        point.template.Position = {
            "x": 182.683,
            "y": 6.721,
            "z": 57.813
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Keys12";
        point.template.Position = {
            "x": 182.683,
            "y": 6.721,
            "z": 60.073
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

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
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val2";
        point.template.Position = {
            "x": 183.064,
            "y": 5.831,
            "z": 53.767
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val3";
        point.template.Position = {
            "x": 185.146,
            "y": 5.831,
            "z": 60.114
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = objectId.generate();
        point.template.Id = "Val4";
        point.template.Position = {
            "x": 185.085,
            "y": 5.831,
            "z": 65.393
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));
    }
}

module.exports = { mod: new Mod() }