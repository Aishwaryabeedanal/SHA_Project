using System;

namespace SHA_Project.Models
{
    public class TodoItem
    {
        public string FileName { get; set; }
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Type { get; set; } // "TODO" or "FIXME"
    }
}
