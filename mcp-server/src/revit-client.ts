import axios, { AxiosInstance } from "axios";
import type {
  RevitElement,
  ModelInfo,
  ScriptResult,
  SaveAsResult,
  ElementQuery,
} from "./types.js";

export class RevitBridgeClient {
  private client: AxiosInstance;

  constructor(port = 8765) {
    this.client = axios.create({
      baseURL: `http://localhost:${port}`,
      timeout: 30_000,
      headers: { "Content-Type": "application/json" },
    });
  }

  async ping(): Promise<boolean> {
    try {
      const res = await this.client.get("/ping");
      return res.data?.status === "ok";
    } catch {
      return false;
    }
  }

  async getModelInfo(): Promise<ModelInfo> {
    const res = await this.client.get("/model/info");
    return res.data;
  }

  async getCategories(): Promise<string[]> {
    const res = await this.client.get("/model/categories");
    return res.data.categories;
  }

  async getLevels(): Promise<Array<{ name: string; elevation: number }>> {
    const res = await this.client.get("/model/levels");
    return res.data.levels;
  }

  async getElements(query: ElementQuery): Promise<RevitElement[]> {
    const res = await this.client.post("/elements/query", query);
    return res.data.elements;
  }

  /** Fetch element by ID. Supports 64-bit element IDs (Revit 2024+). */
  async getElementById(elementId: number): Promise<RevitElement> {
    const res = await this.client.get(`/elements/${elementId}`);
    return res.data;
  }

  async executeDynamoScript(
    scriptPath: string,
    inputs: Record<string, unknown> = {},
    timeoutMs = 120_000
  ): Promise<ScriptResult> {
    const res = await this.client.post(
      "/scripts/dynamo",
      { scriptPath, inputs },
      { timeout: timeoutMs }
    );
    return res.data;
  }

  async executePyRevitScript(
    scriptPath: string,
    args: string[] = [],
    timeoutMs = 60_000
  ): Promise<ScriptResult> {
    const res = await this.client.post(
      "/scripts/pyrevit",
      { scriptPath, args },
      { timeout: timeoutMs }
    );
    return res.data;
  }

  async saveAs(
    newName: string,
    targetFolder: string,
    options: {
      overwrite?: boolean;
      compact?: boolean;
      worksharingMode?: "detach" | "preserve" | "none";
    } = {}
  ): Promise<SaveAsResult> {
    const res = await this.client.post("/file/saveas", {
      newName,
      targetFolder,
      ...options,
    });
    return res.data;
  }

  async openAndUpgrade(
    sourcePath: string,
    audit?: boolean
  ): Promise<{ success: boolean; message: string; upgradedPath: string }> {
    const res = await this.client.post(
      "/file/open-upgrade",
      { sourcePath, audit: audit ?? true },
      { timeout: 300_000 }
    );
    return res.data;
  }
}
