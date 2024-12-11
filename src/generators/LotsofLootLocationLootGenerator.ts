import { inject, injectable } from "tsyringe";

import { BaseClasses } from "@spt/models/enums/BaseClasses";
import { ConfigTypes } from "@spt/models/enums/ConfigTypes";

import { IContainerItem, LocationLootGenerator } from "@spt/generators/LocationLootGenerator";
import { ItemHelper } from "@spt/helpers/ItemHelper";
import { PresetHelper } from "@spt/helpers/PresetHelper";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { DatabaseService } from "@spt/services/DatabaseService";
import { HashUtil } from "@spt/utils/HashUtil";
import { MathUtil } from "@spt/utils/MathUtil";
import { ProbabilityObject, ProbabilityObjectArray, RandomUtil } from "@spt/utils/RandomUtil";
import { ICloner } from "@spt/utils/cloners/ICloner";
import { LotsofLootItemHelper } from "../helpers/LotsofLootItemHelper";
import { LotsofLootConfig } from "../utils/LotsofLootConfig";
import { LotsofLootLogger } from "../utils/LotsofLootLogger";

import { IStaticAmmoDetails } from "@spt/models/eft/common/ILocation";
import { IItem } from "@spt/models/eft/common/tables/IItem";
import { ITemplateItem } from "@spt/models/eft/common/tables/ITemplateItem";
import { ILocationConfig } from "@spt/models/spt/config/ILocationConfig";
import { ILootInlooseContainerLimitConfig } from "../models/ILotsofLootConfig";

@injectable()
export class LotsofLootLocationLootGenerator {
    private looseContainerItemFilterIndexCache: Record<string, string[]> = {};

    constructor(
        @inject("LocationLootGenerator") protected locationLootGenerator: LocationLootGenerator,
        @inject("ConfigServer") protected configServer: ConfigServer,
        @inject("DatabaseService") protected databaseService: DatabaseService,
        @inject("ItemHelper") protected itemHelper: ItemHelper,
        @inject("PresetHelper") protected presetHelper: PresetHelper,
        @inject("PrimaryCloner") protected cloner: ICloner,
        @inject("HashUtil") protected hashUtil: HashUtil,
        @inject("RandomUtil") protected randomUtil: RandomUtil,
        @inject("MathUtil") protected mathUtil: MathUtil,
        @inject("LotsofLootItemHelper") protected lotsOfLootItemHelper: LotsofLootItemHelper,
        @inject("LotsofLootConfig") protected config: LotsofLootConfig,
        @inject("LotsofLootLogger") protected logger: LotsofLootLogger,
    ) {}

