using SHA_Project.Models;
using System.Collections.Generic;

namespace SHA_Project.Services
{
    public class ErrorHumanizer
    {
        private static readonly Dictionary<string, string> ErrorMessages =
            new Dictionary<string, string>
            {
                // Syntax errors
                { "CS1002", "You forgot a semicolon (;) at the end of a statement." },
                { "CS1513", "A closing brace '}' is missing somewhere in the code." },
                { "CS1514", "An opening brace '{' is missing somewhere in the code." },
                { "CS1525", "There's an unexpected character or symbol in the code." },
                { "CS1026", "A closing parenthesis ')' is expected but missing." },
                { "CS1001", "An identifier (name) was expected but is missing." },
                { "CS1003", "There's a syntax error - a specific character or keyword was expected." },

                // Name/Type resolution
                { "CS0103", "A variable or object name is used but was never declared. Check spelling or add a declaration." },
                { "CS0246", "A type or namespace could not be found. You may need to add a 'using' statement or install a NuGet package." },
                { "CS0234", "A type or namespace doesn't exist in the given namespace. Check for typos or missing references." },
                { "CS0111", "A class already has a member with the same name and parameters. Rename one of them." },
                { "CS0101", "A namespace already contains a type with this name. Use a different name." },
                { "CS0116", "A namespace cannot directly contain code like fields or methods. Wrap them in a class." },

                // Member access
                { "CS1061", "You're trying to use a method or property that doesn't exist on this type." },
                { "CS0117", "The type doesn't have the member you're trying to use." },
                { "CS0122", "A member exists but is inaccessible due to its protection level (e.g. private)." },
                { "CS0176", "You're trying to access a static member through an instance. Use the class name instead." },

                // Type conversion
                { "CS0029", "Cannot convert one type to another implicitly. You may need an explicit cast." },
                { "CS0030", "Cannot explicitly convert between these types. Check if the conversion is valid." },
                { "CS0266", "Cannot implicitly convert types. An explicit cast exists but might lose data." },

                // Variables
                { "CS0168", "A variable was declared but never used anywhere. You can remove it." },
                { "CS0219", "A variable is assigned a value but that value is never read. Consider removing it." },
                { "CS0165", "A variable is used before it has been assigned a value. Initialize it first." },
                { "CS0128", "A local variable with this name is already defined in this scope." },

                // Methods and parameters
                { "CS1501", "Wrong number of arguments passed to a method. Check the method signature." },
                { "CS0161", "Not all code paths return a value. Make sure every branch has a return statement." },
                { "CS1520", "A method must have a return type. Add 'void' if it returns nothing." },
                { "CS0127", "A method marked as 'void' is trying to return a value." },
                { "CS0200", "You're trying to assign to a read-only property or indexer." },

                // Async
                { "CS1998", "An async method lacks 'await' operators. It will run synchronously." },
                { "CS4033", "The 'await' operator can only be used within an async method. Add 'async' to the method." },
                { "CS0612", "The member you're using is marked as obsolete/deprecated." },

                // Null safety
                { "CS8600", "Converting null literal or possible null value to non-nullable type." },
                { "CS8602", "Possible dereference of a null reference. Add a null check." },
                { "CS8603", "Possible null reference return. The method might return null when it shouldn't." },
                { "CS8604", "Possible null reference argument. The parameter might not accept null." },
                { "CS8618", "Non-nullable property must have a value when exiting constructor." },

                // Others
                { "CS0019", "The operator cannot be applied to the given types." },
                { "CS0052", "The field is not accessible from this context." },
                { "CS0034", "The operator is ambiguous for the given types." },
            };

        public void HumanizeIssues(List<BuildIssue> issues)
        {
            foreach (var issue in issues)
            {
                issue.HumanizedMessage = HumanizeSingle(issue);
            }
        }

        public string HumanizeSingle(BuildIssue issue)
        {
            if (issue == null) return "";

            if (!string.IsNullOrEmpty(issue.ErrorCode) &&
                ErrorMessages.TryGetValue(issue.ErrorCode, out string friendly))
            {
                return friendly + $" (in {issue.FilePath} at line {issue.LineNumber})";
            }

            return $"Build issue in {issue.FilePath} at line {issue.LineNumber}: {issue.RawMessage}";
        }

        // Legacy compatibility
        public string GetFriendlyMessage(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
                return "No build issues found.";

            foreach (var kvp in ErrorMessages)
            {
                if (error.Contains(kvp.Key))
                    return kvp.Value;
            }

            return error;
        }
    }
}
