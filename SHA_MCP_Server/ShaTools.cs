using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SHA_MCP_Server
{
    public class ShaTools
    {
        private readonly string _path;
        private static readonly HttpClient _http =
            new HttpClient();
        private ScanResult? _lastScan;

        public ShaTools(string path) { _path = path; }

        public string HandleToolsList(string id)
        {
            var tools = new object[]
            {
                new
                {
                    name = "scan_solution",
                    description = "Full health scan.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "get_packages",
                    description =
                        "NuGet package details.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "get_health_score",
                    description =
                        "Health score and status.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "get_build_issues",
                    description =
                        "Build errors/warnings.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "get_todos",
                    description =
                        "TODO/FIXME comments.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "get_ai_insights",
                    description =
                        "AI recommendations.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                },
                new
                {
                    name = "optimize_code",
                    description =
                        "AI code optimization.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            code = new
                            {
                                type = "string"
                            }
                        },
                        required = new[] { "code" }
                    }
                }
            };

            return JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new { tools }
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        public async Task<string> HandleToolCall(
            string id, string toolName, string arguments)
        {
            if (_lastScan == null &&
                toolName != "optimize_code")
            {
                _lastScan = await RunScan();
            }

            switch (toolName)
            {
                case "scan_solution":
                    _lastScan = await RunScan();
                    return Respond(id,
                        _lastScan.FullReport());

                case "get_packages":
                    return Respond(id,
                        _lastScan.PackageReport());

                case "get_health_score":
                    return Respond(id,
                        _lastScan.ScoreReport());

                case "get_build_issues":
                    return Respond(id,
                        "Build analysis requires VS. " +
                        "Use the VSIX extension.");

                case "get_todos":
                    return Respond(id,
                        _lastScan.TodoReport());

                case "get_ai_insights":
                    return Respond(id,
                        _lastScan.AiInsight);

                case "optimize_code":
                    string code = "";
                    try
                    {
                        using var d = JsonDocument
                            .Parse(arguments);
                        code = d.RootElement
                            .GetProperty("code")
                            .GetString() ?? "";
                    }
                    catch { }

                    if (string.IsNullOrEmpty(code))
                        return Respond(id,
                            "Provide 'code' argument.");

                    string key = Environment
                        .GetEnvironmentVariable(
                            "GEMINI_API_KEY") ?? "";

                    return Respond(id,
                        string.IsNullOrEmpty(key)
                            ? "Set GEMINI_API_KEY."
                            : await CallGemini(key,
                                "Optimize this .NET code " +
                                "(5-8 sentences, no " +
                                "markdown):\n" + code));

                default:
                    return JsonSerializer.Serialize(
                        new
                        {
                            jsonrpc = "2.0",
                            id,
                            error = new
                            {
                                code = -32602,
                                message =
                                    $"Unknown tool: " +
                                    $"{toolName}"
                            }
                        });
            }
        }

        private async Task<ScanResult> RunScan()
        {
            var r = new ScanResult
            {
                Todos = ScanTodos(),
                Packages = await ScanPackages()
            };

            r.Calculate();

            string key = Environment
                .GetEnvironmentVariable(
                    "GEMINI_API_KEY") ?? "";

            if (!string.IsNullOrEmpty(key))
            {
                r.AiInsight = await CallGemini(key,
                    $"Analyze: {r.Todos.Count} TODOs, " +
                    $"{r.Packages.Count} packages, " +
                    $"score {r.Score}/100. " +
                    $"3-5 sentences. No markdown.");
            }

            return r;
        }

        private List<TodoEntry> ScanTodos()
        {
            var todos = new List<TodoEntry>();
            if (!Directory.Exists(_path)) return todos;

            var skip = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                "bin", "obj", "node_modules",
                ".git", ".vs"
            };

            var exts = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".vb", ".ts", ".js", ".razor"
            };

            var rx = new Regex(
                @"//\s*(TODO|FIXME)\s*:?\s*(.*)",
                RegexOptions.IgnoreCase);

            foreach (var f in Directory.EnumerateFiles(
                _path, "*.*",
                SearchOption.AllDirectories))
            {
                try
                {
                    if (skip.Any(s => f.Contains(
                        Path.DirectorySeparatorChar +
                        s +
                        Path.DirectorySeparatorChar)))
                        continue;

                    if (!exts.Contains(
                        Path.GetExtension(f)))
                        continue;

                    var lines = File.ReadAllLines(f);
                    string rel =
                        Path.GetRelativePath(_path, f);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var m = rx.Match(lines[i]);
                        if (m.Success)
                        {
                            todos.Add(new TodoEntry
                            {
                                File = rel,
                                Line = i + 1,
                                Type = m.Groups[1].Value
                                    .ToUpper(),
                                Text = m.Groups[2].Value
                                    .Trim()
                            });
                        }
                    }
                }
                catch { }
            }

            return todos;
        }

        private async Task<List<PkgEntry>> ScanPackages()
        {
            var pkgs = new List<PkgEntry>();
            var seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var f in Directory.EnumerateFiles(
                _path, "*.csproj",
                SearchOption.AllDirectories))
            {
                try
                {
                    var xdoc = XDocument.Load(f);
                    foreach (var pr in xdoc.Descendants()
                        .Where(x => x.Name.LocalName ==
                            "PackageReference"))
                    {
                        string name = pr.Attribute(
                            "Include")?.Value ?? "";
                        string ver = pr.Attribute(
                            "Version")?.Value ?? "Unknown";

                        if (!string.IsNullOrEmpty(name) &&
                            seen.Add(name))
                        {
                            pkgs.Add(await BuildPkg(
                                name, ver));
                        }
                    }
                }
                catch { }
            }

            return pkgs;
        }

        private async Task<PkgEntry> BuildPkg(
            string name, string ver)
        {
            var p = new PkgEntry
            {
                Name = name,
                Version = ver,
                Latest = ver,
                Status = "Healthy"
            };

            try
            {
                var r = await _http.GetAsync(
                    "https://api.nuget.org/" +
                    "v3-flatcontainer/" +
                    $"{name.ToLower()}/index.json");

                if (r.IsSuccessStatusCode)
                {
                    using var d = JsonDocument.Parse(
                        await r.Content
                            .ReadAsStringAsync());

                    var vs = d.RootElement
                        .GetProperty("versions");

                    for (int i = vs.GetArrayLength() - 1;
                        i >= 0; i--)
                    {
                        string v = vs[i].GetString() ?? "";
                        if (!v.Contains("-"))
                        {
                            p.Latest = v;
                            break;
                        }
                    }
                }

                p.IsPreRelease = ver.Contains("-");
                p.IsNew = !string.Equals(
                    ver, p.Latest,
                    StringComparison.OrdinalIgnoreCase);
                p.Status = p.IsPreRelease
                    ? "Pre-Release"
                    : p.IsNew
                        ? "Outdated"
                        : "Healthy";
            }
            catch { }

            p.Cmd = $"dotnet add package {name} " +
                    $"--version {p.Latest}";
            p.Rec = p.Status == "Healthy"
                ? "Up to date."
                : $"Update: {p.Cmd}";

            return p;
        }

        private async Task<string> CallGemini(
            string key, string prompt)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
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
                        maxOutputTokens = 2000
                    }
                });

                var r = await _http.PostAsync(
                    "https://generativelanguage" +
                    ".googleapis.com/v1beta/" +
                    "models/gemini-2.0-flash:" +
                    $"generateContent?key={key}",
                    new StringContent(
                        body, Encoding.UTF8,
                        "application/json"));

                using var d = JsonDocument.Parse(
                    await r.Content
                        .ReadAsStringAsync());

                return d.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "No insight.";
            }
            catch (Exception ex)
            {
                return $"AI Error: {ex.Message}";
            }
        }

        private string Respond(string id, string text)
        {
            return JsonSerializer.Serialize(
                new
                {
                    jsonrpc = "2.0",
                    id,
                    result = new
                    {
                        content = new[]
                        {
                            new { type = "text", text }
                        }
                    }
                },
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }
    }

    // --- Internal models ---

    public class TodoEntry
    {
        public string File { get; set; } = "";
        public int Line { get; set; }
        public string Type { get; set; } = "TODO";
        public string Text { get; set; } = "";
    }

    public class PkgEntry
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Latest { get; set; } = "";
        public bool IsPreRelease { get; set; }
        public bool IsNew { get; set; }
        public string Status { get; set; } = "";
        public string Rec { get; set; } = "";
        public string Cmd { get; set; } = "";
    }

    public class ScanResult
    {
        public List<TodoEntry> Todos { get; set; } =
            new List<TodoEntry>();
        public List<PkgEntry> Packages { get; set; } =
            new List<PkgEntry>();
        public int Score { get; set; } = 100;
        public string Status { get; set; } = "Excellent";
        public string AiInsight { get; set; } = "";

        public void Calculate()
        {
            int s = 100;
            s -= Todos.Count;
            s -= Packages.Count(p => p.IsPreRelease) * 5;
            Score = Math.Max(0, s);

            if (Score >= 90) Status = "Excellent";
            else if (Score >= 75) Status = "Good";
            else if (Score >= 50) Status = "Needs Attention";
            else Status = "Critical";
        }

        public string FullReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                $"Score: {Score}/100 ({Status})");
            sb.AppendLine(
                $"TODOs: {Todos.Count}  " +
                $"Packages: {Packages.Count}");
            sb.AppendLine();

            foreach (var t in Todos)
                sb.AppendLine(
                    $"[{t.Type}] {t.File}:{t.Line} " +
                    $"{t.Text}");

            sb.AppendLine();

            foreach (var p in Packages)
                sb.AppendLine(
                    $"{p.Name} v{p.Version} -> " +
                    $"{p.Latest} [{p.Status}]");

            if (!string.IsNullOrEmpty(AiInsight))
            {
                sb.AppendLine();
                sb.AppendLine(AiInsight);
            }

            return sb.ToString();
        }

        public string PackageReport()
        {
            var sb = new StringBuilder();
            foreach (var p in Packages)
                sb.AppendLine(
                    $"{p.Name}: {p.Version} -> " +
                    $"{p.Latest} [{p.Status}] {p.Rec}");
            return sb.ToString();
        }

        public string ScoreReport()
        {
            return $"Score: {Score}/100\n" +
                   $"Status: {Status}";
        }

        public string TodoReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Total: {Todos.Count}");
            foreach (var t in Todos)
                sb.AppendLine(
                    $"[{t.Type}] {t.File}:{t.Line} " +
                    $"{t.Text}");
            return sb.ToString();
        }
    }
}
