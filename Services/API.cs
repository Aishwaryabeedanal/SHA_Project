using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    public class ClaudeAiService
    {
        private static readonly HttpClient _http =
            new HttpClient();

        private const string ApiKey =
            "AQ.Ab8RN6J1oiW8AhJSqoIMM01TK6aTaxqWewzTdIspvFVVOehKsg";

        public async Task<string> GetInsightAsync(
            string packageName,
            string currentVersion,
            string latestVersion,
            string status)
        {
            string prompt =
                $"You are a .NET developer assistant. " +
                $"A project has this NuGet package:\n" +
                $"Package: {packageName}\n" +
                $"Installed: {currentVersion}\n" +
                $"Latest: {latestVersion}\n" +
                $"Status: {status}\n\n" +
                $"Give a 2 sentence plain English " +
                $"recommendation. No markdown, no jargon.";

            string apiUrl =
                "https://generativelanguage.googleapis.com" +
                "/v1beta/models/gemini-1.5-flash:generateContent" +
                $"?key={ApiKey}";

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            string json =
                JsonSerializer.Serialize(body);

            var request = new HttpRequestMessage(
                HttpMethod.Post, apiUrl);

            request.Content = new StringContent(
                json, Encoding.UTF8, "application/json");

            var response =
                await _http.SendAsync(request);

            string responseJson =
                await response.Content
                    .ReadAsStringAsync();

            // Parse Gemini response safely
            try
            {
                using var doc =
                    JsonDocument.Parse(responseJson);

                // Check if error came back
                if (doc.RootElement
                    .TryGetProperty("error", out var err))
                {
                    string errMsg = err
                        .TryGetProperty("message", out var m)
                        ? m.GetString()
                        : "Unknown API error";
                    return $"AI Error: {errMsg}";
                }

                // Get the actual text response
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
                    ?? "No insight available.";
            }
            catch
            {
                // Return raw response so we can debug
                return $"Parse error. Response was: " +
                    responseJson.Substring(
                        0,
                        System.Math.Min(
                            200, responseJson.Length));
            }
        }
    }
}