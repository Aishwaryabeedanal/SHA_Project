using System;
using System.ComponentModel.Design;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace SHA_Project
{
    /// <summary>
    /// Command handler that triggers solution health analysis.
    /// Opens the Solution Health Analyzer tool window and initiates a scan.
    /// </summary>
    internal sealed class AnalyzeSolutionCommand
    {
        /// <summary>
        /// Command ID — matches the VSCT symbol Command1Id (0x0100).
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet =
            new Guid("c76af223-89ec-4beb-bbcd-f2b7d7cacbd7");

        private readonly AsyncPackage package;

        private AnalyzeSolutionCommand(
            AsyncPackage package,
            OleMenuCommandService commandService)
        {
            this.package = package ??
                throw new ArgumentNullException(nameof(package));
            commandService = commandService ??
                throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static AnalyzeSolutionCommand Instance
        {
            get;
            private set;
        }

        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get { return this.package; }
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory
                .SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService =
                await package.GetServiceAsync(
                    typeof(IMenuCommandService))
                as OleMenuCommandService;

            Instance = new AnalyzeSolutionCommand(
                package, commandService);
        }

        /// <summary>
        /// Opens the Solution Health Analyzer tool window.
        /// The tool window automatically triggers a scan on load.
        /// </summary>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Open the tool window
            ToolWindowPane window = this.package.FindToolWindow(
                typeof(SolutionHealthAnalyzerToolWindow), 0, true);

            if (window?.Frame == null)
            {
                throw new NotSupportedException(
                    "Cannot create Solution Health Analyzer tool window");
            }

            IVsWindowFrame windowFrame =
                (IVsWindowFrame)window.Frame;
            Microsoft.VisualStudio.ErrorHandler
                .ThrowOnFailure(windowFrame.Show());
        }
    }
}
