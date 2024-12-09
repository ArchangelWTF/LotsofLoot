import { inject, injectable } from "tsyringe";

import { ItemHelper } from "@spt/helpers/ItemHelper";
import { ILocation } from "@spt/models/eft/common/ILocation";
import { ISpawnpoint } from "@spt/models/eft/common/ILooseLoot";
import { BaseClasses } from "@spt/models/enums/BaseClasses";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { DatabaseService } from "@spt/services/DatabaseService";
import { HashUtil } from "@spt/utils/HashUtil";
import { ICloner } from "@spt/utils/cloners/ICloner";

import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

@injectable()
export class LotsofLootService {
    constructor(
        @inject("ConfigServer") protected configServer: ConfigServer,
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootHelper") protected lotsofLootHelper: LotsofLootHelper,
        @inject("ItemHelper") protected itemHelper: ItemHelper,
        @inject("HashUtil") protected hashUtil: HashUtil,
        @inject("PrimaryCloner") protected cloner: ICloner,
    ) {}

    public async applyLotsOfLootModifications(): Promise<void> {
        const lotsofLootConfig = this.config.getConfig();
        const tables = this.databaseService.getTables();
        const locations = tables.locations;
        const LocationConfig = this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION);

        this.addToRustedKeyRoom();

        if (this.config.getConfig().general.removeBackpackRestrictions) {
            this.lotsofLootHelper.removeBackpackRestrictions();
        }

        for (const map in lotsofLootConfig.looseLootMultiplier) {
            LocationConfig.looseLootMultiplier[map] = lotsofLootConfig.looseLootMultiplier[map];
            this.logger.debug(`${map}: ${LocationConfig.looseLootMultiplier[map]}`);
            LocationConfig.staticLootMultiplier[map] = lotsofLootConfig.staticLootMultiplier[map];
            this.logger.debug(`${map}: ${LocationConfig.staticLootMultiplier[map]}`);
        }

        for (const locationId in locations) {
            if (locations.hasOwnProperty(locationId)) {
                const location: ILocation = locations[locationId];
                //Location does not have any static loot pools, skip this map.
                if (!location.staticLoot) {
                    this.logger.debug(`Skipping ${locationId} as it has no static loot`);

                    continue;
                }

                const staticLoot = location.staticLoot;

                for (const container in staticLoot) {
                    for (const possItemCount in staticLoot[container].itemcountDistribution) {
                        if (staticLoot[container].itemcountDistribution[possItemCount].count == 0) {
                            staticLoot[container].itemcountDistribution[possItemCount].relativeProbability = Math.round(staticLoot[container].itemcountDistribution[possItemCount].relativeProbability * lotsofLootConfig.containers[container]);

                            this.logger.debug(`Changed container ${container} chance to ${staticLoot[container].itemcountDistribution[possItemCount].relativeProbability}`);
                        }
                    }
                }
            }
        }

        for (const itemId in lotsofLootConfig.changeRelativeProbabilityInPool) {
            await this.lotsofLootHelper.changeRelativeProbabilityInPoolAsync(itemId, lotsofLootConfig.changeRelativeProbabilityInPool[itemId]);
        }

        for (const itemId in lotsofLootConfig.changeProbabilityOfPool) {
            await this.lotsofLootHelper.changeProbabilityOfPoolAsync(itemId, lotsofLootConfig.changeProbabilityOfPool[itemId]);
        }

        if (lotsofLootConfig.general.disableFleaRestrictions) {
            for (const item in tables.templates.items) {
                if (this.itemHelper.isValidItem(tables.templates.items[item]._id)) {
                    tables.templates.items[item]._props.CanRequireOnRagfair = true;
                    tables.templates.items[item]._props.CanSellOnRagfair = true;
                }
            }
        }

