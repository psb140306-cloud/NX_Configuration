using System;
using System.IO;

namespace NXConfigLauncher.Helpers
{
    public static class Logger
    {
        private static readonly string LogDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NXConfigLauncher", "logs");

        private static readonly string LogFilePath =
            Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Logging should never break the app.
            }
        }
    }
}
