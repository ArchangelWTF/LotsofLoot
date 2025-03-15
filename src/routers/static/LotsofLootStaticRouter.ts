import { RouteAction, StaticRouter } from "@spt/di/Router";
import { HttpResponseUtil } from "@spt/utils/HttpResponseUtil";
import { inject, injectable } from "tsyringe";
import { LotsofLootCallbacks } from "../../callbacks/LotsofLootCallbacks";

@injectable()
export class LotsofLootStaticRouter extends StaticRouter {
    constructor(
        @inject("HttpResponseUtil") protected httpResponseUtil: HttpResponseUtil,
        @inject("LotsofLootCallbacks") protected lotsofLootCallbacks: LotsofLootCallbacks,
    ) {
        super([
            new RouteAction("/lotsofloot/debug/generateloot", async (url: string, info: string, sessionID: string, _output: string): Promise<any> => {
                return this.httpResponseUtil.noBody(await this.lotsofLootCallbacks.handleGenerateLoot(url, info, sessionID));
            }),
        ]);
    }
}
