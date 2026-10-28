using UnityEngine;
using VRSYS.Core.Logging;

namespace VRSYS.Recording
{
    public static class RecordingLogger
    {
        private static volatile LogLevel _level = LogLevel.Info;

        public static LogLevel Level
        {
            get => _level;
            internal set => _level = value;
        }

        public static bool IsEnabled(LogLevel level)
        {
            return _level != LogLevel.None && level >= _level && level < LogLevel.None;
        }

        public static void LogDebug(string className, string message, Object context = null)
        {
            if (IsEnabled(LogLevel.Debug))
                ExtendedLogger.LogInfo(className, "[Debug] " + message, context);
        }

        public static void LogInfo(string className, string message, Object context = null)
        {
            if (IsEnabled(LogLevel.Info))
                ExtendedLogger.LogInfo(className, message, context);
        }

        public static void LogWarning(string className, string message, Object context = null)
        {
            if (IsEnabled(LogLevel.Warning))
                ExtendedLogger.LogWarning(className, message, context);
        }

        public static void LogError(string className, string message, Object context = null)
        {
            if (IsEnabled(LogLevel.Error))
                ExtendedLogger.LogError(className, message, context);
        }
    }
}
