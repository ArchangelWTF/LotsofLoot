import { DatabaseServer } from "@spt/servers/DatabaseServer";
import { ILotsofLootConfig } from "./ILotsofLootConfig";
import { LotsofLootLogger } from "./LotsofLootLogger";
import { ItemHelper } from "@spt/helpers/ItemHelper";

export class LotsofLootHelper
{
    constructor(private config: ILotsofLootConfig, private databaseServer: DatabaseServer, private itemHelper: ItemHelper, private logger: LotsofLootLogger)
    {

    }

    //This function is heavily based off of SVM's 'RemoveBackpacksRestrictions'
    //Huge credit to GhostFenixx for this
    public removeBackpackRestrictions() : void
    {
        const items = this.databaseServer.getTables().templates.items;

        for (let key in items)
        {
            let value = items[key];

            if(value._parent == "5448e53e4bdc2d60728b4567" && value._props.Grids[0]._props.filters !== undefined)
            {
                value._props.Grids[0]._props.filters = [];
            }
        }
    }
}