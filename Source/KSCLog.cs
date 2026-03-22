using UnityEngine;

namespace regexKSP
{
    /// <summary>
    /// Centralized logger for all KSCSwitcher classes.
    /// Toggle VerboseLogging to control detailed trace output.
    /// </summary>
    public static class KSCLog
    {
        private const string Tag = "[KSC Switcher]";

        /// <summary>
        /// Set to true to enable detailed trace logging.
        /// When false, only Log/Warn/Error calls produce output.
        /// </summary>
        public static bool VerboseLogging = false;

        public static void Log(string message)
        {
            Debug.Log($"{Tag} {message}");
        }

        public static void Warn(string message)
        {
            Debug.LogWarning($"{Tag} {message}");
        }

        public static void Error(string message)
        {
            Debug.LogError($"{Tag} {message}");
        }

        /// <summary>
        /// Only logs when VerboseLogging is true. Use for high-frequency or detail tracing.
        /// </summary>
        public static void Verbose(string message)
        {
            if (VerboseLogging)
                Debug.Log($"{Tag} [V] {message}");
        }
    }
}
