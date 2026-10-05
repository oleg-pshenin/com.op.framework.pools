using UnityEngine;

namespace OP.Framework.Pools
{
    public static class Log
    {
        public static System.Action<string, Object> LogErrorHandler = Debug.LogError;
        public static System.Action<string, Object> LogWarningHandler = Debug.LogWarning;
        public static System.Action<string, Object> LogHandler = Debug.Log;

        internal static void LogError(string message, Object context = null) => LogErrorHandler(message, context);
        internal static void LogWarning(string message, Object context = null) => LogWarningHandler(message, context);
        internal static void LogInfo(string message, Object context = null) => LogHandler(message, context);
    }
}