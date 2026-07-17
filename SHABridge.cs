using System;
using System.Threading.Tasks;

namespace SHA_Project.Services
{
    public static class SHABridge
    {
        public static Func<Task<string>> ScanAndGetResults;

        public static bool IsReady =>
            ScanAndGetResults != null;
    }
}
