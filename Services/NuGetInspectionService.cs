using SHA_Project.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SHA_Project.Services
{
    public class NuGetInspectionService
    {
        public List<PackageHealthInfo> GetPackages(string solutionPath)
        {
            List<PackageHealthInfo> packages =
                new List<PackageHealthInfo>();

            var projectFiles =
                Directory.GetFiles(
                    solutionPath,
                    "*.csproj",
                    SearchOption.AllDirectories);

            foreach (var file in projectFiles)
            {
                XDocument doc =
                    XDocument.Load(file);

                var packageReferences =
                    doc.Descendants()
                       .Where(x => x.Name.LocalName == "PackageReference");

                foreach (var package in packageReferences)
                {
                    string name =
                        package.Attribute("Include")?.Value ?? "Unknown";

                    string version =
                        package.Attribute("Version")?.Value;

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        version =
                            package.Elements()
                                   .FirstOrDefault(x => x.Name.LocalName == "Version")
                                   ?.Value;
                    }

                    version ??= "Unknown";

                    bool isPreRelease =
                        version.Contains("-");

                    bool isDeprecated = false;
                    bool isVulnerable = false;
                    string healthLevel = "Healthy";
                    string recommendation = "Package looks healthy.";

                    if (isPreRelease)
                    {
                        healthLevel = "Needs Upgrade";
                        recommendation = "Upgrade to latest stable version.";
                    }

                    // Example vulnerable package check
                    if (name.Contains("Newtonsoft.Json") &&
                        version.StartsWith("10"))
                    {
                        isVulnerable = true;
                        healthLevel = "Vulnerable";
                        recommendation = "Upgrade immediately. Known vulnerable version.";
                    }

                    packages.Add(
                    new PackageHealthInfo
                    {
                        Name = name,
                        Version = version,
                        LatestStableVersion =
                            isPreRelease ? "13.0.4" : version,

                        IsPreRelease = isPreRelease,
                        IsDeprecated = isDeprecated,
                        IsVulnerable = isVulnerable,

                        HealthLevel = healthLevel,

                        Status = healthLevel,

                        Recommendation = recommendation
                    });

                }
            }

            return packages;
        }
    }
} 