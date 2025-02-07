import { inject, injectable } from "tsyringe";

import { ItemHelper } from "@spt/helpers/ItemHelper";
import { ISpawnpoint } from "@spt/models/eft/common/ILooseLoot";
import { DatabaseService } from "@spt/services/DatabaseService";
import { HashUtil } from "@spt/utils/HashUtil";

import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

@injectable()
export class LotsofLootMarkedRoomService {
    constructor(
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootHelper") protected lotsofLootHelper: LotsofLootHelper,
        @inject("ItemHelper") protected itemHelper: ItemHelper,
        @inject("HashUtil") protected hashUtil: HashUtil,
    ) {}

    public async adjustMarkedRoomItems(): Promise<void> {
        const spawnPointsCustoms = this.databaseService.getTables().locations.bigmap.looseLoot.spawnpoints;
        const spawnPointsReserve = this.databaseService.getTables().locations.rezervbase.looseLoot.spawnpoints;
        const spawnPointsStreets = this.databaseService.getTables().locations.tarkovstreets.looseLoot.spawnpoints;
        const spawnPointsLighthouse = this.databaseService.getTables().locations.lighthouse.looseLoot.spawnpoints;

        for (const spawnpoint of spawnPointsCustoms) {
            if (this.isMarkedRoomSpawnpoint(spawnpoint, "bigmap")) {
                this.logger.debug(`Marked room (Customs) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.customs;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsReserve) {
            if (this.isMarkedRoomSpawnpoint(spawnpoint, "rezervbase")) {
                this.logger.debug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.reserve;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsStreets) {
            if (this.isMarkedRoomSpawnpoint(spawnpoint, "tarkovstreets")) {
                this.logger.debug(`Marked room (Streets) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.streets;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsLighthouse) {
            if (this.isMarkedRoomSpawnpoint(spawnpoint, "lighthouse")) {
                this.logger.debug(`Marked room (Lighthouse) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.config.getConfig().markedRoom.multiplier.lighthouse;
                await this.markedAddExtraItemsAsync(spawnpoint);
                await this.adjustMarkedItemGroupsAsync(spawnpoint);
            }
        }
    }

    public isMarkedRoomSpawnpoint(spawnpoint: ISpawnpoint, locationId: string): boolean {
        if (locationId === "bigmap") {
            //Dorms 314 Marked Room
            if (spawnpoint.template.Position.x > 180 && spawnpoint.template.Position.x < 185 && spawnpoint.template.Position.z > 180 && spawnpoint.template.Position.z < 185 && spawnpoint.template.Position.y > 6 && spawnpoint.template.Position.y < 7) {
                return true;
            }
        }

        if (locationId === "rezervbase") {
            if (spawnpoint.template.Position.x > -125 && spawnpoint.template.Position.x < -120 && spawnpoint.template.Position.z > 25 && spawnpoint.template.Position.z < 30 && spawnpoint.template.Position.y > -15 && spawnpoint.template.Position.y < -14) {
                return true;
            } else if (spawnpoint.template.Position.x > -155 && spawnpoint.template.Position.x < -150 && spawnpoint.template.Position.z > 70 && spawnpoint.template.Position.z < 75 && spawnpoint.template.Position.y > -9 && spawnpoint.template.Position.y < -8) {
                return true;
            } else if (spawnpoint.template.Position.x > 190 && spawnpoint.template.Position.x < 195 && spawnpoint.template.Position.z > -230 && spawnpoint.template.Position.z < -225 && spawnpoint.template.Position.y > -6 && spawnpoint.template.Position.y < -5) {
                return true;
            }
        }

        if (locationId === "tarkovstreets") {
            //Abandoned Factory Marked Room
            if (spawnpoint.template.Position.x > -133 && spawnpoint.template.Position.x < -129 && spawnpoint.template.Position.z > 265 && spawnpoint.template.Position.z < 275 && spawnpoint.template.Position.y > 8.5 && spawnpoint.template.Position.y < 11) {
                return true;
            }
            //Chek 13 Marked Room
            else if (spawnpoint.template.Position.x > 186 && spawnpoint.template.Position.x < 191 && spawnpoint.template.Position.z > 224 && spawnpoint.template.Position.z < 229 && spawnpoint.template.Position.y > -0.5 && spawnpoint.template.Position.y < 1.5) {
                return true;
            }
        }

        if (locationId === "lighthouse") {
            //Lightkeeper marked room
            if (spawnpoint.template.Position.x > 319 && spawnpoint.template.Position.x < 330 && spawnpoint.template.Position.z > 482 && spawnpoint.template.Position.z < 489 && spawnpoint.template.Position.y > 5 && spawnpoint.template.Position.y < 6.5) {
                return true;
            }
        }

        return false;
    }

    private async markedAddExtraItemsAsync(spawnpoint: ISpawnpoint): Promise<void> {
        const extraItems = Object.entries(this.config.getConfig().markedRoom.extraItems);

        await Promise.all(
            extraItems.map(async ([itemTpl, relativeProbability]) => {
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
            }),
        );
    }

    private async adjustMarkedItemGroupsAsync(spawnpoint: ISpawnpoint): Promise<void> {
        const itemGroups = this.config.getConfig().markedRoom.itemGroups;

        await Promise.all(
            spawnpoint.template.Items.map(async (item) => {
                await Promise.all(
                    Object.keys(itemGroups).map(async (group) => {
                        if (this.itemHelper.isOfBaseclass(item._tpl, group)) {
                            await Promise.all(
                                spawnpoint.itemDistribution.map(async (dist) => {
                                    if (dist.composedKey.key === item._id) {
                                        dist.relativeProbability *= itemGroups[group];

                                        this.logger.debug(`markedItemGroups: Changed ${item._tpl} to ${dist.relativeProbability}`);
                                    }
                                }),
                            );
                        }
                    }),
                );
            }),
        );
    }
}
