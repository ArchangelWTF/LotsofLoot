import { DependencyContainer, inject, injectable } from "tsyringe";

import { LocationLootGeneratorOverrides } from "./overrides/LocationLootGeneratorOverrides";
import { LotsofLootLogger } from "./utils/LotsofLootLogger";
import { LotsofLootController } from "./controllers/LotsofLootController";
import { LotsofLootMarkedRoomController } from "./controllers/LotsofLootMarkedRoomController";
import { LocationLootGenerator } from "@spt/generators/LocationLootGenerator";

@injectable()
export class LotsofLoot {
    constructor(
        @inject("LocationLootGeneratorOverrides") protected locationLootGeneratorOverrides: LocationLootGeneratorOverrides,
        @inject("LotsofLootController") protected lotsofLootController: LotsofLootController,
        @inject("LotsofLootMarkedRoomController") protected lotsofLootMarkedRoomController: LotsofLootMarkedRoomController,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {
    }

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
        await this.lotsofLootController.applyLotsOfLootModifications();
        await this.lotsofLootMarkedRoomController.adjustMarkedRoomItems();

        this.logger.logInfo(`Finished loading`);
    }
}