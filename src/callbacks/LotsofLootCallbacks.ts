import { ILocationBase } from "@spt/models/eft/common/ILocationBase";
import { ISpawnpointTemplate } from "@spt/models/eft/common/ILooseLoot";
import { LocationLifecycleService } from "@spt/services/LocationLifecycleService";
import { inject, injectable } from "tsyringe";
import { LocationLootGeneratorOverrides } from "../overrides/LocationLootGeneratorOverrides";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

@injectable()
export class LotsofLootCallbacks {
    constructor(
        @inject("LocationLifecycleService") protected locationLifecycleService: LocationLifecycleService,
        @inject("LocationLootGeneratorOverrides") protected locationLootGeneratorOverrides: LocationLootGeneratorOverrides,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {}

    public async handleGenerateLoot(_url: string, info: any, _sessionID: string): Promise<ISpawnpointTemplate[]> {
        const generatedLocation = (this.locationLifecycleService as any).generateLocationAndLoot(info.location, true) as ILocationBase;

        const generatedItems: Map<string, number> = new Map();

        for (const loot of generatedLocation.Loot) {
            for (const item of loot.Items) {
                const alreadyHasItems = generatedItems.get(item._tpl);

                if (alreadyHasItems) {
                    generatedItems.set(item._tpl, alreadyHasItems + 1);
                } else {
                    generatedItems.set(item._tpl, 1);
                }
            }
        }

        for (const item of generatedItems) {
            this.logger.info(`Generated ${item[1]} ${this.logger.writeItemName(item[0])}`);
        }

        return generatedLocation.Loot;
    }
}
