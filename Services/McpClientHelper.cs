using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    /// <summary>
    /// Helper for constructing MCP JSON-RPC responses
    /// and tool definitions.
    /// </summary>
    public static class McpClientHelper
    {
        private static readonly HttpClient _http =
            new HttpClient();

        public static object CreateToolDef(
            string name,
            string description,
            object inputSchema)
        {
            return new { name, description, inputSchema };
        }

        public static string CreateToolListResponse(
            string id, object[] tools)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result = new { tools }
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public static string CreateToolCallTextResponse(
            string id, string text)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                result = new
                {
                    content = new[]
                    {
                        new { type = "text", text }
                    }
                }
            }, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public static string CreateErrorResponse(
            string id, int code, string message)
        {
            return JsonSerializer.Serialize(new
            {
                jsonrpc = "2.0",
                id,
                error = new { code, message }
            });
        }

        public static async Task SendCommandAsync(
            string command)
        {
            try
            {
                var jsonRpc = JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    method = "tools/call",
                    @params = new
                    {
                        name = command,
                        arguments = new { }
                    }
                });

                await _http.PostAsync(
                    "http://localhost:5010/mcp/",
                    new StringContent(
                        jsonRpc, Encoding.UTF8,
                        "application/json"));
            }
            catch { }
        }
    }
}
