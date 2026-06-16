using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SHA_Project
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("7bb08fd4-3994-4cc6-a4b0-185aaad294dd")]


    public class SolutionHealthAnalyzerToolWindow : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1"/> class.
        /// </summary>
        public SolutionHealthAnalyzerToolWindow() : base(null)
        {
            this.Caption = "Solution Health Analyzer";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new HealthDashboardToolWindowControl();
        }
    }
}
