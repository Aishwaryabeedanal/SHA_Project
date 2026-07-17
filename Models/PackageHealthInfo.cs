using System;

namespace SHA_Project.Models
{
    public class PackageHealthInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string LatestStableVersion { get; set; }
        public bool IsPreRelease { get; set; }
        public bool IsVulnerable { get; set; }
        public bool IsDeprecated { get; set; }
        public bool IsNewVersionAvailable { get; set; }
        public string HealthLevel { get; set; }
        public string Status { get; set; }
        public string Recommendation { get; set; }
        public string AiInsight { get; set; } = "";
        public string UpgradeCommand { get; set; } = "";
    }
}
