# Revit MCP

Connect Claude to Autodesk Revit via the Model Context Protocol.

## Supported Versions

| Revit | .NET Target | ElementId | Status |
|-------|-------------|-----------|--------|
| 2022 | net4.8 | `int` constructor + `IntegerValue` | Supported |
| 2023 | net4.8 | `int` constructor + `IntegerValue` | Supported |
| 2024 | net4.8 | `long` constructor + `Value` (int deprecated) | Supported |
| 2025 | net8.0-windows | `long` constructor + `Value` (int deprecated) | Supported |
| 2026 | net8.0-windows | `long` constructor + `Value` only (int **removed**) | Supported |
| 2027 | net8.0-windows (expected) | Same as 2026 (expected) | Pre-wired |

## Architecture

```
Claude (Claude Desktop / Claude Code)
        |  MCP (stdio)
        v
  mcp-server/          <- TypeScript MCP server (Node.js)
        |  HTTP localhost:8765
        v
  RevitMcpBridge.dll   <- C# Revit add-in (runs inside Revit.exe)
        |  Revit API
        v
  Revit (open model)
```

## Available Tools

| Tool | Description |
|------|-------------|
| `revit_ping` | Check bridge is alive |
| `get_model_info` | Project info, file path, Revit version, element count |
| `get_categories` | All element categories in the model |
| `get_levels` | All levels with elevations |
| `query_elements` | Filter elements by category, family, type, level, parameters |
| `get_element_by_id` | Full parameter list for one element |
| `run_dynamo_script` | Execute a `.dyn` Dynamo graph |
| `run_pyrevit_script` | Execute a `.py` pyRevit script |
| `suggest_save_as_name` | Preview output filename from a naming rule |
| `save_as_with_rule` | Save model to new folder with rule-based name |
| `open_and_upgrade` | Open and upgrade an older `.rvt` file |

## Naming Rule Tokens

Use these tokens in `namingTemplate`:

| Token | Replaced with |
|-------|---------------|
| `{originalName}` | Filename without extension |
| `{date}` | `YYYYMMDD` |
| `{time}` | `HHmmss` |
| `{year}` / `{month}` / `{day}` | Date parts |
| `{revitVersion}` | e.g. `2024` |
| `{projectNumber}` | From Revit Project Information |
| `{projectName}` | From Revit Project Information |
| `{buildingName}` | From Revit Project Information |
| `{suffix}` | User-supplied string |
| `{counter:N}` | Zero-padded counter, N digits wide |

Example: `{projectNumber}_{buildingName}_RVT{revitVersion}_{date}`

## Prerequisites

- Revit 2022-2027
- Node.js 18+
- .NET SDK 8.0+ (for building the C# add-in)
- Dynamo for Revit (optional, for Dynamo scripts)
- pyRevit (optional, for pyRevit scripts)

## Setup

### 1. Build the C# Revit Add-in

```bash
cd revit-addin

# Build for Revit 2024 (default)
dotnet build RevitMcpBridge/RevitMcpBridge.csproj

# Build for a specific version
dotnet build RevitMcpBridge/RevitMcpBridge.csproj -p:RevitVersion=2025
dotnet build RevitMcpBridge/RevitMcpBridge.csproj -p:RevitVersion=2026
dotnet build RevitMcpBridge/RevitMcpBridge.csproj -p:RevitVersion=2027

# Custom Revit install location
dotnet build RevitMcpBridge/RevitMcpBridge.csproj -p:RevitVersion=2026 -p:RevitInstallDir="D:\Autodesk\Revit 2026"
```

Copy the output files to a deployment folder (e.g. `C:\RevitMcpBridge\`):
- `RevitMcpBridge.dll`
- `Newtonsoft.Json.dll`

### 2. Register the Revit Add-in

Edit `revit-addin/RevitMcpBridge.addin`:
- Set `<Assembly>` to the full path of your deployed `RevitMcpBridge.dll`

Copy `RevitMcpBridge.addin` to:
```
%APPDATA%\Autodesk\Revit\Addins\2026\
```
Replace `2026` with your Revit version year.

Restart Revit. Check the log at `%LOCALAPPDATA%\RevitMcpBridge\bridge.log`.

### 3. Build the MCP Server

```bash
cd mcp-server
npm install
npm run build
```

### 4. Register with Claude

Add to your Claude Desktop config (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "revit": {
      "command": "node",
      "args": ["C:/path/to/RevitMCP/mcp-server/dist/index.js"],
      "env": {
        "REVIT_BRIDGE_PORT": "8765"
      }
    }
  }
}
```

Restart Claude Desktop. The Revit tools will appear in Claude's tool list.

## Custom Port

Set `REVIT_MCP_PORT` before starting Revit:

```powershell
$env:REVIT_MCP_PORT = "9000"
& "C:\Program Files\Autodesk\Revit 2026\Revit.exe"
```

Match it in the MCP server config: `"REVIT_BRIDGE_PORT": "9000"`.

## Multi-Version Build Notes

The C# project uses conditional compilation to handle API differences:

- **`ElementIdHelper.cs`** wraps `ElementId` construction and value extraction so the same code compiles against all Revit versions (2022-2027).
- Compilation symbols (`REVIT2026`, `REVIT_V_GTE_2024`, `REVIT_V_GTE_2026`, etc.) are automatically set based on the `-p:RevitVersion=` build parameter.
- Revit 2022-2024 targets `net4.8`; Revit 2025+ targets `net8.0-windows`.

### Revit 2026 Breaking Changes Handled

- `ElementId(int)` constructor **removed** -> use `ElementId(long)` via `ElementIdHelper.Create()`
- `ElementId.IntegerValue` **removed** -> use `ElementId.Value` via `ElementIdHelper.GetValue()`
- CefSharp removed from Revit install (does not affect this add-in)
- Add-in dependency isolation now available (`UseRevitContext` manifest option)

### Revit 2027 Preparation

Revit 2027 has not been released yet, but the project is pre-wired:

- `REVIT2027` and `REVIT_V_GTE_2027` compilation symbols ready
- Expected to continue on .NET 8 / `net8.0-windows`
- `ElementIdHelper` already uses 64-bit APIs so no further migration needed
- Watch for: deprecated APIs becoming removed (electrical classes, Curve.Intersect overloads, Zone APIs, rebar hook-to-termination renames), and potential new required manifest settings

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Bridge not reachable | Check Revit loaded the add-in (Add-ins tab) |
| Permission denied on port | Register the HTTP prefix (see below) |
| Dynamo script fails | Ensure DynamoRevit is installed and path is correct |
| pyRevit not found | Add pyRevit to PATH |
| File save fails | Ensure target folder exists and Revit has write access |
| Assembly version conflict (2026+) | Set `UseRevitContext` to `false` in .addin manifest |

### Registering the HTTP prefix (one-time, run as admin)

```powershell
netsh http add urlacl url=http://localhost:8765/ user=Everyone
```
