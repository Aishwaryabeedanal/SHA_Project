using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    public class ErrorHumanizer
    {
        public string GetFriendlyMessage(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "No build issues found.";
            }

            if (error.Contains("CS0103"))
            {
                return "A variable or object is being used before it is declared. Check the spelling or create it first.";
            }

            if (error.Contains("CS0246"))
            {
                return "A class or type could not be found. You may be missing a using statement or NuGet package.";
            }

            if (error.Contains("CS1061"))
            {
                return "You are calling a method or property that does not exist on this object.";
            }

            if (error.Contains("CS1002"))
            {
                return "A semicolon (;) is missing.";
            }

            if (error.Contains("CS0168"))
            {
                return "A variable has been declared but is never used.";
            }

            if (error.Contains("CS0219"))
            {
                return "A value is assigned to a variable but never used.";
            }

            if (error.Contains("CS0029"))
            {
                return "A value is being assigned to an incompatible data type.";
            }

            if (error.Contains("CS1513"))
            {
                return "A closing brace '}' is missing.";
            }

            return error;
        }
    }
} 
