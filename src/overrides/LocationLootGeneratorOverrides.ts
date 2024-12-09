import { inject, injectable } from "tsyringe";

import { BaseClasses } from "@spt/models/enums/BaseClasses";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";

import { IContainerItem, LocationLootGenerator } from "@spt/generators/LocationLootGenerator";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { PresetHelper } from "@spt/helpers/PresetHelper";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { DatabaseService } from "@spt/services/DatabaseService";
import { ItemFilterService } from "@spt/services/ItemFilterService";
import { LocalisationService } from "@spt/services/LocalisationService";
import { SeasonalEventService } from "@spt/services/SeasonalEventService";
import { HashUtil } from "@spt/utils/HashUtil";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { MathUtil } from "@spt/utils/MathUtil";
import { ProbabilityObject, ProbabilityObjectArray, RandomUtil } from "@spt/utils/RandomUtil";
import { ICloner } from "@spt/utils/cloners/ICloner";
import { LotsofLootItemHelper } from "../helpers/LotsofLootItemHelper";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

import { IStaticAmmoDetails } from "@spt/models/eft/common/ILocation";
import { ILooseLoot, ISpawnpoint, ISpawnpointTemplate, ISpawnpointsForced } from "@spt/models/eft/common/ILooseLoot";
import { IItem } from "@spt/models/eft/common/tables/IItem";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";

@injectable()
export class LocationLootGeneratorOverrides {
    private looseContainerItemFilterIndexCache: Record<string, string[]> = {};

