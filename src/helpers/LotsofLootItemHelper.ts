import { inject, injectable } from "tsyringe";

import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { DatabaseService } from "@spt/services/DatabaseService";

import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

@injectable()
export class LotsofLootItemHelper {
    constructor(
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
    ) {}

    public findAndReturnChildrenItemIdsByItems(items: Record<string, ITemplateItem>, itemID: string): string[] {
        const stack: string[] = [itemID];
        const result: string[] = [];
        const processedItems: Set<string> = new Set();

        //'Item (54009119af1c881c07000029)' Doesn't have a parent, return all of it's children instead.
        if (itemID === "54009119af1c881c07000029") {
            return Object.keys(items);
        }

        const parentToChildrenMap = this.buildParentToChildrenMap(items);

        // Main loop to find all children
        while (stack.length > 0) {
            const currentItemId = stack.pop();

            if (processedItems.has(currentItemId)) {
                continue;
            }

            processedItems.add(currentItemId);

            // If the current item has children, add them to the stack
            if (parentToChildrenMap[currentItemId]) {
                stack.push(...parentToChildrenMap[currentItemId]);
            }

            // If no children were found for the current item, add it to the result
            if (!parentToChildrenMap[currentItemId] || parentToChildrenMap[currentItemId].length === 0) {
                result.push(currentItemId);
            }
        }

        return result;
    }

    private buildParentToChildrenMap(items: Record<string, ITemplateItem>): Record<string, string[]> {
        const parentToChildrenMap: Record<string, string[]> = {};

        for (const itemId of Object.keys(items)) {
            const parentId = items[itemId]._parent;

            if (parentId) {
                if (!parentToChildrenMap[parentId]) {
                    parentToChildrenMap[parentId] = [];
                }
                parentToChildrenMap[parentId].push(itemId);
            }
        }

        return parentToChildrenMap;
    }
}
