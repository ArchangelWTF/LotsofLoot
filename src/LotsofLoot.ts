import { DependencyContainer, inject, injectable } from "tsyringe";

import { LocationLootGenerator } from "@spt/generators/LocationLootGenerator";

import { LocationLootGeneratorOverrides } from "./overrides/LocationLootGeneratorOverrides";
import { LotsofLootMarkedRoomService } from "./services/LotsfLootMarkedRoomService";
import { LotsofLootService } from "./services/LotsofLootService";
import { LotsofLootLogger } from "./utils/LotsofLootLogger";

@injectable()
export class LotsofLoot {
    constructor(
        @inject("LocationLootGeneratorOverrides") protected locationLootGeneratorOverrides: LocationLootGeneratorOverrides,
        @inject("LotsofLootService") protected lotsofLootService: LotsofLootService,
        @inject("LotsofLootMarkedRoomService") protected lotsofLootMarkedRoomService: LotsofLootMarkedRoomService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {}

    public async preSptLoadAsync(container: DependencyContainer): Promise<void> {
        container.afterResolution(
            "LocationLootGenerator",
            (_t, result: LocationLootGenerator) => {
                //Temporary cast to get rid of protected error
                (result as any).createStaticLootItem = (tpl, staticAmmoDist, parentId) => {
                    return this.locationLootGeneratorOverrides.createStaticLootItem(tpl, staticAmmoDist, parentId);
                };

                result.generateDynamicLoot = (dynamicLootDist, staticAmmoDist, locationName) => {
                    return this.locationLootGeneratorOverrides.generateDynamicLoot(dynamicLootDist, staticAmmoDist, locationName);
                };
            },
            { frequency: "Always" },
        );
    }

    public async postDBLoadAsync(_container: DependencyContainer): Promise<void> {
        await this.lotsofLootService.applyLotsOfLootModifications();
        await this.lotsofLootMarkedRoomService.adjustMarkedRoomItems();

        this.logger.logInfo(`Finished loading`);
    }
}
