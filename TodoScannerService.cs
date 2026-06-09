using System.IO;

namespace SHA_Project.Services
{
    public class TodoScannerService
    {
        public int ScanTodos(string solutionPath)
        {
            int todoCount = 0;

            var files = Directory.GetFiles(
                solutionPath,
                "*.cs",
                SearchOption.AllDirectories);

            foreach (var file in files)
            {
                string content = File.ReadAllText(file);

                if (content.Contains("TODO"))
                {
                    todoCount++;
                }
            }

            return todoCount;
        }
    }
} 