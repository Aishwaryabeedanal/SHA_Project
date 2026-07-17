# Solution Health Analyzer (SHA)

A Visual Studio 2022/2026 extension that provides comprehensive solution health analysis.

## Features

- **Build Issues** — Errors and warnings with humanized explanations
- **NuGet Packages** — Version tracking, vulnerability checks, upgrade commands
- **TODO/FIXME Scanner** — Finds all TODO comments with file and line numbers
- **AI Insights** — Powered by Google Gemini for analysis and recommendations
- **Health Score** — 0-100 score with Excellent/Good/Needs Attention/Critical status
- **MCP Server** — JSON-RPC tools for GitHub Copilot Chat integration

## Output Channels

1. **Health Dashboard** — WPF tool window in Visual Studio
2. **Output Pane** — Formatted text in VS Output window
3. **MCP Server** — JSON-RPC on `http://localhost:5010/mcp/`

## Setup

1. Build and install the VSIX extension
2. Set `GEMINI_API_KEY` environment variable for AI features
3. Open a solution → Tools → Solution Health Analyzer

## MCP Tools

| Tool | Description |
|------|-------------|
| `scan_solution` | Full health scan |
| `get_packages` | NuGet package details |
| `get_health_score` | Score and status |
| `get_build_issues` | Errors and warnings |
| `get_todos` | TODO/FIXME comments |
| `get_ai_insights` | AI analysis |
| `optimize_code` | Code optimization |

## Tech Stack

- C# / .NET Framework 4.7.2 (VSIX)
- .NET 8.0 (Standalone MCP Server)
- WPF (Dashboard UI)
- Roslyn (Build analysis)
- Google Gemini AI API
- MCP Protocol (JSON-RPC)
