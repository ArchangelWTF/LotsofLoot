import { DependencyContainer } from "tsyringe";

import { IPreSptLoadModAsync } from "@spt/models/external/IPreSptLoadModAsync";
import { IPostDBLoadModAsync } from "@spt/models/external/IPostDBLoadModAsync";

import { LotsofLoot } from "./LotsofLoot";
import { Container } from "./di/Container";

class Mod implements IPreSptLoadModAsync, IPostDBLoadModAsync {
    public async preSptLoadAsync(container: DependencyContainer): Promise<void> {
        Container.register(container);

        await container.resolve<LotsofLoot>("LotsofLoot").preSptLoadAsync(container); 
    }

    public async postDBLoadAsync(container: DependencyContainer): Promise<void> {
        await container.resolve<LotsofLoot>("LotsofLoot").postDBLoadAsync(container);
    }
}

module.exports = { mod: new Mod() };