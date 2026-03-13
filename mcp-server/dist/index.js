import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { RevitBridgeClient } from "./revit-client.js";
import { registerModelTools } from "./tools/model-tools.js";
import { registerElementTools } from "./tools/element-tools.js";
import { registerScriptTools } from "./tools/script-tools.js";
import { registerFileTools } from "./tools/file-tools.js";
const server = new McpServer({
    name: "revit-mcp",
    version: "1.0.0",
});
const port = parseInt(process.env.REVIT_BRIDGE_PORT ?? "8765", 10);
const revit = new RevitBridgeClient(port);
registerModelTools(server, revit);
registerElementTools(server, revit);
registerScriptTools(server, revit);
registerFileTools(server, revit);
const transport = new StdioServerTransport();
await server.connect(transport);
console.error(`Revit MCP server running on stdio (bridge port ${port})`);
//# sourceMappingURL=index.js.map