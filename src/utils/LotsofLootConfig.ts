import path from "node:path";
import { inject, injectable } from "tsyringe";

import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { JsonUtil } from "@spt/utils/JsonUtil";

import { FileSystemSync } from "@spt/utils/FileSystemSync";
import { ILotsofLootConfig } from "../models/ILotsofLootConfig";

@injectable()
export class LotsofLootConfig {
    private config: ILotsofLootConfig;

    constructor(
        @inject("PreSptModLoader") protected preSptModLoader: PreSptModLoader,
        @inject("FileSystemSync") protected fileSystemSync: FileSystemSync,
        @inject("JsonUtil") protected jsonUtil: JsonUtil,
    ) {
        this.config = this.jsonUtil.deserializeJson5(fileSystemSync.read(path.join(__dirname, "../../config/config.json5")));
    }

    public getConfig(): ILotsofLootConfig {
        return this.config;
    }
}
