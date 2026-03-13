import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import type { RevitBridgeClient } from "../revit-client.js";
import type { NamingContext } from "../types.js";
import { revitError } from "./shared.js";

// ── Naming rule helpers ─────────────────────────────────────────────────────

const KNOWN_TOKENS = new Set([
  "originalName",
  "date",
  "time",
  "year",
  "month",
  "day",
  "revitVersion",
  "projectNumber",
  "projectName",
  "buildingName",
  "suffix",
]);

function sanitize(s: string): string {
  return s.replace(/[\\/:*?"<>|]/g, "").trim();
}

function applyNamingRule(template: string, ctx: NamingContext): string {
  const now = new Date();
  const pad = (n: number, w = 2) => String(n).padStart(w, "0");

  const year = String(now.getFullYear());
  const month = pad(now.getMonth() + 1);
  const day = pad(now.getDate());

  let result = template
    .replace(/{originalName}/g, sanitize(ctx.originalName))
    .replace(/{date}/g, `${year}${month}${day}`)
    .replace(/{time}/g, `${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`)
    .replace(/{year}/g, year)
    .replace(/{month}/g, month)
    .replace(/{day}/g, day)
    .replace(/{revitVersion}/g, sanitize(ctx.revitVersion))
    .replace(/{projectNumber}/g, sanitize(ctx.projectNumber))
    .replace(/{projectName}/g, sanitize(ctx.projectName))
    .replace(/{buildingName}/g, sanitize(ctx.buildingName))
    .replace(/{suffix}/g, sanitize(ctx.suffix ?? ""));

  result = result.replace(/{counter:(\d+)}/g, (_match, widthStr) => {
    const width = parseInt(widthStr, 10);
    const value = ctx.counterSeed ?? 1;
    return pad(value, width);
  });

  return result.replace(/[_-]{2,}/g, "_").replace(/^[_-]|[_-]$/g, "");
}

function suggestNamingRule(ctx: Partial<NamingContext>): string {
  const parts: string[] = [];
  if (ctx.projectNumber) parts.push("{projectNumber}");
  if (ctx.projectName) parts.push("{projectName}");
  if (ctx.buildingName) parts.push("{buildingName}");
  parts.push("{originalName}");
  parts.push("RVT{revitVersion}");
  parts.push("{date}");
  return parts.join("_");
}

function validateTemplate(template: string): string[] {
  const found = [...template.matchAll(/{(\w+)(?::\d+)?}/g)].map((m) => m[1]);
  return found.filter((t) => !KNOWN_TOKENS.has(t));
}

async function buildNamingContext(
  revit: RevitBridgeClient,
  suffix?: string,
  counterSeed?: number
): Promise<NamingContext> {
  const info = await revit.getModelInfo();
  const originalName = info.filePath
    .replace(/\\/g, "/")
    .split("/")
    .pop()!
    .replace(/\.rvt$/i, "");

  return {
    originalName,
    revitVersion: info.revitVersion,
    projectNumber: info.projectNumber,
    projectName: info.projectName,
    buildingName: info.buildingName,
    suffix,
    counterSeed,
  };
}

// ── Tool registration ───────────────────────────────────────────────────────

export function registerFileTools(
  server: McpServer,
  revit: RevitBridgeClient
): void {
  server.tool(
    "suggest_save_as_name",
    "Preview what a file will be named given a naming rule template, using the currently open model's project info. Use this before save_as_with_rule to confirm the output name.",
    {
      namingTemplate: z
        .string()
        .optional()
        .describe(
          "Template with tokens like {projectNumber}_{originalName}_RVT{revitVersion}_{date}. Leave blank to auto-suggest."
        ),
      suffix: z
        .string()
        .optional()
        .describe("Optional suffix to inject via {suffix} token"),
      counterSeed: z
        .number()
        .int()
        .optional()
        .describe("Starting value for {counter:N} token"),
    },
    async ({ namingTemplate, suffix, counterSeed }) => {
      try {
        const ctx = await buildNamingContext(revit, suffix, counterSeed);
        const template = namingTemplate ?? suggestNamingRule(ctx);
        const unknownTokens = validateTemplate(template);

        if (unknownTokens.length > 0) {
          return {
            content: [
              {
                type: "text" as const,
                text: `Template contains unknown tokens: ${unknownTokens.join(", ")}\n\nValid tokens: {originalName}, {date}, {time}, {year}, {month}, {day}, {revitVersion}, {projectNumber}, {projectName}, {buildingName}, {suffix}, {counter:N}`,
              },
            ],
            isError: true,
          };
        }

        const resultName = applyNamingRule(template, ctx);

        return {
          content: [
            {
              type: "text" as const,
              text: [
                `Template:  ${template}`,
                `Result:    ${resultName}.rvt`,
                ``,
                `Context used:`,
                `  originalName:  ${ctx.originalName}`,
                `  projectNumber: ${ctx.projectNumber}`,
                `  projectName:   ${ctx.projectName}`,
                `  buildingName:  ${ctx.buildingName}`,
                `  revitVersion:  ${ctx.revitVersion}`,
              ].join("\n"),
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
    "save_as_with_rule",
    "Save the current Revit model to a new location using a naming rule. Typically called after opening and upgrading a file.",
    {
      targetFolder: z
        .string()
        .describe(
          "Absolute path to the destination folder, e.g. C:\\Projects\\Upgraded"
        ),
      namingTemplate: z
        .string()
        .optional()
        .describe(
          "Naming template. Leave blank to auto-suggest based on project info."
        ),
      suffix: z
        .string()
        .optional()
        .describe("Optional suffix for {suffix} token"),
      counterSeed: z
        .number()
        .int()
        .optional()
        .describe("Starting value for {counter:N} token"),
      overwrite: z
        .boolean()
        .optional()
        .default(false)
        .describe("Overwrite if the destination file already exists"),
      compact: z
        .boolean()
        .optional()
        .default(true)
        .describe("Compact the file on save (reduces file size)"),
      worksharingMode: z
        .enum(["detach", "preserve", "none"])
        .optional()
        .default("detach")
        .describe(
          '"detach" = save as a non-workshared local copy, "preserve" = keep worksharing, "none" = central-file behaviour'
        ),
    },
    async ({
      targetFolder,
      namingTemplate,
      suffix,
      counterSeed,
      overwrite,
      compact,
      worksharingMode,
    }) => {
      try {
        const ctx = await buildNamingContext(revit, suffix, counterSeed);
        const template = namingTemplate ?? suggestNamingRule(ctx);
        const newName = applyNamingRule(template, ctx);

        const result = await revit.saveAs(newName, targetFolder, {
          overwrite,
          compact,
          worksharingMode,
        });

        return {
          content: [
            {
              type: "text" as const,
              text: result.success
                ? [
                    `File saved successfully.`,
                    `Original: ${result.originalFilePath}`,
                    `New path: ${result.newFilePath}`,
                  ].join("\n")
                : `Save As failed: ${result.message}`,
            },
          ],
          isError: !result.success,
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
    "open_and_upgrade",
    "Open a Revit file from an older version and upgrade it to the current installed Revit version. Use save_as_with_rule afterwards to save to the correct location.",
    {
      sourcePath: z
        .string()
        .describe("Absolute path to the .rvt file to open and upgrade"),
      audit: z
        .boolean()
        .optional()
        .default(true)
        .describe(
          "Run Audit on open to repair corrupted elements (recommended)"
        ),
    },
    async ({ sourcePath, audit }) => {
      try {
        const result = await revit.openAndUpgrade(sourcePath, audit);
        return {
          content: [
            {
              type: "text" as const,
              text: result.success
                ? `File opened and upgraded successfully.\n${result.message}`
                : `Open/upgrade failed: ${result.message}`,
            },
          ],
          isError: !result.success,
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
