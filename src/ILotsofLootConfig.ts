export interface ILotsofLootConfig {
    general: GeneralOptions;
    looseLootMultiplier: Record<string, number>;
    staticLootMultiplier: Record<string, number>;
    limits: Record<string, number>;
    markedRoom: MarkedRoomConfig;
    lootinLooseContainer: ILootInLooseContainerConfig;
    changeRelativeProbabilityInPool: Record<string, number>;
    changeProbabilityOfPool: Record<string, number>;
    containers: Record<string, number>;
}

export interface GeneralOptions {
    debug: boolean;
    allowLootOverlay: boolean;
    removeBackpackRestrictions: boolean;
    disableFleaRestrictions: boolean;
    rustedKeyRoomIncludesKeycards: boolean;
    itemWeights: boolean;
    priceCorrection: Record<string, number>;
}

export interface MarkedRoomConfig {
    multiplier: Record<string, number>;
    extraItems: Record<string, number>;
    itemGroups: Record<string, number>;
}

export interface ILootInLooseContainerConfig {
    lootInContainerModifier: number;
    lootInBackpackModifier: number;
    LootInlooseContainerLimitConfig: Record<string, ILootInlooseContainerLimitConfig>;
}

export interface ILootInlooseContainerLimitConfig {
    keys: number;
    keycards: number;
}