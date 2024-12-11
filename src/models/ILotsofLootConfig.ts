export interface ILotsofLootConfig {
    general: IGeneralOptions;
    looseLootMultiplier: Record<string, number>;
    staticLootMultiplier: Record<string, number>;
    limits: Record<string, number>;
    markedRoom: IMarkedRoomConfig;
    lootinLooseContainer: ILootInLooseContainerConfig;
    changeRelativeProbabilityInPool: Record<string, number>;
    changeProbabilityOfPool: Record<string, number>;
    containers: Record<string, number>;
}

export interface IGeneralOptions {
    debug: boolean;
    allowLootOverlay: boolean;
    removeBackpackRestrictions: boolean;
    disableFleaRestrictions: boolean;
    rustedKeyRoomIncludesKeycards: boolean;
    itemWeights: boolean;
    priceCorrection: Record<string, number>;
}

export interface IMarkedRoomConfig {
    multiplier: Record<string, number>;
    extraItems: Record<string, number>;
    itemGroups: Record<string, number>;
}

export interface ILootInLooseContainerConfig {
    lootInContainerModifier: number;
    lootInBackpackModifier: number;
    spawnLimits: Record<string, ILootInlooseContainerLimitConfig>;
}

export interface ILootInlooseContainerLimitConfig {
    keys: number;
    keycards: number;
}
