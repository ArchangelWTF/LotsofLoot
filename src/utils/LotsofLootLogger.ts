import { inject, injectable } from "tsyringe";

import { ILogger } from "@spt/models/spt/utils/ILogger";
import { DatabaseService } from "@spt/services/DatabaseService";
import { LogTextColor } from "@spt/models/spt/logging/LogTextColor";
import { LotsofLootConfig } from "./LotsofLootConfig";

@injectable()
export class LotsofLootLogger {
    constructor(
        @inject("WinstonLogger") protected logger: ILogger,
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
    ) {}
    private loggerPrefix = "[Lots of Loot] ";

    public logInfo(log: string): void {
        this.logger.info(this.loggerPrefix + log);
    }

    public warning(log: string): void {
        this.logWarning(log);
    }

    protected logWarning(log: string): void {
        this.logger.warning(this.loggerPrefix + log);
    }

    public logError(log: string): void {
        this.logger.error(this.loggerPrefix + log);
    }

    public debug(log: string): void {
        this.logDebug(log);
    }

    protected logDebug(log: string): void {
        if (this.config.getConfig().general.debug) {
            this.logger.logWithColor(this.loggerPrefix + log, LogTextColor.YELLOW);
        }
    }

    public writeItemName(itemId: string, writeTpl: boolean = false): string {
        const enLocale = this.databaseService.getLocales().global["en"];
        const itemName = enLocale[`${itemId} Name`] ?? "Unknown";

        if (writeTpl) {
            return `${itemName}(${itemId})`;
        } else {
            return itemName;
        }
    }
}
