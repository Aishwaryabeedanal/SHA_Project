using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SHA_Project
{
    [PackageRegistration(
        UseManagedResourcesOnly = true,
        AllowsBackgroundLoading = true)]
    [Guid(SHA_ProjectPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(SolutionHealthAnalyzerToolWindow))]
    [ProvideAutoLoad(
        UIContextGuids80.SolutionExists,
        PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class SHA_ProjectPackage : AsyncPackage
    {
        public const string PackageGuidString =
            "e4fa8a1f-3134-4352-b550-82ffbb5fc07b";

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory
                .SwitchToMainThreadAsync(cancellationToken);

            await AnalyzeSolutionCommand
                .InitializeAsync(this);

            await HealthDashboardToolWindow
                .InitializeAsync(this);

            // Start MCP server once via singleton
            var mcp = SHA_Project.Services.McpServerService.Instance;
            mcp.OpenToolWindow = () =>
            {
                this.JoinableTaskFactory.Run(async delegate
                {
                    await this.JoinableTaskFactory.SwitchToMainThreadAsync();
                    var window = await this.FindToolWindowAsync(
                        typeof(SolutionHealthAnalyzerToolWindow), 0, true, this.DisposalToken);
                    if (window?.Frame != null)
                    {
                        var windowFrame = (IVsWindowFrame)window.Frame;
                        windowFrame.Show();
                    }
                });
            };

            await Task.Run(() =>
            {
                try { mcp.Start(); } catch { }
            });
        }
    }
}