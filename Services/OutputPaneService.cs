using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SHA_Project.Services
{
    public static class OutputPaneService
    {
        private static IVsOutputWindowPane outputPane;

        public static IVsOutputWindowPane Initialize()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var outWindow =
                    Package.GetGlobalService(typeof(SVsOutputWindow))
                    as IVsOutputWindow;

                Guid paneGuid = new Guid(
                    "12345678-1234-1234-1234-123456789012");

                outWindow.CreatePane(
                    ref paneGuid,
                    "Solution Health Analyzer",
                    1,
                    1);

                outWindow.GetPane(
                    ref paneGuid,
                    out outputPane);

                outputPane.Activate();
            });

            return outputPane;
        }

        public static void WriteLine(string message)
        {
            outputPane?.OutputStringThreadSafe(
                $"{message}\n");
        }

        public static void Clear()
        {
            outputPane?.Clear();
        } 
    }
}
