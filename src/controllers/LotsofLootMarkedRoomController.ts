import { inject, injectable } from "tsyringe";

import { DatabaseService } from "@spt/services/DatabaseService";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { ISpawnpoint } from "@spt/models/eft/common/ILooseLoot";
import { HashUtil } from "@spt/utils/HashUtil";

import { LotsofLootLogger } from "../utils/LotsofLootLogger";
import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";


@injectable()
export class LotsofLootMarkedRoomController {
    constructor(
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootHelper") protected lotsofLootHelper: LotsofLootHelper,
        @inject("ItemHelper") protected itemHelper: ItemHelper,
        @inject("HashUtil") protected hashUtil: HashUtil,
    ) {
    }

    public async adjustMarkedRoomItems(): Promise<void> {
        const spawnPointsCustoms = this.databaseService.getTables().locations.bigmap.looseLoot.spawnpoints;
        const spawnPointsReserve = this.databaseService.getTables().locations.rezervbase.looseLoot.spawnpoints;
        const spawnPointsStreets = this.databaseService.getTables().locations.tarkovstreets.looseLoot.spawnpoints;
        const spawnPointsLighthouse = this.databaseService.getTables().locations.lighthouse.looseLoot.spawnpoints;

        for (const spawnpoint of spawnPointsCustoms) {
            //Dorms 314 Marked Room
            if (spawnpoint.template.Position.x > 180 && spawnpoint.template.Position.x < 185 && spawnpoint.template.Position.z > 180 && spawnpoint.template.Position.z < 185 && spawnpoint.template.Position.y > 6 && spawnpoint.template.Position.y < 7) {
                this.logger.debug(`Marked room (Customs) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.customs;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsReserve) {
            if (spawnpoint.template.Position.x > -125 && spawnpoint.template.Position.x < -120 && spawnpoint.template.Position.z > 25 && spawnpoint.template.Position.z < 30 && spawnpoint.template.Position.y > -15 && spawnpoint.template.Position.y < -14) {
                this.logger.debug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.reserve;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            } else if (spawnpoint.template.Position.x > -155 && spawnpoint.template.Position.x < -150 && spawnpoint.template.Position.z > 70 && spawnpoint.template.Position.z < 75 && spawnpoint.template.Position.y > -9 && spawnpoint.template.Position.y < -8) {
                this.logger.debug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.reserve;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            } else if (spawnpoint.template.Position.x > 190 && spawnpoint.template.Position.x < 195 && spawnpoint.template.Position.z > -230 && spawnpoint.template.Position.z < -225 && spawnpoint.template.Position.y > -6 && spawnpoint.template.Position.y < -5) {
                this.logger.debug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.reserve;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsStreets) {
            //Abandoned Factory Marked Room
            if (spawnpoint.template.Position.x > -133 && spawnpoint.template.Position.x < -129 && spawnpoint.template.Position.z > 265 && spawnpoint.template.Position.z < 275 && spawnpoint.template.Position.y > 8.5 && spawnpoint.template.Position.y < 11) {
                this.logger.debug(`Marked room (Streets) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.streets;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
            //Chek 13 Marked Room
            else if (spawnpoint.template.Position.x > 186 && spawnpoint.template.Position.x < 191 && spawnpoint.template.Position.z > 224 && spawnpoint.template.Position.z < 229 && spawnpoint.template.Position.y > -0.5 && spawnpoint.template.Position.y < 1.5) {
                this.logger.debug(`Marked room (Streets) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.streets;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsLighthouse) {
            //Lightkeeper marked room
            if (spawnpoint.template.Position.x > 319 && spawnpoint.template.Position.x < 330 && spawnpoint.template.Position.z > 482 && spawnpoint.template.Position.z < 489 && spawnpoint.template.Position.y > 5 && spawnpoint.template.Position.y < 6.5) {
                this.logger.debug(`Marked room (Lighthouse) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.lighthouse;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }
    }

    private async markedAddExtraItemsAsync(spawnpoint: ISpawnpoint): Promise<void> {
        const extraItems = Object.entries(await this.config.getConfig().markedRoom.extraItems);

        await Promise.all(extraItems.map(async ([itemTpl, relativeProbability]) => {
            if (spawnpoint.template.Items.find((x) => x._tpl === itemTpl)) {
                return; // Skip if the item already exists
            }

            const key = this.hashUtil.generate();

            spawnpoint.template.Items.push({
                _id: key,
                _tpl: itemTpl,
            });

            spawnpoint.itemDistribution.push({
                composedKey: { key: key },
                relativeProbability: relativeProbability,
            });

            this.logger.debug(`Added ${itemTpl} to ${spawnpoint.template.Id}`);
        }));
    }
    

    private async adjustMarkedItemGroupsAsync(spawnpoint: ISpawnpoint): Promise<void> {
        const itemGroups = this.config.getConfig().markedRoom.itemGroups;
    
        await Promise.all(spawnpoint.template.Items.map(async (item) => {
            await Promise.all(Object.keys(itemGroups).map(async (group) => {
                if (this.itemHelper.isOfBaseclass(item._tpl, group)) {
                    await Promise.all(spawnpoint.itemDistribution.map(async (dist) => {
                        if (dist.composedKey.key === item._id) {
                            dist.relativeProbability *= itemGroups[group];
                            
                            this.logger.debug(`markedItemGroups: Changed ${item._tpl} to ${dist.relativeProbability}`);
                        }
                    }));
                }
            }));
        }));
    }
}
