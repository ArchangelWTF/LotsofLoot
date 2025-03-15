import { DependencyContainer, Lifecycle } from "tsyringe";

import { LotsofLoot } from "../LotsofLoot";
import { LotsofLootCallbacks } from "../callbacks/LotsofLootCallbacks";
import { LotsofLootLocationLootGenerator } from "../generators/LotsofLootLocationLootGenerator";
import { LotsofLootHelper } from "../helpers/LotsofLootHelper";
import { LotsofLootItemHelper } from "../helpers/LotsofLootItemHelper";
import { LocationLootGeneratorOverrides } from "../overrides/LocationLootGeneratorOverrides";
import { LotsofLootStaticRouter } from "../routers/static/LotsofLootStaticRouter";
import { LotsofLootMarkedRoomService } from "../services/LotsfLootMarkedRoomService";
import { LotsofLootService } from "../services/LotsofLootService";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";
import { LotsofLootRandomUtil } from "../utils/LotsofLootRandomUtil";

export class Container {
    public static register(container: DependencyContainer): void {
        container.register<LotsofLootConfig>("LotsofLootConfig", LotsofLootConfig, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootLogger>("LotsofLootLogger", LotsofLootLogger, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootRandomUtil>("LotsofLootRandomUtil", LotsofLootRandomUtil);
        container.register<LotsofLootHelper>("LotsofLootHelper", LotsofLootHelper);
        container.register<LotsofLootItemHelper>("LotsofLootItemHelper", LotsofLootItemHelper);
        container.register<LotsofLootLocationLootGenerator>("LotsofLootLocationLootGenerator", LotsofLootLocationLootGenerator, { lifecycle: Lifecycle.Singleton });

        container.register<LotsofLootService>("LotsofLootService", LotsofLootService, { lifecycle: Lifecycle.Singleton });
        container.register<LotsofLootMarkedRoomService>("LotsofLootMarkedRoomService", LotsofLootMarkedRoomService, { lifecycle: Lifecycle.Singleton });

        container.register<LocationLootGeneratorOverrides>("LocationLootGeneratorOverrides", LocationLootGeneratorOverrides, { lifecycle: Lifecycle.Singleton });

        container.register<LotsofLootStaticRouter>("LotsofLootStaticRouter", LotsofLootStaticRouter);
        container.register<LotsofLootCallbacks>("LotsofLootCallbacks", LotsofLootCallbacks);

        Container.registerTypes(container);

        container.register<LotsofLoot>("LotsofLoot", LotsofLoot, { lifecycle: Lifecycle.Singleton });
    }

    public static registerTypes(container: DependencyContainer) {
        container.registerType("StaticRoutes", "LotsofLootStaticRouter");
    }
}
