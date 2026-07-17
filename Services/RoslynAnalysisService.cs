using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using SHA_Project.Models;

namespace SHA_Project.Services
{
    public class RoslynAnalysisService
    {
        public async Task<(List<BuildIssue> errors, List<BuildIssue> warnings)>
            AnalyzeSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var componentModel =
                (IComponentModel)Package.GetGlobalService(
                    typeof(SComponentModel));

            var workspace =
                componentModel.GetService<VisualStudioWorkspace>();

            var errors = new List<BuildIssue>();
            var warnings = new List<BuildIssue>();

            foreach (var project in workspace.CurrentSolution.Projects)
            {
                var compilation =
                    await project.GetCompilationAsync();

                if (compilation == null)
                    continue;

                var diagnostics = compilation.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error ||
                                d.Severity == DiagnosticSeverity.Warning);

                foreach (var diagnostic in diagnostics)
                {
                    var lineSpan = diagnostic.Location.GetMappedLineSpan();
                    string filePath = lineSpan.Path ?? "Unknown";
                    int line = lineSpan.StartLinePosition.Line + 1;
                    int col = lineSpan.StartLinePosition.Character + 1;

                    // Extract just the filename for display
                    string fileName = filePath;
                    try
                    {
                        fileName = System.IO.Path.GetFileName(filePath);
                    }
                    catch { }

                    var issue = new BuildIssue
                    {
                        FilePath = fileName,
                        LineNumber = line,
                        Column = col,
                        ErrorCode = diagnostic.Id,
                        RawMessage = diagnostic.GetMessage(),
                        HumanizedMessage = "", // Will be set by ErrorHumanizer
                        Severity = diagnostic.Severity == DiagnosticSeverity.Error
                            ? IssueSeverity.Error
                            : IssueSeverity.Warning
                    };

                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        errors.Add(issue);
                    else
                        warnings.Add(issue);
                }
            }

            return (errors, warnings);
        }
    }
}