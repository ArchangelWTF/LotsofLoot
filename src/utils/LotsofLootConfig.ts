import path from "node:path";
import { inject, injectable } from "tsyringe";

import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { JsonUtil } from "@spt/utils/JsonUtil";
import { VFS } from "@spt/utils/VFS";

import { ILotsofLootConfig } from "../models/ILotsofLootConfig";

@injectable()
export class LotsofLootConfig {
    private config: ILotsofLootConfig;

    constructor(
        @inject("PreSptModLoader") protected preSptModLoader: PreSptModLoader,
        @inject("VFS") protected vfs: VFS,
        @inject("JsonUtil") protected jsonUtil: JsonUtil,
    ) {
        this.config = this.jsonUtil.deserializeJson5(vfs.readFile(path.join(__dirname, "../../config/config.json5")));
    }

    public getConfig(): ILotsofLootConfig {
        return this.config;
    }
}