    constructor(
        @inject("LocationLootGenerator") protected locationLootGenerator: LocationLootGenerator,
        @inject("ConfigServer") protected configServer: ConfigServer,
        @inject("ItemFilterService") protected itemFilterService: ItemFilterService,
        @inject("LocalisationService") protected localisationService: LocalisationService,
        @inject("SeasonalEventService") protected seasonalEventService: SeasonalEventService,
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("ItemHelper") protected itemHelper: ItemHelper,
        @inject("PresetHelper") protected presetHelper: PresetHelper,
        @inject("PrimaryCloner") protected cloner: ICloner,
        @inject("JsonUtil") protected jsonUtil: JsonUtil,
        @inject("HashUtil") protected hashUtil: HashUtil,
        @inject("RandomUtil") protected randomUtil: RandomUtil,
        @inject("MathUtil") protected mathUtil: MathUtil,
        @inject("LotsofLootItemHelper") protected lotsOfLootItemHelper: LotsofLootItemHelper,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {}

    // This method closely mirrors that of SPT
    // The only difference being the bypass for loot overlay and using createStaticLootItem
    public generateDynamicLoot(dynamicLootDist: ILooseLoot, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, locationName: string): ISpawnpointTemplate[] {
        const LocationConfig = this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        const loot: ISpawnpointTemplate[] = [];
        const dynamicForcedSpawnPoints: ISpawnpointsForced[] = [];

        // Build the list of forced loot from both `ISpawnpointsForced` and any point marked `IsAlwaysSpawn`
        dynamicForcedSpawnPoints.push(...dynamicLootDist.spawnpointsForced);
        dynamicForcedSpawnPoints.push(...dynamicLootDist.spawnpoints.filter((point) => point.template.IsAlwaysSpawn));

        // Temporary cast to get rid of protected, add all forced loot to return array
        (this.locationLootGenerator as any).addForcedLoot(loot, dynamicLootDist.spawnpointsForced, locationName);

        const allDynamicSpawnpoints = dynamicLootDist.spawnpoints;

        // Temporary cast to get rid of protected, draw from random distribution
        let desiredSpawnpointCount = Math.round((this.locationLootGenerator as any).getLooseLootMultiplerForLocation(locationName) * this.randomUtil.getNormallyDistributedRandomNumber(dynamicLootDist.spawnpointCount.mean, dynamicLootDist.spawnpointCount.std));

        if (desiredSpawnpointCount > this.config.getConfig().limits[locationName]) {
            desiredSpawnpointCount = this.config.getConfig().limits[locationName];
        }

        // Positions not in forced but have 100% chance to spawn
        const guaranteedLoosePoints: ISpawnpoint[] = [];

        const blacklistedSpawnpoints = LocationConfig.looseLootBlacklist[locationName];
        const spawnpointArray = new ProbabilityObjectArray<string, ISpawnpoint>(this.mathUtil, this.cloner);

        for (const spawnpoint of allDynamicSpawnpoints) {
            if (blacklistedSpawnpoints?.includes(spawnpoint.template.Id)) {
                this.logger.debug(`Ignoring loose loot location: ${spawnpoint.template.Id}`);
                continue;
            }

            // We've handled IsAlwaysSpawn above, so skip them
            if (spawnpoint.template.IsAlwaysSpawn) {
                continue;
            }

            if (spawnpoint.probability === 1) {
                guaranteedLoosePoints.push(spawnpoint);
            }

            spawnpointArray.push(new ProbabilityObject(spawnpoint.template.Id, spawnpoint.probability, spawnpoint));
        }

        // Select a number of spawn points to add loot to
        // Add ALL loose loot with 100% chance to pool
        let chosenSpawnpoints: ISpawnpoint[] = [...guaranteedLoosePoints];

        const randomSpawnpointCount = desiredSpawnpointCount - chosenSpawnpoints.length;
        // Only draw random spawn points if needed
        if (randomSpawnpointCount) {
            // Add randomly chosen spawn points
            for (const si of spawnpointArray.draw(randomSpawnpointCount, true)) {
                chosenSpawnpoints.push(spawnpointArray.data(si));
            }
        }

        if (!this.config.getConfig().general.allowLootOverlay) {
            // Filter out duplicate locationIds
            chosenSpawnpoints = [...new Map(chosenSpawnpoints.map((spawnPoint) => [spawnPoint.locationId, spawnPoint])).values()];

            // Do we have enough items in pool to fulfill requirement
            const tooManySpawnPointsRequested = desiredSpawnpointCount - chosenSpawnpoints.length > 0;
            if (tooManySpawnPointsRequested) {
                this.logger.debug(
                    this.localisationService.getText("location-spawn_point_count_requested_vs_found", {
                        requested: desiredSpawnpointCount + guaranteedLoosePoints.length,
                        found: chosenSpawnpoints.length,
                        mapName: locationName,
                    }),
                );
            }
        }

        // Iterate over spawnpoints
        const seasonalEventActive = this.seasonalEventService.seasonalEventEnabled();
        const seasonalItemTplBlacklist = this.seasonalEventService.getInactiveSeasonalEventItems();
        for (const spawnPoint of chosenSpawnpoints) {
            if (!spawnPoint.template) {
                this.logger.warning(this.localisationService.getText("location-missing_dynamic_template", spawnPoint.locationId));
                continue;
            }

            // Ensure no blacklisted lootable items are in pool
            spawnPoint.template.Items = spawnPoint.template.Items.filter((item) => !this.itemFilterService.isLootableItemBlacklisted(item._tpl));

            // Ensure no seasonal items are in pool if not in-season
            if (!seasonalEventActive) {
                spawnPoint.template.Items = spawnPoint.template.Items.filter((item) => !seasonalItemTplBlacklist.includes(item._tpl));
            }

            // Spawn point has no items after filtering, skip
            if (!spawnPoint.template.Items || spawnPoint.template.Items.length === 0) {
                this.logger.warning(this.localisationService.getText("location-spawnpoint_missing_items", spawnPoint.template.Id));

                continue;
            }

            // Get an array of allowed IDs after above filtering has occured
            const validItemIds = spawnPoint.template.Items.map((item) => item._id);

            // Spawn point has no items after filtering, skip
            const itemArray = new ProbabilityObjectArray<string>(this.mathUtil, this.cloner);
            for (const itemDist of spawnPoint.itemDistribution) {
                if (!validItemIds.includes(itemDist.composedKey.key)) {
                    continue;
                }

                itemArray.push(new ProbabilityObject(itemDist.composedKey.key, itemDist.relativeProbability));
            }

            if (itemArray.length === 0) {
                this.logger.warning(this.localisationService.getText("location-loot_pool_is_empty_skipping", spawnPoint.template.Id));

                continue;
            }

            // Draw a random item from spawn points possible items
            const spawnPointClone = this.cloner.clone(spawnPoint);
            const chosenComposedKey = itemArray.draw(1)[0];
            const chosenItem = spawnPointClone.template.Items.find((x) => x._id === chosenComposedKey);
            const chosenTpl = chosenItem._tpl;
            const createItemResult = this.createStaticLootItem(chosenTpl, staticAmmoDist, undefined, spawnPointClone);

            // Root id can change when generating a weapon
            spawnPointClone.template.Root = createItemResult.items[0]._id;
            spawnPointClone.template.Items = createItemResult.items;

            loot.push(spawnPointClone.template);
        }

        return loot;
    }

    public createStaticLootItem(tpl: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, parentId: string = undefined, spawnPoint: ISpawnpoint = undefined): IContainerItem {
        const LocationConfig = this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        const gotItem = this.itemHelper.getItem(tpl);
        let itemTemplate: ITemplateItem;
        if (gotItem[0]) {
            itemTemplate = gotItem[1];
        } else {
            return {
                items: [],
                width: 0,
                height: 0,
            };
        }

        let width = itemTemplate._props.Width;
        let height = itemTemplate._props.Height;
        let items: IItem[] = [
            {
                _id: this.hashUtil.generate(),
                _tpl: tpl,
            },
        ];

        // container item has the container's id as the parentId
        if (parentId) {
            items[0].parentId = parentId;
        }

        if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.WEAPON)) {
            if (spawnPoint != undefined) {
                const chosenItem = spawnPoint.template.Items.find((x) => x._tpl === tpl);
                // Get item + it's children, then replace ids of children with valid MongoIDs before returning them to the items arrray.
                const itemWithChildren = this.itemHelper.replaceIDs(this.itemHelper.findAndReturnChildrenAsItems(spawnPoint.template.Items, chosenItem._id));

                items.splice(0, 1);
                items.push(...itemWithChildren);
            } else {
                let children: IItem[] = [];
                const defaultPreset = this.cloner.clone(this.presetHelper.getDefaultPreset(tpl));
                if (defaultPreset) {
                    children = this.itemHelper.reparentItemAndChildren(defaultPreset._items[0], defaultPreset._items);
                } else {
                    // RSP30 (62178be9d0050232da3485d9/624c0b3340357b5f566e8766) doesnt have any default presets and kills this code below as it has no chidren to reparent
                    this.logger.debug(`LocationLootGeneratorOverrides::createStaticLootItem: No preset found for weapon: ${tpl}`);
                }

                const rootItem = items[0];
                if (!rootItem) {
                    this.logger.logError(this.localisationService.getText("location-missing_root_item", { tpl: tpl, parentId: parentId }));

                    throw new Error(this.localisationService.getText("location-critical_error_see_log"));
                }

                if (children?.length > 0) {
                    items = this.itemHelper.reparentItemAndChildren(rootItem, children);
                }

                const magazine = items.filter((x) => x.slotId === "mod_magazine")[0];
                // some weapon presets come without magazine; only fill the mag if it exists and if it has a good roll.
                if (magazine && this.randomUtil.getChance100(LocationConfig.magazineLootHasAmmoChancePercent)) {
                    const magTemplate = this.itemHelper.getItem(magazine._tpl)[1];
                    const weaponTemplate = this.itemHelper.getItem(tpl)[1];

                    // Create array with just magazine
                    const magazineWithCartridges: IItem[] = [];
                    magazineWithCartridges.push(magazine);

                    this.itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, magTemplate, staticAmmoDist, weaponTemplate._props.ammoCaliber, LocationConfig.minFillStaticMagazinePercent / 100);

                    // Replace existing magazine with above array
                    items.splice(items.indexOf(magazine), 1, ...magazineWithCartridges);
                }

                const size = this.itemHelper.getItemSize(items, rootItem._id);
                width = size.width;
                height = size.height;
            }
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MONEY) || this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO)) {
            const stackCount = this.randomUtil.getInt(itemTemplate._props.StackMinRandom, itemTemplate._props.StackMaxRandom);
            items[0].upd = { StackObjectsCount: stackCount };
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO_BOX)) {
            this.itemHelper.addCartridgesToAmmoBox(items, itemTemplate);
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MAGAZINE)) {
            if (this.randomUtil.getChance100(LocationConfig.magazineLootHasAmmoChancePercent)) {
                // Create array with just magazine
                const magazineWithCartridges: IItem[] = [];
                magazineWithCartridges.push(items[0]);

                this.itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, itemTemplate, staticAmmoDist, null, LocationConfig.minFillStaticMagazinePercent / 100);

                // Replace existing magazine with above array
                items.splice(items.indexOf(items[0]), 1, ...magazineWithCartridges);
            }
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER) && tpl != "5c093e3486f77430cb02e593") {
            const containerLoot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, this.config.getConfig().general.looseContainerModifier);
            this.logger.debug(`Container ${tpl} with`);

            for (const containerItem of containerLoot) {
                this.logger.debug(`${containerItem._tpl}`);
                items.push(containerItem);
            }
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.BACKPACK)) {
            const containerLoot = this.createLooseContainerLoot(items[0]._tpl, items[0]._id, staticAmmoDist, this.config.getConfig().general.looseBackpackModifier);
            this.logger.debug(`Backpack ${tpl} with`);

            for (const containerItem of containerLoot) {
                this.logger.debug(`${containerItem._tpl}`);
                items.push(containerItem);
            }
        } else if (this.itemHelper.armorItemCanHoldMods(tpl)) {
            const defaultPreset = this.presetHelper.getDefaultPreset(tpl);
            if (defaultPreset) {
                const presetAndMods: IItem[] = this.itemHelper.replaceIDs(defaultPreset._items);
                this.itemHelper.remapRootItemId(presetAndMods);

                // Use original items parentId otherwise item doesnt get added to container correctly
                presetAndMods[0].parentId = items[0].parentId;
                items = presetAndMods;
            } else {
                // We make base item above, at start of function, no need to do it here
                if (itemTemplate._props.Slots?.length > 0) {
                    items = this.itemHelper.addChildSlotItems(items, itemTemplate, LocationConfig.equipmentLootSettings.modSpawnChancePercent);
                }
            }
        }

        return {
            items: items,
            width: width,
            height: height,
        };
    }

    public createLooseContainerLoot(tpl: string, id: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, modifier = 0.5): IItem[] {
        if (modifier === 0) {
            return [];
        }

        const tables = this.databaseService.getTables();

        const items = tables.templates.items;
        const item = items[tpl];

        if (item._props.Grids[0]._props.filters[0] === undefined) {
            this.logger.debug(`${item._name} doesn't have a filter, setting default filter!`);
            item._props.Grids[0]._props.filters = [
                {
                    Filter: ["54009119af1c881c07000029"],
                    ExcludedFilter: [],
                },
            ];
        }

        let whitelist = this.cloner.clone(item._props.Grids[0]._props.filters[0].Filter);
        let blacklist = this.cloner.clone(item._props?.Grids[0]._props.filters[0]?.ExcludedFilter) ?? [];
        const amount = this.randomUtil.getInt(1, item._props.Grids[0]._props.cellsH * item._props.Grids[0]._props.cellsV * modifier);
        let fill = 0;

        if (this.looseContainerItemFilterIndexCache[tpl]) {
            whitelist = this.looseContainerItemFilterIndexCache[tpl];
        } else {
            this.logger.debug(`${tpl} is new, generating whitelist`);

            const newWhiteList: string[] = [];
            const newBlackList: string[] = [];

            //If whitelist contains a parent instead of items, replace the parent by all its children.
            for (const content of whitelist) {
                const childItems = this.lotsOfLootItemHelper.findAndReturnChildrenItemIdsByItems(items, content);
                newWhiteList.push(...childItems);
            }

            whitelist = newWhiteList;

            //If blacklist contains a parent instead of items, replace the parent by all its children.
            for (const content of blacklist) {
                const childItems = this.lotsOfLootItemHelper.findAndReturnChildrenItemIdsByItems(items, content);
                newBlackList.push(...childItems);
            }

            blacklist = newBlackList;

            for (const whitelistEntry in whitelist) {
                //If whitelist contains entries that are in the blacklist, remove them.
                if (blacklist[whitelistEntry]) {
                    whitelist.splice(whitelist.indexOf(whitelistEntry), 1);
                }
            }

            //Extra restrictions to avoid errors
            for (let white = 0; white < whitelist.length; white++) {
                //Remove built in inserts, these can not be used.
                if (this.itemHelper.isOfBaseclass(whitelist[white], BaseClasses.BUILT_IN_INSERTS)) {
                    whitelist.splice(white, 1);
                    white--;
                }

                //Validate if item is actually valid (Not a quest item or blacklisted) or if an item actually has a model.
                if (!this.itemHelper.isValidItem(whitelist[white]) || items[whitelist[white]]._props.Prefab.path == "") {
                    whitelist.splice(white, 1);
                    white--;
                }
            }

            //Write new entry to cache for later re-use.
            this.looseContainerItemFilterIndexCache[tpl] = whitelist;
        }

        if (whitelist.length == 0) {
            this.logger.warning(`${tpl} whitelist is empty`);
            return [];
        }

        const itemArray = new ProbabilityObjectArray<string>(this.mathUtil, this.cloner);

        for (let i = 0; i < whitelist.length; i++) {
            let itemWeight = 1;

            if (tables.templates.prices[whitelist[i]]) {
                itemWeight = Math.round(1000 / Math.pow(tables.templates.prices[whitelist[i]], 1 / 3));
            } else if (whitelist[i] === "5449016a4bdc2d6f028b456f") {
                itemWeight = 500;
            } else if (whitelist[i] === "5696686a4bdc2da3298b456a") {
                itemWeight = 100;
            } else if (whitelist[i] === "569668774bdc2da2298b4568") {
                itemWeight = 100;
            }

            //this.logger.debug(`LocationLootGeneratorOverrides::createLooseContainerLoot: Weight of ${this.logger.writeItemName(whitelist[i], true)} is ${itemWeight} for ${tpl}`)

            itemArray.push(new ProbabilityObject(whitelist[i], itemWeight));
        }

        const generatedItems: IItem[] = [];

        while (true) {
            let drawnItemTpl: string;
            if (this.config.getConfig().general.itemWeights) {
                drawnItemTpl = itemArray.draw(1, true)[0];
            } else {
                drawnItemTpl = whitelist[this.randomUtil.getInt(0, whitelist.length - 1)];
            }

            const lootItem = this.createStaticLootItem(drawnItemTpl, staticAmmoDist, id);
            lootItem.items[0].slotId = "main";
            fill += lootItem.height * lootItem.width;

            if (fill > amount) {
                break;
            }

            for (const item of lootItem.items) {
                generatedItems.push(item);
            }
        }

        return generatedItems;
    }
}
