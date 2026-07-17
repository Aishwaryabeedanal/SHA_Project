using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SHA_MCP_Server
{
    class Program
    {
        private static ShaTools _tools = null!;
        private static CancellationTokenSource _cts = null!;

        static async Task Main(string[] args)
        {
            string solutionPath = Directory.GetCurrentDirectory();

            // Check for --stdio flag or solution path arg
            bool stdioMode = false;
            foreach (var arg in args)
            {
                if (arg == "--stdio" || arg == "-s")
                {
                    stdioMode = true;
                }
                else if (!arg.StartsWith("-"))
                {
                    solutionPath = arg;
                }
            }

            _tools = new ShaTools(solutionPath);
            _cts = new CancellationTokenSource();

            if (stdioMode)
            {
                // STDIO mode for Copilot Chat MCP
                await RunStdioMode();
            }
            else
            {
                // HTTP mode for manual/testing use
                await RunHttpMode(solutionPath);
            }
        }

        /// <summary>
        /// STDIO mode: reads JSON-RPC from stdin, forwards to VSIX HTTP server, writes to stdout.
        /// This bridges Copilot Chat to the Visual Studio extension.
        /// </summary>
        private static async Task RunStdioMode()
        {
            using var reader = new StreamReader(
                Console.OpenStandardInput(), Encoding.UTF8);
            using var writer = new StreamWriter(
                Console.OpenStandardOutput(), Encoding.UTF8)
            {
                AutoFlush = true
            };
            
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            while (!_cts.IsCancellationRequested)
            {
                string? line = null;
                try
                {
                    line = await reader.ReadLineAsync();
                }
                catch { break; }

                if (line == null) break; // EOF
                if (string.IsNullOrWhiteSpace(line)) continue;

                string responseStr;
                try
                {
                    // Special case for initialize handshake which the proxy can handle itself
                    if (line.Contains("\"method\":\"initialize\""))
                    {
                        var doc = JsonDocument.Parse(line);
                        string id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : "null";
                        
                        responseStr = JsonSerializer.Serialize(new
                        {
                            jsonrpc = "2.0",
                            id,
                            result = new
                            {
                                protocolVersion = "2024-11-05",
                                capabilities = new { tools = new { listChanged = false } },
                                serverInfo = new { name = "Solution Health Analyzer", version = "1.0.0" }
                            }
                        });
                    }
                    else if (line.Contains("\"method\":\"initialized\""))
                    {
                        continue; // No response needed
                    }
                    else
                    {
                        // Forward to VSIX
                        var content = new StringContent(line, Encoding.UTF8, "application/json");
                        var httpRes = await httpClient.PostAsync("http://localhost:5010/mcp/", content);
                        responseStr = await httpRes.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception ex)
                {
                    // If VSIX is not running or other error
                    using var doc = JsonDocument.Parse(line);
                    string id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.ToString() : "null";
                    
                    responseStr = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id,
                        error = new
                        {
                            code = -32603,
                            message = $"Failed to connect to Solution Health Analyzer. Is Visual Studio open? Error: {ex.Message}"
                        }
                    });
                }

                await writer.WriteLineAsync(responseStr);
            }
        }

        /// <summary>
        /// HTTP mode: listens on port 5010 for testing.
        /// </summary>
        private static async Task RunHttpMode(
            string solutionPath)
        {
            Console.WriteLine(
                "========================================");
            Console.WriteLine(
                "  Solution Health Analyzer MCP Server");
            Console.WriteLine(
                "========================================");
            Console.WriteLine(
                $"Solution path: {solutionPath}");
            Console.WriteLine(
                "Starting on http://localhost:5010/mcp/");
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _cts.Cancel();
            };

            using var listener = new HttpListener();
            listener.Prefixes.Add(
                "http://localhost:5010/mcp/");

            try
            {
                listener.Start();
                Console.WriteLine(
                    "Server started. Listening...");

                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var ctx = await listener
                            .GetContextAsync();
                        _ = Task.Run(
                            () => HandleHttpRequest(ctx));
                    }
                    catch (Exception)
                        when (_cts.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                listener.Stop();
                Console.WriteLine("Server stopped.");
            }
        }

        private static async Task<string> ProcessJsonRpc(
            string body)
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string method = root.TryGetProperty(
                "method", out var m)
                ? m.GetString() ?? "" : "";

            string id = root.TryGetProperty(
                "id", out var idEl)
                ? idEl.ToString() : "null";

            // Handle initialize (MCP handshake)
            if (method == "initialize")
            {
                return JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new
                            {
                                listChanged = false
                            }
                        },
                        serverInfo = new
                        {
                            name =
                                "Solution Health Analyzer",
                            version = "1.0.0"
                        }
                    }
                });
            }

            // Handle initialized notification
            if (method == "initialized")
            {
                // This is a notification, no response
                return "";
            }

            if (method == "tools/list")
            {
                return _tools.HandleToolsList(id);
            }
            else if (method == "tools/call")
            {
                var p = root.TryGetProperty(
                    "params", out var pe)
                    ? pe : default;

                string toolName = "";
                if (p.ValueKind !=
                    JsonValueKind.Undefined &&
                    p.TryGetProperty("name", out var n))
                {
                    toolName = n.GetString() ?? "";
                }

                string arguments = "";
                if (p.ValueKind !=
                    JsonValueKind.Undefined &&
                    p.TryGetProperty(
                        "arguments", out var a))
                {
                    arguments = a.ToString();
                }

                return await _tools.HandleToolCall(
                    id, toolName, arguments);
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id,
                    error = new
                    {
                        code = -32601,
                        message = "Method not found: " +
                                  method
                    }
                });
            }
        }

        private static async Task HandleHttpRequest(
            HttpListenerContext ctx)
        {
            string body;
            using (var reader = new StreamReader(
                ctx.Request.InputStream,
                ctx.Request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

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
                response = await ProcessJsonRpc(body);
            }
            catch (Exception ex)
            {
                response = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = "null",
                    error = new
                    {
                        code = -32603,
                        message = ex.Message
                    }
                });
            }

            byte[] buf = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = buf.Length;
            await ctx.Response.OutputStream
                .WriteAsync(buf, 0, buf.Length);
            ctx.Response.Close();
        }
    }
}
