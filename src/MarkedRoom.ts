import { ItemHelper } from "@spt/helpers/ItemHelper";
import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { Spawnpoint } from "@spt/models/eft/common/ILooseLoot";
import { HashUtil } from "@spt/utils/HashUtil";
import { LotsofLootLogger } from "./LotsofLootLogger";
import { MarkedRoomConfig } from "./ILotsofLootConfig";

export class MarkedRoom 
{
    constructor(private markedRoomConfig: MarkedRoomConfig, private databaseServer: DatabaseServer, private itemHelper: ItemHelper, private hashUtil: HashUtil, private logger: LotsofLootLogger)
    {

    }

    public doMarkedRoomChanges() : void
    {
        const spawnPointscustoms = this.databaseServer.getTables().locations.bigmap.looseLoot.spawnpoints;
        const spawnPointsreserve = this.databaseServer.getTables().locations.rezervbase.looseLoot.spawnpoints;
        const spawnPointsstreets = this.databaseServer.getTables().locations.tarkovstreets.looseLoot.spawnpoints;
        const spawnPointsLighthouse = this.databaseServer.getTables().locations.lighthouse.looseLoot.spawnpoints;
        
        for (const spawnpoint of spawnPointscustoms)
        {
            //Dorms 314 Marked Room
            if ((spawnpoint.template.Position.x > 180) && (spawnpoint.template.Position.x < 185) && (spawnpoint.template.Position.z > 180) && (spawnpoint.template.Position.z < 185) && (spawnpoint.template.Position.y > 6) && (spawnpoint.template.Position.y < 7))
            {
                this.logger.logDebug(`Marked room (Customs) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.customs;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsreserve)
        {
            if ((spawnpoint.template.Position.x > -125) && (spawnpoint.template.Position.x < -120) && (spawnpoint.template.Position.z > 25) && (spawnpoint.template.Position.z < 30) && (spawnpoint.template.Position.y > -15) && (spawnpoint.template.Position.y < -14))
            {
                this.logger.logDebug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.reserve;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
                
            }
            else if ((spawnpoint.template.Position.x > -155) && (spawnpoint.template.Position.x < -150) && (spawnpoint.template.Position.z > 70) && (spawnpoint.template.Position.z < 75) && (spawnpoint.template.Position.y > -9) && (spawnpoint.template.Position.y < -8))
            {
                this.logger.logDebug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.reserve;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
            else if ((spawnpoint.template.Position.x > 190) && (spawnpoint.template.Position.x < 195) && (spawnpoint.template.Position.z > -230) && (spawnpoint.template.Position.z < -225) && (spawnpoint.template.Position.y > -6) && (spawnpoint.template.Position.y < -5))
            {
                this.logger.logDebug(`Marked room (Reserve) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.reserve;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

        for (const spawnpoint of spawnPointsstreets)
        {
            //Abandoned Factory Marked Room
            if ((spawnpoint.template.Position.x > -133) && (spawnpoint.template.Position.x < -129) && (spawnpoint.template.Position.z > 265) && (spawnpoint.template.Position.z < 275) && (spawnpoint.template.Position.y > 8.5) && (spawnpoint.template.Position.y < 11))
            {
                this.logger.logDebug(`Marked room (Streets) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.streets;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
            //Chek 13 Marked Room
            else if ((spawnpoint.template.Position.x > 186) && (spawnpoint.template.Position.x < 191) && (spawnpoint.template.Position.z > 224) && (spawnpoint.template.Position.z < 229) && (spawnpoint.template.Position.y > -0.5) && (spawnpoint.template.Position.y < 1.5))
            {
                this.logger.logDebug(`Marked room (Streets) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.streets;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }
        
        for (const spawnpoint of spawnPointsLighthouse)
        {
            if ((spawnpoint.template.Position.x > 319) && (spawnpoint.template.Position.x < 330) && (spawnpoint.template.Position.z > 482) && (spawnpoint.template.Position.z < 489) && (spawnpoint.template.Position.y > 5) && (spawnpoint.template.Position.y < 6.5))
            {
                this.logger.logDebug(`Marked room (Lighthouse) ${spawnpoint.template.Id}`);
                spawnpoint.probability *= this.markedRoomConfig.multiplier.lighthouse;
                this.markedAddExtraItems(spawnpoint);
                this.markedItemGroups(spawnpoint);
            }
        }

    }

    private markedAddExtraItems(spawnpoint: Spawnpoint) : void
    {
        for (const item of Object.entries(this.markedRoomConfig.extraItems))
        {
            if (spawnpoint.template.Items.find(x => x._tpl === item[0]))
            {
                continue;
            }

            const id = Math.random()*10000;
            
            spawnpoint.template.Items.push({
                "_id": id.toString(),
                "_tpl": item[0]
            })

            spawnpoint.itemDistribution.push({
                "composedKey":{"key":id.toString()},
                "relativeProbability": item[1]
            });

            this.logger.logDebug(`Added ${item[0]} to ${spawnpoint.template.Id}`);
        }
    }

    private markedItemGroups(spawnpoint: Spawnpoint) : void
    {
        for (const item of spawnpoint.template.Items)
        {
            for (const group in this.markedRoomConfig.itemGroups)
            {
                if (this.itemHelper.isOfBaseclass(item._tpl, group))
                {
                    for (const dist of spawnpoint.itemDistribution)
                    {
                        if (dist.composedKey.key == item._id)
                        {
                            dist.relativeProbability *= this.markedRoomConfig.itemGroups[group];
                            this.logger.logDebug(`markedItemGroups: Changed ${item._tpl} to ${dist.relativeProbability}`);
                        }
                    }
                }
            }
        }
    }
}