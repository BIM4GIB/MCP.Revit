import { revitError } from "./shared.js";
export function registerModelTools(server, revit) {
    server.tool("revit_ping", "Check if the Revit bridge add-in is running and reachable", {}, async () => {
        const alive = await revit.ping();
        return {
            content: [
                {
                    type: "text",
                    text: alive
                        ? "Revit bridge is online and responding."
                        : "Revit bridge is not reachable. Make sure Revit is open and the RevitMcpBridge add-in is loaded.",
                },
            ],
        };
    });
    server.tool("get_model_info", "Get high-level information about the currently open Revit model (title, file path, version, project info, element count)", {}, async () => {
        try {
            const info = await revit.getModelInfo();
            return {
                content: [{ type: "text", text: JSON.stringify(info, null, 2) }],
            };
        }
        catch (err) {
            return {
                content: [{ type: "text", text: `Error: ${revitError(err)}` }],
                isError: true,
            };
        }
    });
    server.tool("get_categories", "List all element categories present in the current Revit model", {}, async () => {
        try {
            const categories = await revit.getCategories();
            return {
                content: [
                    {
                        type: "text",
                        text: `Found ${categories.length} categories:\n${categories.join("\n")}`,
                    },
                ],
            };
        }
        catch (err) {
            return {
                content: [{ type: "text", text: `Error: ${revitError(err)}` }],
                isError: true,
            };
        }
    });
    server.tool("get_levels", "List all levels in the current Revit model with their elevations", {}, async () => {
        try {
            const levels = await revit.getLevels();
            const text = levels
                .map((l) => `${l.name}: ${l.elevation.toFixed(3)} ft`)
                .join("\n");
            return {
                content: [{ type: "text", text: text || "No levels found." }],
            };
        }
        catch (err) {
            return {
                content: [{ type: "text", text: `Error: ${revitError(err)}` }],
                isError: true,
            };
        }
    });
}
//# sourceMappingURL=model-tools.js.map