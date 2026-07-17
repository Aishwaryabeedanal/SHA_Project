using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SHA_Project.Models;

namespace SHA_Project.Services
{
    public class McpServerService
    {
        private HttpListener _listener;
        private CancellationTokenSource _cts;
        private static McpServerService _instance;
        private static readonly object _lock = new object();
        private bool _isRunning;

        public event Action<string> CommandReceived;
        public Func<string, Task<string>> CommandHandler { get; set; }
        
        public Action OpenToolWindow { get; set; }

        private HealthReport _lastReport;

        public static McpServerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new McpServerService();
                    }
                }
                return _instance;
            }
        }

        public McpServerService()
        {
            lock (_lock)
            {
                if (_instance == null)
                    _instance = this;
            }
        }

        public void SetLastReport(HealthReport report)
        {
            _lastReport = report;
        }

        public HealthReport GetLastReport() => _lastReport;

        public void Start()
        {
            if (_isRunning) return;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(
                    "http://localhost:5010/mcp/");
                _listener.Start();
                _isRunning = true;
                _cts = new CancellationTokenSource();
                Task.Run(() => ListenLoop(_cts.Token));
            }
            catch { }
        }

        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var ctx = await _listener
                        .GetContextAsync();
                    _ = Task.Run(() => HandleRequest(ctx));
                }
                catch
                {
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private async Task HandleRequest(
            HttpListenerContext ctx)
        {
            string body = "";
            using (var reader = new StreamReader(
                ctx.Request.InputStream,
                ctx.Request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            // CORS
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Origin", "*");
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Methods",
                "POST, GET, OPTIONS");
            ctx.Response.Headers.Add(
                "Access-Control-Allow-Headers",
                "Content-Type");

            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                return;
            }

            string response;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string method = root.TryGetProperty(
                    "method", out var m)
                    ? m.GetString() ?? "" : "";

                string id = root.TryGetProperty(
                    "id", out var idEl)
                    ? idEl.ToString() : "null";

                if (method == "tools/list")
                {
                    response = HandleToolsList(id);
                }
                else if (method == "tools/call")
                {
                    var paramsEl = root.TryGetProperty(
                        "params", out var p) ? p : default;

                    string toolName = "";
                    if (paramsEl.ValueKind !=
                        JsonValueKind.Undefined &&
                        paramsEl.TryGetProperty(
                            "name", out var n))
                    {
                        toolName = n.GetString() ?? "";
                    }

                    string arguments = "";
                    if (paramsEl.ValueKind !=
                        JsonValueKind.Undefined &&
                        paramsEl.TryGetProperty(
                            "arguments", out var args))
                    {
                        arguments = args.ToString();
                    }

                    response = await HandleToolCall(
                        id, toolName, arguments);
                }
                else
                {
                    CommandReceived?.Invoke(body);
                    response = CommandHandler != null
                        ? await CommandHandler(body)
                        : McpClientHelper.CreateErrorResponse(
                            id, -32601, "Method not found");
                }
            }
            catch (JsonException)
            {
                CommandReceived?.Invoke(body);
                response = CommandHandler != null
                    ? await CommandHandler(body)
                    : "{\"status\":\"ok\"}";
            }
            catch (Exception ex)
            {
                response = McpClientHelper
                    .CreateErrorResponse(
                        "null", -32603, ex.Message);
            }

            byte[] buf = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream
                .WriteAsync(buf, 0, buf.Length);
            ctx.Response.Close();
        }

        private string HandleToolsList(string id)
        {
            var tools = new[]
            {
                McpClientHelper.CreateToolDef(
                    "scan_solution",
                    "Full health scan returning build issues, " +
                    "NuGet details, TODOs, AI insights, " +
                    "health score.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "get_packages",
                    "Get all NuGet packages with health details.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "get_health_score",
                    "Get health score (0-100) and status.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "get_build_issues",
                    "Get build errors and warnings with " +
                    "humanized messages.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "get_todos",
                    "Get all TODO/FIXME comments.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "get_ai_insights",
                    "Get AI analysis and recommendations.",
                    new { type = "object",
                          properties = new { } }),
                McpClientHelper.CreateToolDef(
                    "optimize_code",
                    "Submit code for AI optimization.",
                    new
                    {
                        type = "object",
                        properties = new
                        {
                            code = new
                            {
                                type = "string",
                                description =
                                    "Code to optimize"
                            }
                        },
                        required = new[] { "code" }
                    })
            };

            return McpClientHelper
                .CreateToolListResponse(id, tools);
        }

        private async Task<string> HandleToolCall(
            string id, string toolName, string arguments)
        {
            switch (toolName)
            {
                case "scan_solution":
                    if (CommandHandler == null)
                    {
                        OpenToolWindow?.Invoke();
                        await Task.Delay(1500); // Wait for window to load and bind
                    }
                    
                    if (CommandHandler != null)
                    {
                        string scanResult =
                            await CommandHandler("scan");
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id, scanResult);
                    }
                    if (_lastReport != null)
                    {
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id,
                                _lastReport
                                    .ToMarkdownString());
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id,
                            "No scan data. Open a solution " +
                            "and run a scan first.");

                case "get_packages":
                    if (_lastReport?.Packages != null)
                    {
                        var sb = new StringBuilder();
                        foreach (var p in _lastReport.Packages)
                        {
                            sb.AppendLine(
                                $"Package: {p.Name}");
                            sb.AppendLine(
                                $"  Installed: {p.Version}" +
                                $"  Latest: " +
                                $"{p.LatestStableVersion}");
                            sb.AppendLine(
                                $"  Vulnerable: " +
                                $"{(p.IsVulnerable ? "Yes" : "No")}" +
                                $"  Deprecated: " +
                                $"{(p.IsDeprecated ? "Yes" : "No")}");
                            sb.AppendLine(
                                $"  Pre-Release: " +
                                $"{(p.IsPreRelease ? "Yes" : "No")}" +
                                $"  Status: {p.HealthLevel}");
                            sb.AppendLine(
                                $"  Recommendation: " +
                                $"{p.Recommendation}");
                            sb.AppendLine(
                                $"  Upgrade: " +
                                $"{p.UpgradeCommand}");
                            sb.AppendLine();
                        }
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id, sb.ToString());
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id, "Run scan_solution first.");

                case "get_health_score":
                    if (_lastReport != null)
                    {
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id,
                                $"Score: " +
                                $"{_lastReport.HealthScore}" +
                                $"/100\nStatus: " +
                                $"{_lastReport.HealthStatus}" +
                                $"\nErrors: " +
                                $"-{_lastReport.Errors.Count * 10}" +
                                $"pts\nWarnings: " +
                                $"-{_lastReport.Warnings.Count * 2}" +
                                $"pts\nTODOs: " +
                                $"-{_lastReport.Todos.Count}pts");
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id, "Run scan_solution first.");

                case "get_build_issues":
                    if (_lastReport != null)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(
                            $"Errors: " +
                            $"{_lastReport.Errors.Count}" +
                            $"  Warnings: " +
                            $"{_lastReport.Warnings.Count}");
                        foreach (var e in _lastReport.Errors)
                        {
                            sb.AppendLine(
                                $"[Error] {e.FilePath} " +
                                $"line {e.LineNumber}: " +
                                $"{e.RawMessage}");
                            sb.AppendLine(
                                $"  -> {e.HumanizedMessage}");
                        }
                        foreach (var w in _lastReport.Warnings)
                        {
                            sb.AppendLine(
                                $"[Warning] {w.FilePath} " +
                                $"line {w.LineNumber}: " +
                                $"{w.RawMessage}");
                            sb.AppendLine(
                                $"  -> {w.HumanizedMessage}");
                        }
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id, sb.ToString());
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id, "Run scan_solution first.");

                case "get_todos":
                    if (_lastReport?.Todos != null)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine(
                            $"Total: " +
                            $"{_lastReport.Todos.Count}");
                        foreach (var t in _lastReport.Todos)
                        {
                            sb.AppendLine(
                                $"[{t.Type}] {t.FileName} " +
                                $"line {t.LineNumber}: " +
                                $"{t.Text}");
                        }
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id, sb.ToString());
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id, "Run scan_solution first.");

                case "get_ai_insights":
                    if (_lastReport?.AiInsights != null)
                    {
                        var ai = _lastReport.AiInsights;
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id,
                                $"Build Analysis:\n" +
                                $"{ai.BuildAnalysis}\n\n" +
                                $"Optimization:\n" +
                                $"{ai.OptimizationSuggestions}" +
                                $"\n\nPackages:\n" +
                                $"{ai.PackageRecommendations}" +
                                $"\n\nOverall:\n" +
                                $"{ai.OverallFeedback}");
                    }
                    return McpClientHelper
                        .CreateToolCallTextResponse(
                            id, "Run scan_solution first.");

                case "optimize_code":
                    try
                    {
                        string code = "";
                        if (!string.IsNullOrEmpty(arguments))
                        {
                            using var argsDoc =
                                JsonDocument.Parse(arguments);
                            if (argsDoc.RootElement
                                .TryGetProperty(
                                    "code", out var c))
                            {
                                code = c.GetString() ?? "";
                            }
                        }
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            return McpClientHelper
                                .CreateToolCallTextResponse(
                                    id,
                                    "Provide 'code' argument.");
                        }
                        var aiSvc = new ClaudeAiService();
                        string opt = await aiSvc
                            .OptimizeCodeAsync(code);
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id, opt);
                    }
                    catch (Exception ex)
                    {
                        return McpClientHelper
                            .CreateToolCallTextResponse(
                                id,
                                "Error: " + ex.Message);
                    }

                default:
                    return McpClientHelper
                        .CreateErrorResponse(
                            id, -32602,
                            "Unknown tool: " + toolName);
            }
        }
    }
}