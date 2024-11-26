import { DependencyContainer, Lifecycle } from "tsyringe";

import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";
import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootController } from "../controllers/LotsofLootController";
import { LotsofLootMarkedRoomController } from "../controllers/LotsofLootMarkedRoomController";
import { LocationLootGeneratorOverrides } from "../overrides/LocationLootGeneratorOverrides";

import { LotsofLoot } from "../LotsofLoot";

export class Container {
    public static register(container: DependencyContainer): void {
        container.register<LotsofLootConfig>("LotsofLootConfig", LotsofLootConfig, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootLogger>("LotsofLootLogger", LotsofLootLogger, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootHelper>("LotsofLootHelper", LotsofLootHelper, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootController>("LotsofLootController", LotsofLootController, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootMarkedRoomController>("LotsofLootMarkedRoomController", LotsofLootMarkedRoomController, { lifecycle: Lifecycle.Singleton });

        container.register<LocationLootGeneratorOverrides>("LocationLootGeneratorOverrides", LocationLootGeneratorOverrides, { lifecycle: Lifecycle.Singleton });

        container.register<LotsofLoot>("LotsofLoot", LotsofLoot, { lifecycle: Lifecycle.Singleton });
    }
}