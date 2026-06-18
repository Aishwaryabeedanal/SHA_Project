using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    public class McpClientHelper
    {
        private static readonly HttpClient _http =
            new HttpClient();

        public static async Task SendCommandAsync(
            string command)
        {
            try
            {
                await _http.PostAsync(
                    "http://localhost:5010/mcp/",
                    new StringContent(
                        command,
                        Encoding.UTF8,
                        "text/plain"));

                Console.WriteLine(
                    $"✓ Command '{command}' sent!");
            }
            catch
            {
                Console.WriteLine(
                    "✗ Could not connect. " +
                    "Is Visual Studio open?");
            }
        }
    }
}
