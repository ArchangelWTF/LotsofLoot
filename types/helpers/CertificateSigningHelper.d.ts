import type { ILogger } from "@spt/models/spt/utils/ILogger";
import { FileSystem } from "@spt/utils/FileSystem";
export declare class CertificateSigningHelper {
    protected fileSystem: FileSystem;
    protected logger: ILogger;
    protected readonly CertificatePath = "./user/cert/";
    constructor(fileSystem: FileSystem, logger: ILogger);
    getOrGenerateCert(): Promise<void>;
    getCertAndKey(): object;
    generateCert(): void;
}
