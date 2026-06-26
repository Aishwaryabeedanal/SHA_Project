using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace SHA_Project.Services
{
    public class DiagnosticDetail
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string Severity { get; set; }
        public string FriendlyMessage { get; set; }
    }

    public class RoslynAnalysisService
    {
        public async Task<(
            int errors,
            int warnings,
            string errorDetails,
            List<DiagnosticDetail> errorList,
            List<DiagnosticDetail> warningList)>
            AnalyzeSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory
                .SwitchToMainThreadAsync();

            var componentModel =
                (IComponentModel)Package.GetGlobalService(
                    typeof(SComponentModel));

            var workspace =
                componentModel
                    .GetService<VisualStudioWorkspace>();

            int errorCount = 0;
            int warningCount = 0;
            string errorDetails = "";

            var errorList = new List<DiagnosticDetail>();
            var warningList = new List<DiagnosticDetail>();

            foreach (var project in
                workspace.CurrentSolution.Projects)
            {
                var compilation =
                    await project.GetCompilationAsync();

                if (compilation == null) continue;

                var diagnostics =
                    compilation.GetDiagnostics();

                // Process errors
                foreach (var d in diagnostics
                    .Where(d =>
                        d.Severity == DiagnosticSeverity.Error)
                    .Take(10))
                {
                    var location = d.Location
                        .GetLineSpan();

                    string fileName =
                        System.IO.Path.GetFileName(
                            location.Path) ?? "Unknown";

                    int line =
                        location.StartLinePosition.Line + 1;

                    string friendly =
                        GetFriendlyMessage(d.Id, d.GetMessage());

                    errorList.Add(new DiagnosticDetail
                    {
                        Id = d.Id,
                        Message = d.GetMessage(),
                        FilePath = fileName,
                        LineNumber = line,
                        Severity = "Error",
                        FriendlyMessage = friendly
                    });

                    errorDetails +=
                        $"❌ {d.Id} in {fileName} " +
                        $"(Line {line})\n" +
                        $"   {friendly}\n\n";
                }

                // Process warnings
                foreach (var d in diagnostics
                    .Where(d =>
                        d.Severity == DiagnosticSeverity.Warning)
                    .Take(10))
                {
                    var location = d.Location
                        .GetLineSpan();

                    string fileName =
                        System.IO.Path.GetFileName(
                            location.Path) ?? "Unknown";

                    int line =
                        location.StartLinePosition.Line + 1;

                    string friendly =
                        GetFriendlyMessage(d.Id, d.GetMessage());

                    warningList.Add(new DiagnosticDetail
                    {
                        Id = d.Id,
                        Message = d.GetMessage(),
                        FilePath = fileName,
                        LineNumber = line,
                        Severity = "Warning",
                        FriendlyMessage = friendly
                    });
                }

                errorCount += diagnostics.Count(
                    d => d.Severity == DiagnosticSeverity.Error);

                warningCount += diagnostics.Count(
                    d => d.Severity == DiagnosticSeverity.Warning);
            }

            // Add warning details to errorDetails
            if (warningList.Count > 0)
            {
                errorDetails += "\n";
                foreach (var w in warningList.Take(5))
                {
                    errorDetails +=
                        $"⚠ {w.Id} in {w.FilePath} " +
                        $"(Line {w.LineNumber})\n" +
                        $"   {w.FriendlyMessage}\n\n";
                }
            }

            if (string.IsNullOrWhiteSpace(errorDetails))
                errorDetails = "✅ No errors or warnings found!";

            return (
                errorCount,
                warningCount,
                errorDetails,
                errorList,
                warningList);
        }

        private string GetFriendlyMessage(
            string code, string original)
        {
            switch (code)
            {
                case "CS0103":
                    return "A variable or method name " +
                           "doesn't exist. Check spelling " +
                           "or add missing declaration.";
                case "CS0246":
                    return "A type or class can't be found. " +
                           "Check using statements or " +
                           "NuGet packages.";
                case "CS1061":
                    return "You're calling something that " +
                           "doesn't exist on this object. " +
                           "Check for typos.";
                case "CS0029":
                    return "Type mismatch — you're putting " +
                           "wrong type into a variable. " +
                           "Convert or cast the value.";
                case "CS8600":
                    return "Possible null value assigned. " +
                           "Add null check or use ? operator.";
                case "CS8602":
                    return "Possible null reference. Use ?. " +
                           "operator or add null check first.";
                case "CS1002":
                    return "Missing semicolon. Every C# " +
                           "statement must end with ;";
                case "CS1003":
                    return "Syntax error — missing bracket " +
                           "or parenthesis nearby.";
                case "CS0168":
                    return "Variable declared but never used. " +
                           "Use it or remove it.";
                case "CS0161":
                    return "Not all code paths return a value. " +
                           "Add return statement.";
                default:
                    return original;
            }
        }
    }
}