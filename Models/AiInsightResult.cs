using System.Collections.Generic;

namespace SHA_Project.Models
{
    public class AiInsightResult
    {
        public string BuildAnalysis { get; set; } = "";
        public string OptimizationSuggestions { get; set; } = "";
        public string PackageRecommendations { get; set; } = "";
        public string OverallFeedback { get; set; } = "";
        public Dictionary<string, string> PerPackageInsights { get; set; } = new Dictionary<string, string>();
    }
}
