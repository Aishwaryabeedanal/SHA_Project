using SHA_Project.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace SHA_Project.Services
{
    public class NuGetInspectionService
    {
        private static readonly HttpClient _http = new HttpClient();

        // Call this from your scan button instead of GetPackages
        public async Task<List<PackageHealthInfo>> GetPackagesAsync(
            string solutionPath)
        {
            List<PackageHealthInfo> packages =
                new List<PackageHealthInfo>();

            // Read all .csproj files
            var projectFiles =
                Directory.GetFiles(
                    solutionPath,
                    "*.csproj",
                    SearchOption.AllDirectories);

            var seen = new HashSet<string>();

            foreach (var file in projectFiles)
            {
                try
                {
                    XDocument doc = XDocument.Load(file);

                    var refs = doc.Descendants()
                        .Where(x =>
                            x.Name.LocalName == "PackageReference");

                    foreach (var r in refs)
                    {
                        string name =
                            r.Attribute("Include")?.Value ?? "Unknown";

                        string version =
                            r.Attribute("Version")?.Value
                            ?? r.Elements()
                                .FirstOrDefault(x =>
                                    x.Name.LocalName == "Version")
                                ?.Value
                            ?? "Unknown";

                        if (!seen.Add(name)) continue;

                        // Call real NuGet API for latest version
                        var info = await BuildPackageInfoAsync(
                            name, version);

                        packages.Add(info);
                    }
                }
                catch { }
            }

            return packages;
        }

        private async Task<PackageHealthInfo> BuildPackageInfoAsync(
    string name, string version)
        {
            bool isPreRelease = version.Contains("-");
            bool isVulnerable = false;
            bool isDeprecated = false;
            string healthLevel = "Healthy";
            string recommendation = "Package looks healthy.";
            string latestStable = version;

            try
            {
                // Flat container API — simple and always works
                string flatUrl =
                    $"https://api.nuget.org/v3-flatcontainer/" +
                    $"{name.ToLower()}/index.json";

                var response = await _http.GetAsync(flatUrl);

                if (response.IsSuccessStatusCode)
                {
                    string flatJson =
                        await response.Content.ReadAsStringAsync();

                    using var flatDoc =
                        JsonDocument.Parse(flatJson);

                    var versions = flatDoc.RootElement
                        .GetProperty("versions");

                    // Walk backwards to find latest stable
                    for (int i = versions.GetArrayLength() - 1;
                         i >= 0; i--)
                    {
                        string v =
                            versions[i].GetString() ?? "";
                        if (!v.Contains("-"))
                        {
                            latestStable = v;
                            break;
                        }
                    }
                }

                // Compare versions to set status
                if (latestStable != version &&
                    latestStable != "" &&
                    !isPreRelease)
                {
                    healthLevel = "Update Available";
                    recommendation =
                        $"Version {latestStable} is available. " +
                        $"Run: dotnet add package {name} " +
                        $"--version {latestStable}";
                }
            }
            catch (System.Exception ex)
            {
                latestStable =
                    "Unknown (Error: " + ex.Message + ")";
            }

            // Pre-release overrides update available
            if (isPreRelease && !isVulnerable)
            {
                healthLevel = "Needs Upgrade";
                recommendation =
                    $"Pre-release installed. " +
                    $"Switch to stable: {latestStable}";
            }

            return new PackageHealthInfo
            {
                Name = name,
                Version = version,
                LatestStableVersion = latestStable,
                IsPreRelease = isPreRelease,
                IsDeprecated = isDeprecated,
                IsVulnerable = isVulnerable,
                HealthLevel = healthLevel,
                Status = healthLevel,
                Recommendation = recommendation
            };
        }
    }
}