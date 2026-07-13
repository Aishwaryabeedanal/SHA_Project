using SHA_Project.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using EnvDTE;
using EnvDTE80;

namespace SHA_Project.Services
{
    public class NuGetInspectionService
    {
        private static readonly HttpClient _http =
            new HttpClient();

        public async Task<List<PackageHealthInfo>>
            GetPackagesAsync(string solutionPath)
        {
            List<PackageHealthInfo> packages =
                new List<PackageHealthInfo>();

            var seen = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

            // METHOD 1 — IVsPackageInstallerServices
            try
            {
                await ThreadHelper.JoinableTaskFactory
                    .SwitchToMainThreadAsync();

                var installerServices =
                    Package.GetGlobalService(
                        typeof(SVsServiceProvider))
                    as IVsPackageInstallerServices;

                var dte = Package.GetGlobalService(
                    typeof(DTE)) as DTE2;

                if (installerServices != null &&
                    dte?.Solution != null)
                {
                    foreach (Project project in
                        dte.Solution.Projects)
                    {
                        try
                        {
                            var installedPkgs =
                                installerServices
                                    .GetInstalledPackages(
                                        project);

                            if (installedPkgs == null)
                                continue;

                            foreach (var pkg in
                                installedPkgs)
                            {
                                if (!seen.Add(pkg.Id))
                                    continue;

                                var info =
                                    await BuildPackageInfoAsync(
                                        pkg.Id,
                                        pkg.VersionString
                                        ?? "Unknown");

                                packages.Add(info);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // METHOD 2 — csproj PackageReference
            try
            {
                var projectFiles =
                    Directory.GetFiles(
                        solutionPath,
                        "*.csproj",
                        SearchOption.AllDirectories);

                foreach (var file in projectFiles)
                {
                    try
                    {
                        XDocument doc =
                            XDocument.Load(file);

                        var refs = doc.Descendants()
                            .Where(x =>
                                x.Name.LocalName ==
                                "PackageReference");

                        foreach (var r in refs)
                        {
                            string name =
                                r.Attribute("Include")?.Value
                                ?? "Unknown";

                            string version =
                                r.Attribute("Version")?.Value
                                ?? r.Elements()
                                    .FirstOrDefault(x =>
                                        x.Name.LocalName ==
                                        "Version")
                                    ?.Value
                                ?? "Unknown";

                            var existing =
                                packages.FirstOrDefault(
                                    p => p.Name.Equals(
                                        name,
                                        System.StringComparison
                                            .OrdinalIgnoreCase));

                            if (existing != null &&
                                existing.Version == "Unknown"
                                && version != "Unknown")
                            {
                                packages.Remove(existing);
                                seen.Remove(name);
                                var updated =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(updated);
                                seen.Add(name);
                            }
                            else if (!seen.Add(name))
                                continue;
                            else
                            {
                                var info =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(info);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // METHOD 3 — Directory.Packages.props
            // WITH variable resolution
            try
            {
                var propsFiles = Directory.GetFiles(
                    solutionPath,
                    "Directory.Packages.props",
                    SearchOption.AllDirectories);

                foreach (var propsFile in propsFiles)
                {
                    try
                    {
                        XDocument doc =
                            XDocument.Load(propsFile);

                        // Read variable definitions
                        var variables =
                            new Dictionary<string, string>(
                                System.StringComparer
                                    .OrdinalIgnoreCase);

                        foreach (var pg in
                            doc.Descendants()
                            .Where(x =>
                                x.Name.LocalName ==
                                "PropertyGroup"))
                        {
                            foreach (var prop in
                                pg.Elements())
                            {
                                string varName =
                                    prop.Name.LocalName;
                                string varValue =
                                    prop.Value?.Trim()
                                    ?? "";

                                if (!string.IsNullOrEmpty(
                                    varValue))
                                {
                                    variables[varName] =
                                        varValue;
                                }
                            }
                        }

                        // Read PackageVersion elements
                        var refs = doc.Descendants()
                            .Where(x =>
                                x.Name.LocalName ==
                                "PackageVersion");

                        foreach (var r in refs)
                        {
                            string name =
                                r.Attribute("Include")?.Value
                                ?? "Unknown";

                            string version =
                                r.Attribute("Version")?.Value
                                ?? "Unknown";

                            // Resolve $(VariableName)
                            if (version.StartsWith("$(") &&
                                version.EndsWith(")"))
                            {
                                string varName =
                                    version.Substring(2,
                                        version.Length - 3);

                                if (variables.TryGetValue(
                                    varName,
                                    out string resolved))
                                {
                                    version = resolved;
                                }
                            }

                            var existing =
                                packages.FirstOrDefault(
                                    p => p.Name.Equals(
                                        name,
                                        System.StringComparison
                                            .OrdinalIgnoreCase));

                            if (existing != null &&
                                existing.Version == "Unknown"
                                && version != "Unknown")
                            {
                                packages.Remove(existing);
                                seen.Remove(name);
                                var updated =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(updated);
                                seen.Add(name);
                            }
                            else if (!seen.Add(name))
                                continue;
                            else
                            {
                                var info =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(info);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // METHOD 4 — packages.config
            try
            {
                var configFiles = Directory.GetFiles(
                    solutionPath,
                    "packages.config",
                    SearchOption.AllDirectories);

                foreach (var configFile in configFiles)
                {
                    try
                    {
                        XDocument doc =
                            XDocument.Load(configFile);

                        var refs = doc.Descendants()
                            .Where(x =>
                                x.Name.LocalName ==
                                "package");

                        foreach (var r in refs)
                        {
                            string name =
                                r.Attribute("id")?.Value
                                ?? "Unknown";

                            string version =
                                r.Attribute("version")?.Value
                                ?? "Unknown";

                            var existing =
                                packages.FirstOrDefault(
                                    p => p.Name.Equals(
                                        name,
                                        System.StringComparison
                                            .OrdinalIgnoreCase));

                            if (existing != null &&
                                existing.Version == "Unknown"
                                && version != "Unknown")
                            {
                                packages.Remove(existing);
                                seen.Remove(name);
                                var updated =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(updated);
                                seen.Add(name);
                            }
                            else if (!seen.Add(name))
                                continue;
                            else
                            {
                                var info =
                                    await BuildPackageInfoAsync(
                                        name, version);
                                packages.Add(info);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return packages;
        }

        private async Task<PackageHealthInfo>
            BuildPackageInfoAsync(
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
                string flatUrl =
                    "https://api.nuget.org/" +
                    "v3-flatcontainer/" +
                    $"{name.ToLower()}/index.json";

                var response =
                    await _http.GetAsync(flatUrl);

                if (response.IsSuccessStatusCode)
                {
                    string flatJson =
                        await response.Content
                            .ReadAsStringAsync();

                    using var flatDoc =
                        JsonDocument.Parse(flatJson);

                    var versions =
                        flatDoc.RootElement
                            .GetProperty("versions");

                    for (int i =
                        versions.GetArrayLength() - 1;
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

                if (!isVulnerable &&
                    !isDeprecated &&
                    !isPreRelease &&
                    latestStable != version &&
                    latestStable != "")
                {
                    healthLevel = "Update Available";
                    recommendation =
                        $"Version {latestStable} available. " +
                        $"Run: dotnet add package " +
                        $"{name} --version {latestStable}";
                }
            }
            catch (System.Exception ex)
            {
                latestStable =
                    "Unknown (Error: " + ex.Message + ")";
            }

            if (isPreRelease &&
                !isVulnerable &&
                !isDeprecated)
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