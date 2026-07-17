using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SHA_Project.Models
{
    public class HealthReport
    {
        public List<BuildIssue> Errors { get; set; } = new List<BuildIssue>();
        public List<BuildIssue> Warnings { get; set; } = new List<BuildIssue>();
        public List<PackageHealthInfo> Packages { get; set; } = new List<PackageHealthInfo>();
        public List<TodoItem> Todos { get; set; } = new List<TodoItem>();
        public int HealthScore { get; set; } = 100;
        public string HealthStatus { get; set; } = "Excellent";
        public AiInsightResult AiInsights { get; set; } = new AiInsightResult();
        public DateTime ScannedAt { get; set; } = DateTime.Now;

        public void CalculateHealthScore()
        {
            int score = 100;
            score -= Errors.Count * 10;
            score -= Warnings.Count * 2;
            score -= Todos.Count * 1;
            score -= Packages.Count(p => p.IsPreRelease) * 5;
            HealthScore = Math.Max(0, score);

            if (HealthScore >= 90) HealthStatus = "Excellent";
            else if (HealthScore >= 75) HealthStatus = "Good";
            else if (HealthScore >= 50) HealthStatus = "Needs Attention";
            else HealthStatus = "Critical";
        }

        public string ToOutputPaneString()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("===== SOLUTION HEALTH ANALYZER =====");
            sb.AppendLine();

            // BUILD ISSUES
            sb.AppendLine("----- BUILD ISSUES -----");
            sb.AppendLine($"Errors: {Errors.Count}");
            sb.AppendLine($"Warnings: {Warnings.Count}");
            sb.AppendLine();

            foreach (var error in Errors)
            {
                sb.AppendLine($"[Error] {error.FilePath} line {error.LineNumber}: {error.RawMessage}");
                sb.AppendLine($"  → Humanized: {error.HumanizedMessage}");
            }

            foreach (var warning in Warnings)
            {
                sb.AppendLine($"[Warning] {warning.FilePath} line {warning.LineNumber}: {warning.RawMessage}");
                sb.AppendLine($"  → Humanized: {warning.HumanizedMessage}");
            }

            sb.AppendLine();

            // NUGET PACKAGES
            sb.AppendLine("----- NUGET PACKAGES -----");
            foreach (var p in Packages)
            {
                sb.AppendLine($"Package: {p.Name}");
                sb.AppendLine($"  Installed Version : {p.Version}");
                sb.AppendLine($"  Latest Stable     : {p.LatestStableVersion}");
                sb.AppendLine($"  Vulnerable        : {(p.IsVulnerable ? "Yes" : "No")}");
                sb.AppendLine($"  Deprecated        : {(p.IsDeprecated ? "Yes" : "No")}");
                sb.AppendLine($"  Pre-Release       : {(p.IsPreRelease ? "Yes" : "No")}");
                sb.AppendLine($"  New Version       : {(p.IsNewVersionAvailable ? "Available" : "Up to Date")}");
                sb.AppendLine($"  Status            : {p.HealthLevel}");
                sb.AppendLine($"  Recommendation    : {p.Recommendation}");
                sb.AppendLine($"  AI Insight        : {p.AiInsight}");
                sb.AppendLine($"  Upgrade Command   : dotnet add package {p.Name} --version {p.LatestStableVersion}");
                sb.AppendLine();
            }

            // TODO COMMENTS
            sb.AppendLine("----- TODO COMMENTS -----");
            sb.AppendLine($"Total TODOs: {Todos.Count}");
            foreach (var todo in Todos)
            {
                sb.AppendLine($"[{todo.Type}] {todo.FileName} line {todo.LineNumber}: {todo.Text}");
            }
            sb.AppendLine();

            // AI INSIGHTS
            sb.AppendLine("----- AI INSIGHTS -----");
            if (AiInsights != null)
            {
                if (!string.IsNullOrEmpty(AiInsights.BuildAnalysis))
                    sb.AppendLine(AiInsights.BuildAnalysis);
                if (!string.IsNullOrEmpty(AiInsights.OptimizationSuggestions))
                    sb.AppendLine(AiInsights.OptimizationSuggestions);
                if (!string.IsNullOrEmpty(AiInsights.PackageRecommendations))
                    sb.AppendLine(AiInsights.PackageRecommendations);
                if (!string.IsNullOrEmpty(AiInsights.OverallFeedback))
                    sb.AppendLine(AiInsights.OverallFeedback);
            }
            sb.AppendLine();

            // HEALTH SCORE
            sb.AppendLine("----- HEALTH SCORE -----");
            sb.AppendLine($"Score  : {HealthScore}/100");
            sb.AppendLine($"Status : {HealthStatus}");
            sb.AppendLine($"Errors   : -{Errors.Count * 10} points");
            sb.AppendLine($"Warnings : -{Warnings.Count * 2} points");
            sb.AppendLine($"TODOs    : -{Todos.Count} points");
            int preReleaseCount = Packages.Count(p => p.IsPreRelease);
            sb.AppendLine($"Pre-Release Packages: -{preReleaseCount * 5} points");
            sb.AppendLine();
            sb.AppendLine("====================================");

            return sb.ToString();
        }
    }
}
