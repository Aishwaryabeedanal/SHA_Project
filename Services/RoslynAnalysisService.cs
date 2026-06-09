using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace SHA_Project.Services
{
    public class RoslynAnalysisService
    {
        public async Task<(int errors,
                   int warnings,
                   string errorDetails)>
             AnalyzeSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var componentModel =
                (IComponentModel)Package.GetGlobalService(
                    typeof(SComponentModel));

            var workspace =
                componentModel.GetService<VisualStudioWorkspace>();

            int errorCount = 0;
            int warningCount = 0;
            string errorDetails = "";

            foreach (var project in workspace.CurrentSolution.Projects)
            {
                var compilation =
                    await project.GetCompilationAsync();

                if (compilation == null)
                    continue;

                var diagnostics =
                    compilation.GetDiagnostics();

                foreach (var diagnostic in diagnostics
                         .Where(d => d.Severity ==
                           DiagnosticSeverity.Error)
                           .Take(5))
                {
                    errorDetails +=
                        $"{diagnostic.Id} : {diagnostic.GetMessage()}\n";
                }

                errorCount += diagnostics.Count(
                    d => d.Severity == DiagnosticSeverity.Error);

                warningCount += diagnostics.Count(
                    d => d.Severity == DiagnosticSeverity.Warning);
            }

            return (
                    errorCount,
                    warningCount,
                    errorDetails
                           ); 
        }
    }
} 