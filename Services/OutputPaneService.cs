using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SHA_Project.Models;

namespace SHA_Project.Services
{
    public static class OutputPaneService
    {
        private static IVsOutputWindowPane _outputPane;
        private static readonly Guid PaneGuid =
            new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        public static IVsOutputWindowPane Initialize()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory
                    .SwitchToMainThreadAsync();

                var outWindow =
                    Package.GetGlobalService(typeof(SVsOutputWindow))
                    as IVsOutputWindow;

                if (outWindow == null) return;

                Guid paneGuid = PaneGuid;

                outWindow.CreatePane(
                    ref paneGuid,
                    "Solution Health Analyzer",
                    1, 1);

                outWindow.GetPane(
                    ref paneGuid,
                    out _outputPane);

                _outputPane?.Activate();
            });

            return _outputPane;
        }

        public static void WriteLine(string message)
        {
            _outputPane?.OutputStringThreadSafe(
                $"{message}\n");
        }

        public static void Clear()
        {
            _outputPane?.Clear();
        }

        /// <summary>
        /// Writes the full health report in the spec-defined format.
        /// </summary>
        public static void WriteFullReport(HealthReport report)
        {
            if (report == null) return;

            Initialize();
            Clear();

            string output = report.ToOutputPaneString();
            _outputPane?.OutputStringThreadSafe(output);
            _outputPane?.Activate();
        }
    }
}
