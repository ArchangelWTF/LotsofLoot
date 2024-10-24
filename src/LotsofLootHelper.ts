import { ItemHelper } from "@spt/helpers/ItemHelper";
import { ILocation } from "@spt/models/eft/common/ILocation";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { ILotsofLootConfig } from "./ILotsofLootConfig";
import { LotsofLootLogger } from "./LotsofLootLogger";

export class LotsofLootHelper {
    constructor(
        private config: ILotsofLootConfig,
        private databaseServer: DatabaseServer,
        private itemHelper: ItemHelper,
        private logger: LotsofLootLogger,
    ) {}

    //This function is heavily based off of SVM's 'RemoveBackpacksRestrictions'
    //Huge credit to GhostFenixx for this
    public removeBackpackRestrictions(): void {
        const items = this.databaseServer.getTables().templates.items;

        for (let key in items) {
            let value = items[key];

            if (value._parent == "5448e53e4bdc2d60728b4567" && value._props.Grids[0]._props.filters !== undefined) {
                value._props.Grids[0]._props.filters = [];
            }
        }
    }

    public changeRelativeProbabilityInPool(itemtpl: string, mult: number): void {
        const locations = this.databaseServer.getTables().locations;

        for (const locationId in locations) {
            if (locations.hasOwnProperty(locationId)) {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.logDebug(`Skipping ${locationId} as it has no loose loot!`);
                    continue;
                }

                location.looseLoot.spawnpoints.forEach((spawnpoint) => {
                    const item = spawnpoint.template.Items.find((i) => i._tpl == itemtpl);

                    if (item) {
                        const itemDistribution = spawnpoint.itemDistribution.find((i) => i.composedKey.key == item._id);

                        if (itemDistribution) {
                            itemDistribution.relativeProbability *= mult;

                            this.logger.logDebug(`${locationId}, ${spawnpoint.template.Id}, ${item._tpl}, ${itemDistribution.relativeProbability}`);
                        }
                    }
                });
            }
        }
    }

    public changeProbabilityOfPool(itemtpl: string, mult: number): void {
        const locations = this.databaseServer.getTables().locations;

        for (const locationId in locations) {
            if (locations.hasOwnProperty(locationId)) {
                const location: ILocation = locations[locationId];

                if (!location.looseLoot) {
                    this.logger.logDebug(`Skipping ${locationId} as it has no loose loot!`);
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

                        this.logger.logDebug(`${locationId},   Pool:${spawnpoint.template.Id},    Chance:${spawnpoint.probability}`);
                    }
                });
            }
        }
    }
}
