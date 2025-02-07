import { inject, injectable } from "tsyringe";

@injectable()
export class LotsofLootRandomUtil {
    /**
     * During 3.10's lifecycle SPT changed their randomizer to be more random, this is quite nice however it works against us
     * When we overlay loot, this class re-implements the old methods.
     */
    // biome-ignore lint/suspicious/noEmptyBlockStatements: No need to inject anything here (yet)
    constructor() {}

    public getNormallyDistributedRandomNumber(mean: number, sigma: number, attempt = 0): number {
        let u = 0;
        let v = 0;
        while (u === 0) {
            u = Math.random(); // Converting [0,1) to (0,1)
        }
        while (v === 0) {
            v = Math.random();
        }
        const w = Math.sqrt(-2.0 * Math.log(u)) * Math.cos(2.0 * Math.PI * v);
        const valueDrawn = mean + w * sigma;
        if (valueDrawn < 0) {
            if (attempt > 100) {
                return this.getFloat(0.01, mean * 2);
            }

            return this.getNormallyDistributedRandomNumber(mean, sigma, attempt + 1);
        }

        return valueDrawn;
    }

    public getFloat(min: number, max: number): number {
        return Math.random() * (max - min) + min;
    }
}
