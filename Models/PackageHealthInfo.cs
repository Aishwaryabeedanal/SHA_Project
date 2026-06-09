using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHA_Project.Models
{
    public class PackageHealthInfo
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string LatestStableVersion { get; set; }

        public bool IsPreRelease { get; set; }

        public string Status { get; set; }

        public bool IsDeprecated { get; set; }

        public bool IsVulnerable { get; set; }

        public string Recommendation { get; set; }

        public string HealthLevel { get; set; }
    }
}
