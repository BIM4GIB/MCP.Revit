import type { RevitElement, ModelInfo, ScriptResult, SaveAsResult, ElementQuery } from "./types.js";
export declare class RevitBridgeClient {
    private client;
    constructor(port?: number);
    ping(): Promise<boolean>;
    getModelInfo(): Promise<ModelInfo>;
    getCategories(): Promise<string[]>;
    getLevels(): Promise<Array<{
        name: string;
        elevation: number;
    }>>;
    getElements(query: ElementQuery): Promise<RevitElement[]>;
    /** Fetch element by ID. Supports 64-bit element IDs (Revit 2024+). */
    getElementById(elementId: number): Promise<RevitElement>;
    executeDynamoScript(scriptPath: string, inputs?: Record<string, unknown>, timeoutMs?: number): Promise<ScriptResult>;
    executePyRevitScript(scriptPath: string, args?: string[], timeoutMs?: number): Promise<ScriptResult>;
    saveAs(newName: string, targetFolder: string, options?: {
        overwrite?: boolean;
        compact?: boolean;
        worksharingMode?: "detach" | "preserve" | "none";
    }): Promise<SaveAsResult>;
    openAndUpgrade(sourcePath: string, audit?: boolean): Promise<{
        success: boolean;
        message: string;
        upgradedPath: string;
    }>;
}
//# sourceMappingURL=revit-client.d.ts.map