using SHA_Project.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SHA_Project.Services
{
    public class TodoScannerService
    {
        private static readonly string[] SkipDirs = new[]
        {
            "bin", "obj", "node_modules", ".git", ".vs",
            "packages", "TestResults", ".nuget"
        };

        private static readonly string[] ScanExtensions = new[]
        {
            ".cs", ".vb", ".fs", ".ts", ".js", ".jsx", ".tsx",
            ".xml", ".xaml", ".json", ".yaml", ".yml", ".razor"
        };

        private static readonly Regex TodoPattern = new Regex(
            @"//\s*(TODO|FIXME|HACK|BUG)\s*:?\s*(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VbTodoPattern = new Regex(
            @"'\s*(TODO|FIXME|HACK|BUG)\s*:?\s*(.*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public List<TodoItem> ScanTodos(string solutionPath)
        {
            var todos = new List<TodoItem>();

            if (string.IsNullOrWhiteSpace(solutionPath) ||
                !Directory.Exists(solutionPath))
                return todos;

            ScanDirectory(solutionPath, solutionPath, todos);
            return todos;
        }

        private void ScanDirectory(
            string dir, string rootPath, List<TodoItem> todos)
        {
            try
            {
                string dirName = Path.GetFileName(dir);
                foreach (var skip in SkipDirs)
                {
                    if (dirName.Equals(skip,
                        StringComparison.OrdinalIgnoreCase))
                        return;
                }

                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    string ext = Path.GetExtension(file)
                        .ToLowerInvariant();
                    bool isSupported = false;
                    foreach (var e in ScanExtensions)
                    {
                        if (ext == e)
                        {
                            isSupported = true;
                            break;
                        }
                    }
                    if (!isSupported) continue;

                    ScanFile(file, rootPath, todos, ext);
                }

                foreach (var subDir in
                    Directory.EnumerateDirectories(dir))
                {
                    ScanDirectory(subDir, rootPath, todos);
                }
            }
            catch { }
        }

        private void ScanFile(
            string filePath, string rootPath,
            List<TodoItem> todos, string ext)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                string relativePath = filePath;

                try
                {
                    if (filePath.StartsWith(rootPath))
                        relativePath = filePath.Substring(
                            rootPath.Length).TrimStart('\\', '/');
                }
                catch { }

                Regex pattern = TodoPattern;
                if (ext == ".vb") pattern = VbTodoPattern;

                for (int i = 0; i < lines.Length; i++)
                {
                    var match = pattern.Match(lines[i]);
                    if (match.Success)
                    {
                        todos.Add(new TodoItem
                        {
                            FileName = relativePath,
                            LineNumber = i + 1,
                            Type = match.Groups[1].Value.ToUpper(),
                            Text = match.Groups[2].Value.Trim()
                        });
                    }
                }
            }
            catch { }
        }

        // Legacy compatibility
        public int ScanTodoCount(string solutionPath)
        {
            return ScanTodos(solutionPath).Count;
        }
    }
}
