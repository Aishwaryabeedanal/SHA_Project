using System.Collections.Generic;
using SHA_Project.Models;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using SHA_Project.Services;
using System.Windows;
using System.Windows.Controls;

namespace SHA_Project
{
    public partial class HealthDashboardToolWindowControl : UserControl
    {
        private SHA_Project.Services.McpServerService _mcpServer;
        private List<PackageHealthInfo> _currentPackages =
            new List<PackageHealthInfo>();

        public HealthDashboardToolWindowControl()
        {
            InitializeComponent();

            PackagesList.SelectionChanged +=
                PackagesList_SelectionChanged;

            Loaded += HealthDashboardToolWindowControl_Loaded;

            _mcpServer =
                new SHA_Project.Services.McpServerService();

            _mcpServer.CommandReceived += async (body) =>
            {
                string cmd = body.ToLower()
                    .Replace("{", "")
                    .Replace("}", "")
                    .Replace("\"", "")
                    .Replace("command:", "")
                    .Trim();

                if (cmd.Contains("scan") ||
                    cmd.Contains("analyze"))
                {
                    await Dispatcher.InvokeAsync(() =>
                        ScanButton_Click(null, null));
                }
                else if (cmd.Contains("build"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory
                            .SwitchToMainThreadAsync();
                        var dte = Microsoft.VisualStudio.Shell
                            .Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand(
                            "Build.BuildSolution");
                    });
                }
                else if (cmd.Contains("clean"))
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory
                            .SwitchToMainThreadAsync();
                        var dte = Microsoft.VisualStudio.Shell
                            .Package.GetGlobalService(
                            typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                        dte?.ExecuteCommand(
                            "Build.CleanSolution");
                    });
                }
            };

            _mcpServer.Start();
        }

        private async void HealthDashboardToolWindowControl_Loaded(
            object sender, RoutedEventArgs e)
        {
            await System.Threading.Tasks.Task.Delay(500);
            ScanButton_Click(null, null);
        }

        private async void ScanButton_Click(
            object sender, RoutedEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory
                .SwitchToMainThreadAsync();

            OutputPaneService.Initialize();
            OutputPaneService.Clear();

            DTE dte = Package.GetGlobalService(
                typeof(DTE)) as DTE;

            if (string.IsNullOrWhiteSpace(
                dte?.Solution?.FullName))
                return;

            string solutionPath =
                System.IO.Path.GetDirectoryName(
                    dte.Solution.FullName);

            TodoScannerService todoService =
                new TodoScannerService();
            int todoCount =
                todoService.ScanTodos(solutionPath);

            RoslynAnalysisService roslynService =
                new RoslynAnalysisService();
            var result =
                await roslynService.AnalyzeSolutionAsync();

            ErrorsText.Text = $"Errors: {result.errors}";
            WarningsText.Text = $"Warnings: {result.warnings}";

            ErrorHumanizer humanizer = new ErrorHumanizer();
            BuildIssuesText.Text =
                humanizer.GetFriendlyMessage(
                    result.errorDetails);

            TodoText.Text = $"TODOs: {todoCount}";

            NuGetInspectionService nugetService =
                new NuGetInspectionService();
            var packages =
                await nugetService.GetPackagesAsync(
                    solutionPath);
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
            if (healthScore >= 90)
                healthStatus = "Excellent";
            else if (healthScore >= 75)
                healthStatus = "Good";
            else if (healthScore >= 50)
                healthStatus = "Needs Attention";
            else
                healthStatus = "Critical";

            ScoreText.Text =
                $"Health Score: {healthScore} ({healthStatus})";

            PackagesList.Items.Clear();

            foreach (var package in packages)
            {
                string packageHealth = "Healthy";
                if (package.IsVulnerable)
                    packageHealth = "Vulnerable";
                else if (package.IsDeprecated)
                    packageHealth = "Deprecated";
                else if (package.IsPreRelease)
                    packageHealth = "Needs Upgrade";

                PackagesList.Items.Add(
                    $"{package.Name} | {packageHealth}");
            }

            if (PackagesList.Items.Count > 0)
                PackagesList.SelectedIndex = 0;

            string recommendation = "";

            if (result.errors > 0)
                recommendation += "Fix build errors first.\n";

            if (todoCount > 5)
                recommendation +=
                    "Review pending TODO comments.\n";

            foreach (var package in packages)
            {
                if (package.IsPreRelease)
                    recommendation +=
                        $"Package {package.Name} is " +
                        $"pre-release. Consider stable.\n";
            }

            if (string.IsNullOrWhiteSpace(recommendation))
                recommendation = "Project looks healthy.";

            AiInsightText.Text = recommendation;

            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine(
                "===== SOLUTION HEALTH ANALYZER =====");
            OutputPaneService.WriteLine(
                $"Errors: {result.errors}");
            OutputPaneService.WriteLine(
                $"Warnings: {result.warnings}");
            OutputPaneService.WriteLine(
                $"TODOs: {todoCount}");
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine(
                $"Health Score: {healthScore} ({healthStatus})");
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine(
                "===== PACKAGE DETAILS =====");

            foreach (var package in packages)
            {
                OutputPaneService.WriteLine(
                    $"Package: {package.Name}");
                OutputPaneService.WriteLine(
                    $"Current Version: {package.Version}");
                OutputPaneService.WriteLine(
                    $"Latest Stable: {package.LatestStableVersion}");
                OutputPaneService.WriteLine(
                    $"Package Type: {(package.IsPreRelease ? "Pre-Release" : "Stable")}");
                OutputPaneService.WriteLine(
                    $"Support Status: {(package.IsDeprecated ? "Deprecated" : "Active")}");
                OutputPaneService.WriteLine(
                    $"Vulnerable: {(package.IsVulnerable ? "Yes" : "No")}");
                OutputPaneService.WriteLine(
                    $"Recommendation: {package.Recommendation}");
                OutputPaneService.WriteLine("");
            }

            OutputPaneService.WriteLine(
                "===== BUILD ISSUES =====");
            OutputPaneService.WriteLine(BuildIssuesText.Text);
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine(
                "===== AI INSIGHT =====");
            OutputPaneService.WriteLine(recommendation);
            OutputPaneService.WriteLine("");
            OutputPaneService.WriteLine(
                "====================================");
        }

        private async void PackagesList_SelectionChanged(
            object sender, SelectionChangedEventArgs e)
        {
            if (PackagesList.SelectedIndex < 0)
                return;

            var package =
                _currentPackages[PackagesList.SelectedIndex];

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
                var ai =
                    new SHA_Project.Services.ClaudeAiService();

                string insight = await ai.GetInsightAsync(
                    package.Name,
                    package.Version,
                    package.LatestStableVersion,
                    package.Status);

                AiInsightText.Text = insight;

                OutputPaneService.WriteLine(
                    $"[AI] {package.Name}: {insight}");
            }
            catch (System.Exception ex)
            {
                AiInsightText.Text =
                    "AI unavailable: " + ex.Message;
            }
        }

        private void CommandInput_KeyDown(
            object sender,
            System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                RunCommand_Click(null, null);
        }

        private async void RunCommand_Click(
            object sender, RoutedEventArgs e)
        {
            string cmd = CommandInput.Text
                .Trim().ToLower();

            if (string.IsNullOrWhiteSpace(cmd))
                return;

            CommandInput.Text = "";

            if (cmd == "scan" || cmd == "analyze")
            {
                AiInsightText.Text = "⏳ Scanning...";
                ScanButton_Click(null, null);
            }
            else if (cmd == "build")
            {
                await ThreadHelper.JoinableTaskFactory
                    .SwitchToMainThreadAsync();
                var dte = Package.GetGlobalService(
                    typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                dte?.ExecuteCommand("Build.BuildSolution");
                AiInsightText.Text = "✓ Build triggered!";
            }
            else if (cmd == "clean")
            {
                await ThreadHelper.JoinableTaskFactory
                    .SwitchToMainThreadAsync();
                var dte = Package.GetGlobalService(
                    typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                dte?.ExecuteCommand("Build.CleanSolution");
                AiInsightText.Text = "✓ Clean triggered!";
            }
            else
            {
                AiInsightText.Text =
                    $"Unknown command '{cmd}'. " +
                    $"Try: scan, build, clean";
            }
        }

    }  // closes class
}      // closes namespace