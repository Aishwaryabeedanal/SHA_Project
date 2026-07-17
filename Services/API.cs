using SHA_Project.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    /// <summary>
    /// Provides AI insights via Google Gemini API.
    /// Reads API key from GEMINI_API_KEY environment variable.
    /// </summary>
    public class ClaudeAiService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly string _apiKey;
        private const string Model = "gemini-2.0-flash";

        public ClaudeAiService()
        {
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        }

        private bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

        /// <summary>
        /// Gets AI insight for a single NuGet package.
        /// </summary>
        public async Task<string> GetInsightAsync(
            string packageName, string currentVersion,
            string latestVersion, string status)
        {
            if (!IsAvailable)
                return "AI insights unavailable — set GEMINI_API_KEY environment variable.";

            string prompt =
                $"You are a .NET developer assistant. " +
                $"A project has this NuGet package:\n" +
                $"Package: {packageName}\n" +
                $"Installed: {currentVersion}\n" +
                $"Latest: {latestVersion}\n" +
                $"Status: {status}\n\n" +
                $"Give a 2 sentence plain English " +
                $"recommendation. No markdown, no jargon.";

            return await CallGeminiAsync(prompt);
        }

        /// <summary>
        /// Gets comprehensive AI insights for the full health report.
        /// </summary>
        public async Task<AiInsightResult> GetFullInsightsAsync(HealthReport report)
        {
            var result = new AiInsightResult();

            if (!IsAvailable)
            {
                string msg = "AI insights unavailable — set GEMINI_API_KEY environment variable.";
                result.BuildAnalysis = msg;
                result.OptimizationSuggestions = msg;
                result.PackageRecommendations = msg;
                result.OverallFeedback = msg;
                return result;
            }

            // Build Analysis
            if (report.Errors.Count > 0 || report.Warnings.Count > 0)
            {
                var errorSummary = new StringBuilder();
                errorSummary.AppendLine("Analyze these build issues and provide actionable fixes:");
                foreach (var err in report.Errors.Take(10))
                {
                    errorSummary.AppendLine($"  ERROR {err.ErrorCode} in {err.FilePath} line {err.LineNumber}: {err.RawMessage}");
                }
                foreach (var warn in report.Warnings.Take(10))
                {
                    errorSummary.AppendLine($"  WARNING {warn.ErrorCode} in {warn.FilePath} line {warn.LineNumber}: {warn.RawMessage}");
                }
                errorSummary.AppendLine("\nProvide a brief analysis (3-5 sentences). No markdown.");

                result.BuildAnalysis = await CallGeminiAsync(errorSummary.ToString());
            }
            else
            {
                result.BuildAnalysis = "No build errors or warnings detected. The code compiles cleanly.";
            }

            // Package Recommendations
            if (report.Packages.Count > 0)
            {
                var pkgSummary = new StringBuilder();
                pkgSummary.AppendLine("Review these NuGet packages and suggest improvements:");
                foreach (var pkg in report.Packages.Take(15))
                {
                    pkgSummary.AppendLine($"  {pkg.Name} v{pkg.Version} (latest: {pkg.LatestStableVersion}, status: {pkg.HealthLevel})");
                }
                pkgSummary.AppendLine("\nProvide 3-5 sentences of recommendations. No markdown.");

                result.PackageRecommendations = await CallGeminiAsync(pkgSummary.ToString());
            }

            // Optimization Suggestions
            {
                string optPrompt =
                    $"A .NET solution has {report.Errors.Count} errors, " +
                    $"{report.Warnings.Count} warnings, {report.Todos.Count} TODOs, " +
                    $"and {report.Packages.Count} NuGet packages. " +
                    $"Health score is {report.HealthScore}/100 ({report.HealthStatus}). " +
                    $"Suggest 3-5 concrete optimization actions. No markdown.";

                result.OptimizationSuggestions = await CallGeminiAsync(optPrompt);
            }

            // Overall Feedback
            {
                string overallPrompt =
                    $"A .NET project has health score {report.HealthScore}/100. " +
                    $"Status: {report.HealthStatus}. " +
                    $"{report.Errors.Count} errors, {report.Warnings.Count} warnings, " +
                    $"{report.Todos.Count} TODOs. " +
                    $"Give an overall 2-3 sentence quality assessment. No markdown.";

                result.OverallFeedback = await CallGeminiAsync(overallPrompt);
            }

            // Per-package insights (limit to 10 to avoid API overuse)
            foreach (var pkg in report.Packages.Take(10))
            {
                try
                {
                    string insight = await GetInsightAsync(
                        pkg.Name, pkg.Version,
                        pkg.LatestStableVersion, pkg.HealthLevel);
                    result.PerPackageInsights[pkg.Name] = insight;
                }
                catch
                {
                    result.PerPackageInsights[pkg.Name] = "Insight unavailable.";
                }
            }

            return result;
        }

        /// <summary>
        /// AI-powered code optimization suggestions.
        /// </summary>
        public async Task<string> OptimizeCodeAsync(string codeSnippet)
        {
            if (!IsAvailable)
                return "AI insights unavailable — set GEMINI_API_KEY environment variable.";

            string prompt =
                "You are a senior .NET developer. Analyze this code and provide:\n" +
                "1. Performance optimizations\n" +
                "2. Code quality improvements\n" +
                "3. Best practice recommendations\n\n" +
                $"Code:\n{codeSnippet}\n\n" +
                "Keep response concise (5-8 sentences). No markdown.";

            return await CallGeminiAsync(prompt);
        }

        private async Task<string> CallGeminiAsync(string prompt)
        {
            try
            {
                string apiUrl =
                    $"https://generativelanguage.googleapis.com" +
                    $"/v1beta/models/{Model}:generateContent" +
                    $"?key={_apiKey}";

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
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = 2000,
                        temperature = 0.3
                    }
                };

                string json = JsonSerializer.Serialize(body);

                var request = new HttpRequestMessage(
                    HttpMethod.Post, apiUrl);
                request.Content = new StringContent(
                    json, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                string responseJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);

                if (doc.RootElement.TryGetProperty("error", out var err))
                {
                    string errMsg = err.TryGetProperty("message", out var m)
                        ? m.GetString() : "Unknown API error";
                    return $"AI Error: {errMsg}";
                }

                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()
                    ?? "No insight available.";
            }
            catch (TaskCanceledException)
            {
                return "AI request timed out. Try again later.";
            }
            catch (Exception ex)
            {
                return $"AI Error: {ex.Message}";
            }
        }
    }
}