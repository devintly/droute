using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Droute.UpdaterHook
{
    internal static class Logger
    {
        private static readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Temp", "droute.log");
        private static readonly object _lock = new object();

        static Logger()
        {
            lock (_lock)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_logPath, $"// updaterHook session started at {timestamp}{Environment.NewLine}");
            }
        }

        public static void Trace(string message, [CallerFilePath] string filePath = "")
            => Log("TRACE", message, filePath);

        public static void Debug(string message, [CallerFilePath] string filePath = "")
            => Log("DEBUG", message, filePath);

        public static void Info(string message, [CallerFilePath] string filePath = "")
            => Log("INFO", message, filePath);

        public static void Warning(string message, [CallerFilePath] string filePath = "")
            => Log("WARN", message, filePath);

        public static void Error(string message, [CallerFilePath] string filePath = "")
            => Log("ERROR", message, filePath);

        private static void Log(string level, string message, string filePath)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                string fileName = !string.IsNullOrEmpty(filePath) ? Path.GetFileName(filePath) : "unknown";

                string logLine = $"[{timestamp}] [{level}] [{fileName}] {message}{Environment.NewLine}";

                lock (_lock) File.AppendAllText(_logPath, logLine);
            }
            catch { }
        }
    }
}
