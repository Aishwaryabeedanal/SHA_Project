using System.Collections.Generic;
using System.Text.Json;
using SHA_Project.Models;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using SHA_Project.Services;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace SHA_Project
{
    public partial class HealthDashboardToolWindowControl : UserControl
    {
        private SHA_Project.Services.McpServerService _mcpServer;
        private List<PackageHealthInfo> _currentPackages =
            new List<PackageHealthInfo>();

        // Store the latest scan results so MCP commands can read them
        private int _lastErrors;
        private int _lastWarnings;
        private int _lastTodoCount;
        private int _lastHealthScore;
        private string _lastHealthStatus = "Unknown";
        private string _lastRecommendation = "";
        private string _lastBuildIssues = "";

        public HealthDashboardToolWindowControl()
        {
            InitializeComponent();

            PackagesList.SelectionChanged +=
                PackagesList_SelectionChanged;

            Loaded += HealthDashboardToolWindowControl_Loaded;

            _mcpServer = new SHA_Project.Services.McpServerService();

            // Old-style commands (build/clean) still handled here
            _mcpServer.CommandReceived += async (body) =>
            {
                string cmd = body.ToLower()
                    .Replace("{", "").Replace("}", "")
                    .Replace("\"", "").Replace("command:", "")
                    .Trim();

                if (cmd.Contains("build"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand("Build.BuildSolution");
                    });
                }
                else if (cmd.Contains("clean"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand("Build.CleanSolution");
                    });
                }
            };

            // New-style: returns real JSON data back to Copilot
            _mcpServer.CommandHandler = async (body) =>
            {
                string cmd = body.ToLower()
                    .Replace("{", "").Replace("}", "")
                    .Replace("\"", "").Replace("command:", "")
                    .Trim();

                if (cmd.Contains("scan") || cmd.Contains("health"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await DoScanAsync();
                    });
                    return BuildScanResultJson();
                }
                else if (cmd.Contains("error"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        errors = _lastErrors,
                        details = _lastBuildIssues
                    });
                }
                else if (cmd.Contains("warning"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        warnings = _lastWarnings
                    });
                }
                else if (cmd.Contains("todo"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        todos = _lastTodoCount
                    });
                }
                else if (cmd.Contains("package"))
                {
                    return JsonSerializer.Serialize(_currentPackages);
                }
                else if (cmd.Contains("vulnerable"))
                {
                    var vulnerable = _currentPackages.FindAll(p => p.IsVulnerable);
                    return JsonSerializer.Serialize(vulnerable);
                }
                else if (cmd.Contains("score"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        score = _lastHealthScore,
                        status = _lastHealthStatus
                    });
                }

                return "{\"status\":\"ok\",\"message\":\"command not recognized\"}";
            };

            _mcpServer.Start();
        }

        private string BuildScanResultJson()
        {
            return JsonSerializer.Serialize(new
            {
                healthScore = _lastHealthScore,
                healthStatus = _lastHealthStatus,
                errors = _lastErrors,
                warnings = _lastWarnings,
                todos = _lastTodoCount,
                recommendation = _lastRecommendation,
                buildIssues = _lastBuildIssues,
                packages = _currentPackages
            });
        }

        private async void HealthDashboardToolWindowControl_Loaded(
            object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            await DoScanAsync();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            await DoScanAsync();
        }

        private async Task DoScanAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputPaneService.Initialize();
            OutputPaneService.Clear();

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;

            if (string.IsNullOrWhiteSpace(dte?.Solution?.FullName))
                return;

            string solutionPath =
                System.IO.Path.GetDirectoryName(dte.Solution.FullName);

            TodoScannerService todoService = new TodoScannerService();
            int todoCount = todoService.ScanTodos(solutionPath);

            RoslynAnalysisService roslynService = new RoslynAnalysisService();
            var result = await roslynService.AnalyzeSolutionAsync();

            ErrorsText.Text = $"Errors: {result.errors}";
            WarningsText.Text = $"Warnings: {result.warnings}";

            ErrorHumanizer humanizer = new ErrorHumanizer();
            BuildIssuesText.Text = humanizer.GetFriendlyMessage(result.errorDetails);

            TodoText.Text = $"TODOs: {todoCount}";

            NuGetInspectionService nugetService = new NuGetInspectionService();
            var packages = await nugetService.GetPackagesAsync(solutionPath);
            _currentPackages = packages;

            int healthScore = 100;
            healthScore -= result.errors * 10;
            healthScore -= result.warnings * 2;
            healthScore -= todoCount;

            foreach (var package in packages)
            {
                if (package.IsPreRelease)
                    healthScore -= 5;
            }

            if (healthScore < 0) healthScore = 0;

            string healthStatus;
            if (healthScore >= 90) healthStatus = "Excellent";
            else if (healthScore >= 75) healthStatus = "Good";
            else if (healthScore >= 50) healthStatus = "Needs Attention";
            else healthStatus = "Critical";

            ScoreText.Text = $"Health Score: {healthScore} ({healthStatus})";

            if (healthScore >= 90)
            {
                ScoreText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(78, 201, 78));
                ScoreBorderColor.Color = System.Windows.Media.Color.FromRgb(30, 58, 30);
            }
            else if (healthScore >= 50)
            {
                ScoreText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 165, 0));
                ScoreBorderColor.Color = System.Windows.Media.Color.FromRgb(58, 46, 30);
            }
            else
            {
                ScoreText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(244, 71, 71));
                ScoreBorderColor.Color = System.Windows.Media.Color.FromRgb(58, 30, 30);
            }

            PackagesList.Items.Clear();

            foreach (var package in packages)
            {
                string packageHealth = "Healthy";
                if (package.IsVulnerable) packageHealth = "Vulnerable";
                else if (package.IsDeprecated) packageHealth = "Deprecated";
                else if (package.IsPreRelease) packageHealth = "Needs Upgrade";

                PackagesList.Items.Add($"{package.Name} | {packageHealth}");
            }

            if (PackagesList.Items.Count > 0)
                PackagesList.SelectedIndex = 0;

            string recommendation = "";

            if (result.errors > 0)
                recommendation += "Fix build errors first.\n";

            if (todoCount > 5)
                recommendation += "Review pending TODO comments.\n";

            foreach (var package in packages)
            {
                if (package.IsPreRelease)
                    recommendation += $"Package {package.Name} is pre-release. Consider stable.\n";
            }

            if (string.IsNullOrWhiteSpace(recommendation))
                recommendation = "Project looks healthy.";

            AiInsightText.Text = recommendation;

            // Save for MCP responses
            _lastErrors = result.errors;
            _lastWarnings = result.warnings;
            _lastTodoCount = todoCount;
            _lastHealthScore = healthScore;
            _lastHealthStatus = healthStatus;
            _lastRecommendation = recommendation;
            _lastBuildIssues = BuildIssuesText.Text;

            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine("===== SOLUTION HEALTH ANALYZER =====");
            OutputPaneService.WriteLine($"Errors: {result.errors}");
            OutputPaneService.WriteLine($"Warnings: {result.warnings}");
            OutputPaneService.WriteLine($"TODOs: {todoCount}");
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine($"Health Score: {healthScore} ({healthStatus})");
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine("===== PACKAGE DETAILS =====");

            foreach (var package in packages)
            {
                OutputPaneService.WriteLine($"Package: {package.Name}");
                OutputPaneService.WriteLine($"Current Version: {package.Version}");
                OutputPaneService.WriteLine($"Latest Stable: {package.LatestStableVersion}");
                OutputPaneService.WriteLine($"Package Type: {(package.IsPreRelease ? "Pre-Release" : "Stable")}");
                OutputPaneService.WriteLine($"Support Status: {(package.IsDeprecated ? "Deprecated" : "Active")}");
                OutputPaneService.WriteLine($"Vulnerable: {(package.IsVulnerable ? "Yes" : "No")}");
                OutputPaneService.WriteLine($"Recommendation: {package.Recommendation}");
                OutputPaneService.WriteLine("");
            }

            OutputPaneService.WriteLine("===== BUILD ISSUES =====");
            OutputPaneService.WriteLine(BuildIssuesText.Text);
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine("===== AI INSIGHT =====");
            OutputPaneService.WriteLine(recommendation);
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine("====================================");
        }

        private async void PackagesList_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (PackagesList.SelectedIndex < 0)
                return;

            var package = _currentPackages[PackagesList.SelectedIndex];

            PackageDetailsText.Text =
                $"Health Level: {package.HealthLevel}\n" +
                $"Package: {package.Name}\n" +
                $"Current Version: {package.Version}\n" +
                $"Latest Stable: {package.LatestStableVersion}\n" +
                $"Status: {package.Status}\n" +
                $"Package Type: {(package.IsPreRelease ? "Pre-Release" : "Stable")}\n" +
                $"Support Status: {(package.IsDeprecated ? "Deprecated" : "Active")}\n" +
                $"Vulnerable: {(package.IsVulnerable ? "Yes" : "No")}\n" +
                $"Recommendation: {package.Recommendation}";

            AiInsightText.Text = "⏳ Asking AI for insight...";

            try
            {
                var ai = new SHA_Project.Services.ClaudeAiService();

                string insight = await ai.GetInsightAsync(
                    package.Name,
                    package.Version,
                    package.LatestStableVersion,
                    package.Status);

                AiInsightText.Text = insight;

                OutputPaneService.WriteLine($"[AI] {package.Name}: {insight}");
            }
            catch (System.Exception ex)
            {
                AiInsightText.Text = "AI unavailable: " + ex.Message;
            }
        }

        private void CommandInput_KeyDown(
            object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                RunCommand_Click(null, null);
        }

        private async void RunCommand_Click(object sender, RoutedEventArgs e)
        {
            string cmd = CommandInput.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(cmd))
                return;

            CommandInput.Text = "";

            if (cmd == "scan" || cmd == "analyze")
            {
                AiInsightText.Text = "⏳ Scanning...";
                await DoScanAsync();
            }
            else if (cmd == "build")
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                dte?.ExecuteCommand("Build.BuildSolution");
                AiInsightText.Text = "✓ Build triggered!";
            }
            else if (cmd == "clean")
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                dte?.ExecuteCommand("Build.CleanSolution");
                AiInsightText.Text = "✓ Clean triggered!";
            }
            else
            {
                AiInsightText.Text = $"Unknown command '{cmd}'. Try: scan, build, clean";
            }
        }
    }
}