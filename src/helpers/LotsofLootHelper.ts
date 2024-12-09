import { inject, injectable } from "tsyringe";

import { ILocation } from "@spt/models/eft/common/ILocation";
import { DatabaseService } from "@spt/services/DatabaseService";

import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

@injectable()
export class LotsofLootHelper {
    constructor(
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
    ) {}

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

    public async changeRelativeProbabilityInPoolAsync(itemtpl: string, mult: number): Promise<void> {
        const locations = this.databaseService.getTables().locations;
        const locationIds = Object.keys(locations);

        //Process each location asynchronously
        await Promise.all(
            locationIds.map(async (locationId) => {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.debug(`Skipping ${locationId} as it has no loose loot!`);
                    return; //Return skips this location, keeps the loop going.
                }

                //Process spawnpoints asynchronously
                await Promise.all(
                    location.looseLoot.spawnpoints.map(async (spawnpoint) => {
                        const item = spawnpoint.template.Items.find((i) => i._tpl == itemtpl);

                        if (item) {
                            const itemDistribution = spawnpoint.itemDistribution.find((i) => i.composedKey.key == item._id);

                            if (itemDistribution) {
                                itemDistribution.relativeProbability *= mult;

                                this.logger.debug(`${locationId}, ${spawnpoint.template.Id}, ${item._tpl}, ${itemDistribution.relativeProbability}`);
                            }
                        }
                    }),
                );
            }),
        );
    }

    public async changeProbabilityOfPoolAsync(itemtpl: string, mult: number): Promise<void> {
        const locations = this.databaseService.getTables().locations;
        const locationIds = Object.keys(locations);

        //Process each location asynchronously
        await Promise.all(
            locationIds.map(async (locationId) => {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.debug(`Skipping ${locationId} as it has no loose loot!`);
                    return; //Return skips this location, keeps the loop going.
                }

                //Process spawnpoints asynchronously
                await Promise.all(
                    location.looseLoot.spawnpoints.map(async (spawnpoint) => {
                        const item = spawnpoint.template.Items.find((i) => i._tpl == itemtpl);

                        if (item) {
                            spawnpoint.probability *= mult;

                            //Clamp probability to 1 if it exceeds
                            if (spawnpoint.probability > 1) {
                                spawnpoint.probability = 1;
                            }

                            this.logger.debug(`${locationId}, Pool:${spawnpoint.template.Id}, Chance:${spawnpoint.probability}`);
                        }
                    }),
                );
            }),
        );
    }
}
