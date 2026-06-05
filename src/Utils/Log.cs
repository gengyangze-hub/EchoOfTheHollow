using System;
using StardewModdingAPI;

namespace EchoesOfTheHollow.Utils
{
    /// <summary>
    /// Centralized logging wrapper with conditional debug output.
    /// </summary>
    internal static class Log
    {
        private static IMonitor? _monitor;

        public static void Initialize(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public static void Info(string message)
        {
            _monitor?.Log(message, LogLevel.Info);
        }

        public static void Debug(string message)
        {
            if (ModEntry.Config?.DebugMode == true)
                _monitor?.Log($"[DEBUG] {message}", LogLevel.Debug);
        }

        public static void Warn(string message)
        {
            _monitor?.Log($"[WARN] {message}", LogLevel.Warn);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var msg = $"[ERROR] {message}";
            if (ex != null)
                msg += $"\n  Exception: {ex.GetType().Name}: {ex.Message}\n  {ex.StackTrace}";
            _monitor?.Log(msg, LogLevel.Error);
        }

        public static void Trace(string message)
        {
            _monitor?.Log($"[TRACE] {message}", LogLevel.Trace);
        }
    }
}
