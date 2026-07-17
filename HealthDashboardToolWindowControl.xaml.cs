using EnvDTE;
using Microsoft.VisualStudio.Shell;
using SHA_Project.Models;
using SHA_Project.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SHA_Project
{
    public partial class HealthDashboardToolWindowControl : UserControl
    {
        private List<PackageHealthInfo> _currentPackages =
            new List<PackageHealthInfo>();

        private HealthReport _lastReport;

        public HealthDashboardToolWindowControl()
        {
            InitializeComponent();

            PackagesList.SelectionChanged +=
                PackagesList_SelectionChanged;

            Loaded += HealthDashboardToolWindowControl_Loaded;

            // Wire up MCP server (singleton — already started by package)
            var mcpServer = McpServerService.Instance;

            // Legacy build/clean commands
            mcpServer.CommandReceived += async (body) =>
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
                        var dte = Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand("Build.BuildSolution");
                    });
                }
                else if (cmd.Contains("clean"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        var dte = Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand("Build.CleanSolution");
                    });
                }
            };

            // JSON-RPC command handler for scan requests
            mcpServer.CommandHandler = async (body) =>
            {
                string cmd = body.ToLower()
                    .Replace("{", "").Replace("}", "")
                    .Replace("\"", "").Replace("command:", "")
                    .Trim();

                if (cmd.Contains("scan") ||
                    cmd.Contains("health") ||
                    cmd.Contains("report"))
                {
                    var tcs = new TaskCompletionSource<bool>();

                    await Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await DoScanAsync();
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    try { await tcs.Task; } catch { }
                    await Task.Delay(300);

                    if (_lastReport != null)
                    {
                        return _lastReport.ToMarkdownString();
                    }

                    return "{\"status\":\"ok\",\"message\":\"scan completed\"}";
                }
                else if (cmd.Contains("error"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        errors = _lastReport?.Errors.Count ?? 0,
                        details = _lastReport?.Errors.Select(e => new
                        {
                            file = e.FilePath,
                            line = e.LineNumber,
                            code = e.ErrorCode,
                            message = e.RawMessage,
                            humanized = e.HumanizedMessage
                        }).ToList()
                    });
                }
                else if (cmd.Contains("warning"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        warnings = _lastReport?.Warnings.Count ?? 0,
                        details = _lastReport?.Warnings.Select(w => new
                        {
                            file = w.FilePath,
                            line = w.LineNumber,
                            code = w.ErrorCode,
                            message = w.RawMessage,
                            humanized = w.HumanizedMessage
                        }).ToList()
                    });
                }
                else if (cmd.Contains("todo"))
                {
                    return JsonSerializer.Serialize(new
                    {
                        count = _lastReport?.Todos.Count ?? 0,
                        items = _lastReport?.Todos.Select(t => new
                        {
                            file = t.FileName,
                            line = t.LineNumber,
                            text = t.Text,
                            type = t.Type
                        }).ToList()
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
                        score = _lastReport?.HealthScore ?? 0,
                        status = _lastReport?.HealthStatus ?? "Unknown"
                    });
                }

                return "{\"status\":\"ok\",\"message\":\"command not recognized\"}";
            };
        }

        private async void HealthDashboardToolWindowControl_Loaded(
            object sender, RoutedEventArgs e)
        {
            await Task.Delay(500);
            await DoScanAsync();
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            ScanButton.IsEnabled = false;
            ScanButton.Content = "⏳ Scanning...";

            try
            {
                await DoScanAsync();
            }
            finally
            {
                ScanButton.IsEnabled = true;
                ScanButton.Content = "🔍 Scan Solution";
            }
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

            // Create the health report
            var report = new HealthReport();

            // 1. Scan TODOs (per-occurrence)
            TodoScannerService todoService = new TodoScannerService();
            report.Todos = todoService.ScanTodos(solutionPath);

            // 2. Roslyn build analysis (returns structured BuildIssue lists)
            RoslynAnalysisService roslynService = new RoslynAnalysisService();
            var buildResult = await roslynService.AnalyzeSolutionAsync();
            report.Errors = buildResult.errors;
            report.Warnings = buildResult.warnings;

            // 3. Humanize errors and warnings
            ErrorHumanizer humanizer = new ErrorHumanizer();
            humanizer.HumanizeIssues(report.Errors);
            humanizer.HumanizeIssues(report.Warnings);

            // 4. NuGet package scan
            NuGetInspectionService nugetService = new NuGetInspectionService();
            report.Packages = await nugetService.GetPackagesAsync(solutionPath);
            _currentPackages = report.Packages;

            // 5. Calculate health score
            report.CalculateHealthScore();
            report.ScannedAt = DateTime.Now;

            // 6. AI Insights (Gemini)
            try
            {
                var aiService = new ClaudeAiService();
                report.AiInsights = await aiService.GetFullInsightsAsync(report);

                // Apply per-package insights back to packages
                foreach (var pkg in report.Packages)
                {
                    if (report.AiInsights.PerPackageInsights
                        .TryGetValue(pkg.Name, out string insight))
                    {
                        pkg.AiInsight = insight;
                    }
                }
            }
            catch (Exception ex)
            {
                report.AiInsights = new AiInsightResult
                {
                    OverallFeedback = "AI analysis failed: " + ex.Message
                };
            }

            // Store for MCP access
            _lastReport = report;
            McpServerService.Instance.SetLastReport(report);

            // --- UPDATE UI ---

            // Errors & Warnings
            ErrorsText.Text = $"❌ Errors: {report.Errors.Count}";
            WarningsText.Text = $"⚠ Warnings: {report.Warnings.Count}";
            TodoText.Text = $"📝 TODOs: {report.Todos.Count}";

            // Build Issues text
            if (report.Errors.Count > 0 || report.Warnings.Count > 0)
            {
                var issuesText = new StringBuilder();
                foreach (var e in report.Errors)
                {
                    issuesText.AppendLine(
                        $"[Error] {e.ErrorCode} in {e.FilePath} line {e.LineNumber}:");
                    issuesText.AppendLine($"  {e.RawMessage}");
                    issuesText.AppendLine($"  → {e.HumanizedMessage}");
                    issuesText.AppendLine();
                }
                foreach (var w in report.Warnings.Take(20))
                {
                    issuesText.AppendLine(
                        $"[Warning] {w.ErrorCode} in {w.FilePath} line {w.LineNumber}:");
                    issuesText.AppendLine($"  {w.RawMessage}");
                    issuesText.AppendLine($"  → {w.HumanizedMessage}");
                    issuesText.AppendLine();
                }
                BuildIssuesText.Text = issuesText.ToString();
            }
            else
            {
                BuildIssuesText.Text = "✅ No build issues found.";
            }

            // TODO Comments text
            if (report.Todos.Count > 0)
            {
                var todosText = new StringBuilder();
                todosText.AppendLine($"Total: {report.Todos.Count}");
                todosText.AppendLine();
                foreach (var t in report.Todos.Take(50))
                {
                    todosText.AppendLine(
                        $"[{t.Type}] {t.FileName} line {t.LineNumber}: {t.Text}");
                }
                TodoDetailsText.Text = todosText.ToString();
            }
            else
            {
                TodoDetailsText.Text = "✅ No TODO comments found.";
            }

            // Health score with color coding
            ScoreText.Text = $"Health Score: {report.HealthScore}/100 ({report.HealthStatus})";

            if (report.HealthScore >= 90)
            {
                ScoreText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(78, 201, 78));
                ScoreBorderColor.Color = System.Windows.Media.Color.FromRgb(30, 58, 30);
            }
            else if (report.HealthScore >= 75)
            {
                ScoreText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(144, 238, 144));
                ScoreBorderColor.Color = System.Windows.Media.Color.FromRgb(30, 50, 30);
            }
            else if (report.HealthScore >= 50)
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

            // Package list
            PackagesList.Items.Clear();
            foreach (var package in report.Packages)
            {
                string icon = package.HealthLevel == "Healthy" ? "✅" :
                    package.HealthLevel == "Vulnerable" ? "🔴" :
                    package.HealthLevel == "Deprecated" ? "⚠" :
                    package.HealthLevel == "Pre-Release" ? "🟡" :
                    package.HealthLevel == "Outdated" ? "🔵" : "📦";

                PackagesList.Items.Add(
                    $"{icon} {package.Name} | {package.HealthLevel}");
            }

            if (PackagesList.Items.Count > 0)
                PackagesList.SelectedIndex = 0;

            // AI Insights
            var aiText = new StringBuilder();
            if (report.AiInsights != null)
            {
                if (!string.IsNullOrEmpty(report.AiInsights.BuildAnalysis))
                {
                    aiText.AppendLine("── Build Analysis ──");
                    aiText.AppendLine(report.AiInsights.BuildAnalysis);
                    aiText.AppendLine();
                }
                if (!string.IsNullOrEmpty(report.AiInsights.OptimizationSuggestions))
                {
                    aiText.AppendLine("── Optimization ──");
                    aiText.AppendLine(report.AiInsights.OptimizationSuggestions);
                    aiText.AppendLine();
                }
                if (!string.IsNullOrEmpty(report.AiInsights.PackageRecommendations))
                {
                    aiText.AppendLine("── Package Recommendations ──");
                    aiText.AppendLine(report.AiInsights.PackageRecommendations);
                    aiText.AppendLine();
                }
                if (!string.IsNullOrEmpty(report.AiInsights.OverallFeedback))
                {
                    aiText.AppendLine("── Overall Assessment ──");
                    aiText.AppendLine(report.AiInsights.OverallFeedback);
                }
            }

            AiInsightText.Text = aiText.Length > 0
                ? aiText.ToString()
                : "AI insights unavailable.";

            // Timestamp
            LastScannedText.Text = $"Last scanned: {report.ScannedAt:HH:mm:ss}";

            // 7. Write to Output Pane (full formatted report)
            OutputPaneService.WriteFullReport(report);
        }

        private async void PackagesList_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (PackagesList.SelectedIndex < 0)
                return;

            var package = _currentPackages[PackagesList.SelectedIndex];

            PackageDetailsText.Text =
                $"Package: {package.Name}\n" +
                $"Installed Version : {package.Version}\n" +
                $"Latest Stable     : {package.LatestStableVersion}\n" +
                $"Vulnerable        : {(package.IsVulnerable ? "Yes" : "No")}\n" +
                $"Deprecated        : {(package.IsDeprecated ? "Yes" : "No")}\n" +
                $"Pre-Release       : {(package.IsPreRelease ? "Yes" : "No")}\n" +
                $"New Version       : {(package.IsNewVersionAvailable ? "Available" : "Up to Date")}\n" +
                $"Health Status     : {package.HealthLevel}\n" +
                $"Recommendation    : {package.Recommendation}\n" +
                $"Upgrade Command   : {package.UpgradeCommand}";

            // Get AI insight for selected package
            if (string.IsNullOrEmpty(package.AiInsight) ||
                package.AiInsight == "")
            {
                AiInsightText.Text = "⏳ Asking Gemini AI for insight...";

                try
                {
                    var ai = new ClaudeAiService();
                    string insight = await ai.GetInsightAsync(
                        package.Name,
                        package.Version,
                        package.LatestStableVersion,
                        package.HealthLevel);

                    package.AiInsight = insight;
                    AiInsightText.Text = $"── AI Insight: {package.Name} ──\n{insight}";

                    OutputPaneService.WriteLine(
                        $"[AI] {package.Name}: {insight}");
                }
                catch (Exception ex)
                {
                    AiInsightText.Text = "AI unavailable: " + ex.Message;
                }
            }
            else
            {
                AiInsightText.Text =
                    $"── AI Insight: {package.Name} ──\n{package.AiInsight}";
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