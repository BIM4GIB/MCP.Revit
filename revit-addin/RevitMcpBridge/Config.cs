using System;
using System.IO;

namespace RevitMcpBridge
{
    public static class BridgeConfig
    {
        /// <summary>
        /// HTTP port the bridge listens on.
        /// Override via REVIT_MCP_PORT environment variable.
        /// </summary>
        public static int Port
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("REVIT_MCP_PORT");
                return int.TryParse(env, out int p) ? p : 8765;
            }
        }
    }

    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitMcpBridge");

        private static readonly string LogPath = Path.Combine(LogDir, "bridge.log");

        static Logger()
        {
            Directory.CreateDirectory(LogDir);
        }

        public static void Info(string message) => Write("INFO ", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Don't crash Revit over a log failure
            }
        }
    }
}