        for (const id in lotsofLootConfig.general.priceCorrection) {
            tables.templates.prices[id] = lotsofLootConfig.general.priceCorrection[id];
        }
    }

    private addToRustedKeyRoom(): void {
        const streetsloot = this.databaseService.getTables().locations.tarkovstreets.looseLoot;
        const items = this.databaseService.getTables().templates.items;
        let keys: string[] = [];
        let valuables: string[] = [];

        for (const item in items) {
            if (this.config.getConfig().general.rustedKeyRoomIncludesKeycards) {
                if (this.itemHelper.isOfBaseclass(item, BaseClasses.KEY)) {
                    keys.push(item);
                }
            } else {
                if (this.itemHelper.isOfBaseclass(item, BaseClasses.KEY_MECHANICAL)) {
                    keys.push(item);
                }
            }
            if (this.itemHelper.isOfBaseclass(item, BaseClasses.JEWELRY)) {
                valuables.push(item);
            }
        }
        let point: ISpawnpoint = {
            locationId: "(185.087, 6.554, 63.721)",
            probability: 0.25,
            template: {
                Id: "Keys1",
                IsContainer: false,
                useGravity: true,
                randomRotation: true,
                Position: {
                    x: 185.087,
                    y: 6.554,
                    z: 63.721,
                },
                Rotation: {
                    x: 0,
                    y: 0,
                    z: 0,
                },
                IsGroupPosition: false,
                GroupPositions: [],
                IsAlwaysSpawn: false,
                Root: this.hashUtil.generate(),
                Items: [],
            },
            itemDistribution: [],
        };
        for (let i = 0; i < keys.length; i++) {
            point.template.Items.push({
                _id: i.toString(),
                _tpl: keys[i],
            });
            point.itemDistribution.push({
                composedKey: { key: i.toString() },
                relativeProbability: 1,
            });
        }
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys2";
        point.template.Position = {
            x: 185.125,
            y: 6.554,
            z: 63.186,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys3";
        point.template.Position = {
            x: 185.164,
            y: 6.554,
            z: 62.241,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys4";
        point.template.Position = {
            x: 185.154,
            y: 6.554,
            z: 62.686,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys5";
        point.template.Position = {
            x: 185.21,
            y: 6.935,
            z: 60.86,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys6";
        point.template.Position = {
            x: 185.205,
            y: 6.935,
            z: 60.56,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys7";
        point.template.Position = {
            x: 185.208,
            y: 6.58,
            z: 60.857,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys8";
        point.template.Position = {
            x: 185.211,
            y: 6.562,
            z: 60.562,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys9";
        point.template.Position = {
            x: 185.202,
            y: 6.175,
            z: 60.551,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys10";
        point.template.Position = {
            x: 185.2,
            y: 6.234,
            z: 60.872,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys11";
        point.template.Position = {
            x: 182.683,
            y: 6.721,
            z: 57.813,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Keys12";
        point.template.Position = {
            x: 182.683,
            y: 6.721,
            z: 60.073,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Val1";
        point.template.Position = {
            x: 185.037,
            y: 5.831,
            z: 53.836,
        };
        point.template.Items = [];
        point.itemDistribution = [];
        for (let i = 0; i < valuables.length; i++) {
            point.template.Items.push({
                _id: i.toString(),
                _tpl: valuables[i],
            });
            point.itemDistribution.push({
                composedKey: { key: i.toString() },
                relativeProbability: 1,
            });
        }
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Val2";
        point.template.Position = {
            x: 183.064,
            y: 5.831,
            z: 53.767,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Val3";
        point.template.Position = {
            x: 185.146,
            y: 5.831,
            z: 60.114,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));

        point.template.Root = this.hashUtil.generate();
        point.template.Id = "Val4";
        point.template.Position = {
            x: 185.085,
            y: 5.831,
            z: 65.393,
        };
        point.locationId = point.template.Position.x.toString() + point.template.Position.y.toString() + point.template.Position.z.toString();
        streetsloot.spawnpoints.push(this.cloner.clone(point));
    }
}
