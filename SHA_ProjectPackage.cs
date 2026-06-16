using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace SHA_Project
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(SHA_ProjectPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(SolutionHealthAnalyzerToolWindow))]
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

            await AnalyzeSolutionCommand.InitializeAsync(this);
            await HealthDashboardToolWindow.InitializeAsync(this);
        }
    }
}