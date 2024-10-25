import { LogTextColor } from "@spt/models/spt/logging/LogTextColor";
import { ILogger } from "@spt/models/spt/utils/ILogger";

export class LotsofLootLogger {
    constructor(
        private logger: ILogger,
        private debugEnabled: boolean,
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
        if (this.debugEnabled) {
            this.logger.logWithColor(this.loggerPrefix + log, LogTextColor.YELLOW);
        }
    }
}
