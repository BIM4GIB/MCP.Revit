import axios from "axios";
export class RevitBridgeClient {
    client;
    constructor(port = 8765) {
        this.client = axios.create({
            baseURL: `http://localhost:${port}`,
            timeout: 30_000,
            headers: { "Content-Type": "application/json" },
        });
    }
    async ping() {
        try {
            const res = await this.client.get("/ping");
            return res.data?.status === "ok";
        }
        catch {
            return false;
        }
    }
    async getModelInfo() {
        const res = await this.client.get("/model/info");
        return res.data;
    }
    async getCategories() {
        const res = await this.client.get("/model/categories");
        return res.data.categories;
    }
    async getLevels() {
        const res = await this.client.get("/model/levels");
        return res.data.levels;
    }
    async getElements(query) {
        const res = await this.client.post("/elements/query", query);
        return res.data.elements;
    }
    /** Fetch element by ID. Supports 64-bit element IDs (Revit 2024+). */
    async getElementById(elementId) {
        const res = await this.client.get(`/elements/${elementId}`);
        return res.data;
    }
    async executeDynamoScript(scriptPath, inputs = {}, timeoutMs = 120_000) {
        const res = await this.client.post("/scripts/dynamo", { scriptPath, inputs }, { timeout: timeoutMs });
        return res.data;
    }
    async executePyRevitScript(scriptPath, args = [], timeoutMs = 60_000) {
        const res = await this.client.post("/scripts/pyrevit", { scriptPath, args }, { timeout: timeoutMs });
        return res.data;
    }
    async saveAs(newName, targetFolder, options = {}) {
        const res = await this.client.post("/file/saveas", {
            newName,
            targetFolder,
            ...options,
        });
        return res.data;
    }
    async openAndUpgrade(sourcePath, audit) {
        const res = await this.client.post("/file/open-upgrade", { sourcePath, audit: audit ?? true }, { timeout: 300_000 });
        return res.data;
    }
}
//# sourceMappingURL=revit-client.js.map