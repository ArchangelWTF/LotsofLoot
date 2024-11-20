import { inject, injectable } from "tsyringe";

import { ILocation } from "@spt/models/eft/common/ILocation";
import { DatabaseService } from "@spt/services/DatabaseService";

import { LotsofLootLogger } from "../utils/LotsofLootLogger";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";

@injectable()
export class LotsofLootHelper {
    constructor(
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
    ) {
    }

    //This function is heavily based off of SVM's 'RemoveBackpacksRestrictions'
    //Huge credit to GhostFenixx (SVM) for this
    public removeBackpackRestrictions(): void {
        const items = this.databaseService.getTables().templates.items;

        for (let key in items) {
            let value = items[key];

            if (value._parent === "5448e53e4bdc2d60728b4567" && value._props.Grids[0]._props.filters !== undefined) {
                value._props.Grids[0]._props.filters = [];
            }
        }
    }

    public changeRelativeProbabilityInPool(itemtpl: string, mult: number): void {
        const locations = this.databaseService.getTables().locations;

        for (const locationId in locations) {
            if (locations.hasOwnProperty(locationId)) {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.debug(`Skipping ${locationId} as it has no loose loot!`);
                    continue;
                }

                location.looseLoot.spawnpoints.forEach((spawnpoint) => {
                    const item = spawnpoint.template.Items.find((i) => i._tpl == itemtpl);

                    if (item) {
                        const itemDistribution = spawnpoint.itemDistribution.find((i) => i.composedKey.key == item._id);

                        if (itemDistribution) {
                            itemDistribution.relativeProbability *= mult;

                            this.logger.debug(`${locationId}, ${spawnpoint.template.Id}, ${item._tpl}, ${itemDistribution.relativeProbability}`);
                        }
                    }
                });
            }
        }
    }

    public changeProbabilityOfPool(itemtpl: string, mult: number): void {
        const locations = this.databaseService.getTables().locations;

        for (const locationId in locations) {
            if (locations.hasOwnProperty(locationId)) {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.debug(`Skipping ${locationId} as it has no loose loot!`);
                    continue;
                }

                location.looseLoot.spawnpoints.forEach((spawnpoint) => {
                    const item = spawnpoint.template.Items.find((i) => i._tpl == itemtpl);

                    if (item) {
                        spawnpoint.probability *= mult;

                        //Clamp probability back down to 1
                        if (spawnpoint.probability > 1) {
                            spawnpoint.probability = 1;
                        }

                        this.logger.debug(`${locationId},   Pool:${spawnpoint.template.Id},    Chance:${spawnpoint.probability}`);
                    }
                });
            }
        }
    }
}