    public createStaticLootItem(tpl: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, parentId: string = undefined): IContainerItem {
        const itemTemplate = this.getItemTemplate(tpl);
        if (!itemTemplate) {
            return {
                items: [],
                width: 0,
                height: 0,
            };
        }

        let width = itemTemplate._props.Width;
        let height = itemTemplate._props.Height;
        let items: IItem[] = [
            {
                _id: this.hashUtil.generate(),
                _tpl: tpl,
                parentId: parentId,
            },
        ];

        // Handle different item types
        if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.WEAPON)) {
            items = this.handleWeaponItem(items, tpl, staticAmmoDist);
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MONEY) || this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO)) {
            const stackCount = this.randomUtil.getInt(itemTemplate._props.StackMinRandom, itemTemplate._props.StackMaxRandom);
            items[0].upd = { StackObjectsCount: stackCount };
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.AMMO_BOX)) {
            this.itemHelper.addCartridgesToAmmoBox(items, itemTemplate);
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.MAGAZINE)) {
            this.handleMagazineItem(items, itemTemplate, staticAmmoDist);
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.SIMPLE_CONTAINER) && tpl !== "5c093e3486f77430cb02e593") {
            this.handleContainerOrBackpackItem(items, staticAmmoDist, this.config.getConfig().lootinLooseContainer.lootInContainerModifier);
        } else if (this.itemHelper.isOfBaseclass(tpl, BaseClasses.BACKPACK)) {
            this.handleContainerOrBackpackItem(items, staticAmmoDist, this.config.getConfig().lootinLooseContainer.lootInBackpackModifier);
        } else if (this.itemHelper.armorItemCanHoldMods(tpl)) {
            const defaultPreset = this.presetHelper.getDefaultPreset(tpl);
            if (defaultPreset) {
                const presetAndMods: IItem[] = this.itemHelper.replaceIDs(defaultPreset._items);
                this.itemHelper.remapRootItemId(presetAndMods);
                presetAndMods[0].parentId = items[0].parentId;
                items = presetAndMods;
            } else if (itemTemplate._props.Slots?.length > 0) {
                items = this.itemHelper.addChildSlotItems(items, itemTemplate, this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION).equipmentLootSettings.modSpawnChancePercent);
            }
        }

        return {
            items: items,
            width: width,
            height: height,
        };
    }

    private getItemTemplate(tpl: string): ITemplateItem | null {
        const gotItem = this.itemHelper.getItem(tpl);
        return gotItem[0] ? gotItem[1] : null;
    }

    // Cobbled together mostly from SPT's way of generating these.
    private handleWeaponItem(items: IItem[], tpl: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>): IItem[] {
        const rootItem = items[0];

        // Get the original weapon preset
        const weaponPreset = this.cloner.clone(this.presetHelper.getDefaultPreset(tpl));
        if (weaponPreset?._items) {
            const itemWithChildren = this.itemHelper.reparentItemAndChildren(weaponPreset._items[0], weaponPreset._items);

            if (itemWithChildren?.length > 0) {
                items = this.itemHelper.reparentItemAndChildren(rootItem, itemWithChildren);
            }
        }

        const magazine = items.find((x) => x.slotId === "mod_magazine");

        if (magazine && this.randomUtil.getChance100(this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION).magazineLootHasAmmoChancePercent)) {
            // Get required templates
            const magTemplate = this.itemHelper.getItem(magazine._tpl)[1];
            const weaponTemplate = this.itemHelper.getItem(tpl)[1];
            const defaultWeapon = this.itemHelper.getItem(rootItem._tpl)[1];

            // Fill the magazine with cartridges
            const magazineWithCartridges: IItem[] = [magazine];
            this.itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, magTemplate, staticAmmoDist, weaponTemplate._props.ammoCaliber, this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION).minFillStaticMagazinePercent / 100, defaultWeapon._props.defAmmo, defaultWeapon);

            // Replace the original magazine with the filled version
            items.splice(items.indexOf(magazine), 1, ...magazineWithCartridges);
        }

        return items;
    }

    private handleMagazineItem(items: IItem[], itemTemplate: ITemplateItem, staticAmmoDist: Record<string, IStaticAmmoDetails[]>): void {
        if (this.randomUtil.getChance100(this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION).magazineLootHasAmmoChancePercent)) {
            const magazineWithCartridges: IItem[] = [items[0]];
            this.itemHelper.fillMagazineWithRandomCartridge(magazineWithCartridges, itemTemplate, staticAmmoDist, null, this.configServer.getConfig<ILocationConfig>(ConfigTypes.LOCATION).minFillStaticMagazinePercent / 100);
            items.splice(0, 1, ...magazineWithCartridges);
        }
    }

    private handleContainerOrBackpackItem(items: IItem[], staticAmmoDist: Record<string, IStaticAmmoDetails[]>, modifier: number): void {
        const containerLoot = this.createLootInLooseContainer(items[0]._tpl, items[0]._id, staticAmmoDist, modifier);
        containerLoot.forEach((containerItem) => items.push(containerItem));
    }

    public createLootInLooseContainer(tpl: string, id: string, staticAmmoDist: Record<string, IStaticAmmoDetails[]>, modifier = 0.5): IItem[] {
        if (modifier === 0) {
            return [];
        }

        const tables = this.databaseService.getTables();
        const items = tables.templates.items;
        const item = items[tpl];

        // Ensure filters exist for item
        if (!item._props.Grids[0]._props.filters[0]) {
            this.logger.debug(`${item._name} doesn't have a filter, setting default filter!`);
            item._props.Grids[0]._props.filters = [
                {
                    Filter: ["54009119af1c881c07000029"],
                    ExcludedFilter: [],
                },
            ];
        }

        // Clone filters
        let whitelist = [...item._props.Grids[0]._props.filters[0].Filter];
        let blacklist = new Set(item._props?.Grids[0]._props.filters[0]?.ExcludedFilter || []);

        const amount = this.randomUtil.getInt(1, item._props.Grids[0]._props.cellsH * item._props.Grids[0]._props.cellsV * modifier);
        let fill = 0;

        // Use cache for whitelist if available
        if (this.looseContainerItemFilterIndexCache[tpl]) {
            whitelist = this.looseContainerItemFilterIndexCache[tpl];
        } else {
            this.logger.debug(`${tpl} is new, generating whitelist`);

            // Expand whitelist and blacklist with children items
            const newWhiteList = this.expandItemsWithChildrenItemIds(whitelist, items);
            const newBlackList = this.expandItemsWithChildrenItemIds(Array.from(blacklist), items);

            whitelist = newWhiteList;
            blacklist = new Set(newBlackList);

            // Remove blacklist items from whitelist
            whitelist = whitelist.filter((item) => !blacklist.has(item));

            // Remove invalid items and built-in inserts from whitelist
            whitelist = whitelist.filter((itemId) => !this.itemHelper.isOfBaseclass(itemId, BaseClasses.BUILT_IN_INSERTS) && this.itemHelper.isValidItem(itemId) && items[itemId]?._props.Prefab.path !== "");

            // Cache the result for later reuse
            this.looseContainerItemFilterIndexCache[tpl] = whitelist;
        }

        // Return here as we cant put loose loot into containers that have no whitelisted items.
        if (whitelist.length === 0) {
            this.logger.warning(`${tpl} whitelist is empty`);
            return [];
        }

        const itemArray = new ProbabilityObjectArray<string>(this.mathUtil, this.cloner);
        whitelist.forEach((itemId) => {
            let itemWeight = 1;

            const price = tables.templates.prices[itemId];
            if (price) {
                itemWeight = Math.round(1000 / Math.pow(price, 1 / 3));
            } else if (itemId === "5449016a4bdc2d6f028b456f") {
                // Roubles
                itemWeight = 500;
            } else if (["5696686a4bdc2da3298b456a", "569668774bdc2da2298b4568"].includes(itemId)) {
                // Dollars, Euros
                itemWeight = 100;
            }

            itemArray.push(new ProbabilityObject(itemId, itemWeight));
        });

        // Generate loot items
        const generatedItems: IItem[] = [];
        const limits: ILootInlooseContainerLimitConfig = this.config.getConfig().lootinLooseContainer.LootInlooseContainerLimitConfig[tpl] ?? null;
        let attempts = 0;
        let drawnKeys = 0;
        let drawnKeycards = 0;
        while (fill <= amount && attempts < 100) {
            attempts++;

            // Handle if we should draw an item from the ProbabilityObjectArray (Weighted) or from the whitelist
            const drawnItemTpl = this.config.getConfig().general.itemWeights ? itemArray.draw(1, true)[0] : whitelist[this.randomUtil.getInt(0, whitelist.length - 1)];

            if (limits) {
                //Todo: Maybe we should filter out these items out of the itemArray and whitelist after we hit the limit?

                if (limits.keys != null) {
                    if (this.itemHelper.isOfBaseclass(drawnItemTpl, BaseClasses.KEY_MECHANICAL)) {
                        if (drawnKeys >= limits.keys) {
                            continue; // Skip keys if limit is reached
                        }

                        drawnKeys++;
                    }
                }

                if (limits.keycards != null) {
                    if (this.itemHelper.isOfBaseclass(drawnItemTpl, BaseClasses.KEYCARD)) {
                        if (drawnKeycards >= limits.keycards) {
                            continue; // Skip keycards if limit is reached
                        }

                        drawnKeycards++;
                    }
                }
            }

            const lootItem = this.createStaticLootItem(drawnItemTpl, staticAmmoDist, id);
            lootItem.items[0].slotId = "main";
            fill += lootItem.height * lootItem.width;

            if (fill > amount) {
                break;
            }

            generatedItems.push(...lootItem.items);
        }

        return generatedItems;
    }

    private expandItemsWithChildrenItemIds(itemsToExpand: string[], items: Record<string, any>): string[] {
        const expandedItems: string[] = [];

        itemsToExpand.forEach((content) => {
            const childItems = this.lotsOfLootItemHelper.findAndReturnChildrenItemIdsByItems(items, content);
            expandedItems.push(...childItems);
        });

        return expandedItems;
    }
}
