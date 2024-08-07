export interface ILotsofLootConfig {
    general: GeneralOptions;
    looseLootMultiplier: Record<string, number>;
    staticLootMultiplier: Record<string, number>;
    limits: Record<string, number>;
    markedRoom: MarkedRoomConfig;
    containers: Record<string, number>;
}

export interface GeneralOptions {
    debug: boolean;
    allowLootOverlay: boolean;
    removeBackpackRestrictions: boolean;
    disableFleaRestrictions: boolean;
    rustedKeyRoomIncludesKeycards: boolean;
    looseContainerModifier: number;
    looseBackpackModifier: number;
    itemWeights: boolean;
    priceCorrection: Record<string, number>;
}

export interface MarkedRoomConfig {
    multiplier: Record<string, number>;
    extraItems: Record<string, number>;
    itemGroups: Record<string, number>;
}