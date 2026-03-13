import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import type { RevitBridgeClient } from "../revit-client.js";
import { revitError } from "./shared.js";

export function registerElementTools(
  server: McpServer,
  revit: RevitBridgeClient
): void {
  server.tool(
    "query_elements",
    "Query elements in the Revit model by category, family, type, level, or parameter values",
    {
      category: z
        .string()
        .optional()
        .describe('Revit category name, e.g. "Walls", "Doors", "Rooms"'),
      familyName: z.string().optional().describe("Family name filter"),
      typeName: z.string().optional().describe("Type name filter"),
      levelName: z.string().optional().describe("Level name filter"),
      parameterFilters: z
        .array(
          z.object({
            parameterName: z.string(),
            operator: z.enum([
              "equals",
              "contains",
              "startsWith",
              "greaterThan",
              "lessThan",
            ]),
            value: z.union([z.string(), z.number()]),
          })
        )
        .optional()
        .describe("Parameter-level filters"),
      limit: z
        .number()
        .int()
        .positive()
        .max(500)
        .optional()
        .default(50)
        .describe("Max elements to return (default 50, max 500)"),
    },
    async (args) => {
      try {
        const elements = await revit.getElements(args);
        return {
          content: [
            {
              type: "text" as const,
              text:
                elements.length === 0
                  ? "No elements matched the query."
                  : JSON.stringify(elements, null, 2),
            },
          ],
        };
      } catch (err) {
        return {
          content: [{ type: "text" as const, text: `Error: ${revitError(err)}` }],
          isError: true,
        };
      }
    }
  );

  server.tool(
    "get_element_by_id",
    "Get full details and all parameters for a specific Revit element by its element ID (supports 64-bit IDs for Revit 2024+)",
    {
      elementId: z.number().int().describe("Revit element ID (integer, 64-bit safe)"),
    },
    async ({ elementId }) => {
      try {
        const element = await revit.getElementById(elementId);
        return {
          content: [
            { type: "text" as const, text: JSON.stringify(element, null, 2) },
          ],
        };
      } catch (err) {
        return {
          content: [{ type: "text" as const, text: `Error: ${revitError(err)}` }],
          isError: true,
        };
      }
    }
  );
}
