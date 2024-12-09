import { DependencyContainer, Lifecycle } from "tsyringe";

import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";
import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootItemHelper } from "../helpers/LotsofLootItemHelper";
import { LotsofLootService } from "../services/LotsofLootService";
import { LotsofLootMarkedRoomService } from "../services/LotsfLootMarkedRoomService";
import { LocationLootGeneratorOverrides } from "../overrides/LocationLootGeneratorOverrides";

import { LotsofLoot } from "../LotsofLoot";

export class Container {
    public static register(container: DependencyContainer): void {
        container.register<LotsofLootConfig>("LotsofLootConfig", LotsofLootConfig, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootLogger>("LotsofLootLogger", LotsofLootLogger, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootHelper>("LotsofLootHelper", LotsofLootHelper, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootItemHelper>("LotsofLootItemHelper", LotsofLootItemHelper, { lifecycle: Lifecycle.Singleton });

        container.register<LotsofLootService>("LotsofLootService", LotsofLootService, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootMarkedRoomService>("LotsofLootMarkedRoomService", LotsofLootMarkedRoomService, { lifecycle: Lifecycle.Singleton });

        container.register<LocationLootGeneratorOverrides>("LocationLootGeneratorOverrides", LocationLootGeneratorOverrides, { lifecycle: Lifecycle.Singleton });

        container.register<LotsofLoot>("LotsofLoot", LotsofLoot, { lifecycle: Lifecycle.Singleton });
    }
}