import { z } from "zod";
import { revitError } from "./shared.js";
export function registerScriptTools(server, revit) {
    server.tool("run_dynamo_script", "Execute a Dynamo script (.dyn file) from disk within the current Revit session", {
        scriptPath: z
            .string()
            .describe("Absolute path to the .dyn Dynamo script file"),
        inputs: z
            .record(z.unknown())
            .optional()
            .default({})
            .describe("Key-value map of input node names to values for the Dynamo graph"),
        timeoutSeconds: z
            .number()
            .int()
            .positive()
            .max(600)
            .optional()
            .default(120)
            .describe("Execution timeout in seconds (default 120)"),
    }, async ({ scriptPath, inputs, timeoutSeconds }) => {
        try {
            const result = await revit.executeDynamoScript(scriptPath, inputs ?? {}, (timeoutSeconds ?? 120) * 1000);
            const summary = result.success
                ? "Script succeeded"
                : "Script failed";
            return {
                content: [
                    {
                        type: "text",
                        text: [
                            summary,
                            `Execution time: ${result.executionTimeMs}ms`,
                            result.output ? `\nOutput:\n${result.output}` : "",
                            result.errors.length > 0
                                ? `\nErrors:\n${result.errors.join("\n")}`
                                : "",
                        ]
                            .filter(Boolean)
                            .join("\n"),
                    },
                ],
                isError: !result.success,
            };
        }
        catch (err) {
            return {
                content: [{ type: "text", text: `Error: ${revitError(err)}` }],
                isError: true,
            };
        }
    });
    server.tool("run_pyrevit_script", "Execute a pyRevit Python script (.py) within the current Revit session", {
        scriptPath: z
            .string()
            .describe("Absolute path to the .py pyRevit script file"),
        args: z
            .array(z.string())
            .optional()
            .default([])
            .describe("Command-line arguments to pass to the script"),
        timeoutSeconds: z
            .number()
            .int()
            .positive()
            .max(300)
            .optional()
            .default(60)
            .describe("Execution timeout in seconds (default 60)"),
    }, async ({ scriptPath, args, timeoutSeconds }) => {
        try {
            const result = await revit.executePyRevitScript(scriptPath, args ?? [], (timeoutSeconds ?? 60) * 1000);
            const summary = result.success
                ? "Script succeeded"
                : "Script failed";
            return {
                content: [
                    {
                        type: "text",
                        text: [
                            summary,
                            `Execution time: ${result.executionTimeMs}ms`,
                            result.output ? `\nOutput:\n${result.output}` : "",
                            result.errors.length > 0
                                ? `\nErrors:\n${result.errors.join("\n")}`
                                : "",
                        ]
                            .filter(Boolean)
                            .join("\n"),
                    },
                ],
                isError: !result.success,
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
//# sourceMappingURL=script-tools.js.map