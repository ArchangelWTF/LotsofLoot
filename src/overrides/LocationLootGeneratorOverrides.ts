import { inject, injectable } from "tsyringe";

import { ConfigTypes } from "@spt/models/enums/ConfigTypes";

import { IContainerItem, LocationLootGenerator } from "@spt/generators/LocationLootGenerator";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { ItemFilterService } from "@spt/services/ItemFilterService";
import { LocalisationService } from "@spt/services/LocalisationService";
import { SeasonalEventService } from "@spt/services/SeasonalEventService";
import { MathUtil } from "@spt/utils/MathUtil";
import { ProbabilityObject, ProbabilityObjectArray, RandomUtil } from "@spt/utils/RandomUtil";
import { ICloner } from "@spt/utils/cloners/ICloner";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

import { IStaticAmmoDetails } from "@spt/models/eft/common/ILocation";
import { ILooseLoot, ISpawnpoint, ISpawnpointTemplate, ISpawnpointsForced } from "@spt/models/eft/common/ILooseLoot";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";
import { LotsofLootLocationLootGenerator } from "../generators/LotsofLootLocationLootGenerator";

@injectable()
export class LocationLootGeneratorOverrides {
    constructor(
        @inject("LocationLootGenerator") protected locationLootGenerator: LocationLootGenerator,
        @inject("ConfigServer") protected configServer: ConfigServer,
        @inject("ItemFilterService") protected itemFilterService: ItemFilterService,
        @inject("LocalisationService") protected localisationService: LocalisationService,
        @inject("SeasonalEventService") protected seasonalEventService: SeasonalEventService,
        @inject("PrimaryCloner") protected cloner: ICloner,
        @inject("RandomUtil") protected randomUtil: RandomUtil,
        @inject("MathUtil") protected mathUtil: MathUtil,
        @inject("LotsofLootLocationLootGenerator") protected lotsOfLootLocationLootGenerator: LotsofLootLocationLootGenerator,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {}

    // This method closely mirrors that of SPT
    // The only difference being the bypass for loot overlay and using Lots of Loot's createStaticLootItem
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
            const createItemResult = this.createStaticLootItem(chosenTpl, staticAmmoDist, undefined);

            // Root id can change when generating a weapon
            spawnPointClone.template.Root = createItemResult.items[0]._id;
            spawnPointClone.template.Items = createItemResult.items;

            loot.push(spawnPointClone.template);
        }

        return loot;
    }

    public createStaticLootItem(tpl: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, parentId: string = undefined): IContainerItem {
        return this.lotsOfLootLocationLootGenerator.createStaticLootItem(tpl, staticAmmoDist, parentId);
    }
}
