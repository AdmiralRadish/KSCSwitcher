using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace regexKSP
{
    /// <summary>
    /// Detects LunaMultiplayer via reflection and retrieves the current player name.
    /// Adapted from FastVesselChanger's FVCLunaHelper. Prefixed KSC to avoid type collision.
    /// </summary>
    public static class KSCLunaHelper
    {
        private static bool? _isLunaAvailable;
        private static string _cachedPlayerName;

        public static bool IsLunaEnabled
        {
            get
            {
                if (_isLunaAvailable == null)
                    _isLunaAvailable = DetectLunaMultiplayer();
                return _isLunaAvailable.Value;
            }
        }

        public static string GetCurrentPlayerName()
        {
            if (_cachedPlayerName != null)
            {
                // If Luna is enabled and we previously fell back to SinglePlayer,
                // retry because the client may not have been fully initialized yet.
                if (!string.Equals(_cachedPlayerName, "SinglePlayer", StringComparison.OrdinalIgnoreCase) || !IsLunaEnabled)
                    return _cachedPlayerName;
                _cachedPlayerName = null;
            }

            try
            {
                if (IsLunaEnabled)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Strategy 1: Main.MyPlayer.PlayerName
                    Type mainType = null;
                    foreach (var a in assemblies)
                    {
                        mainType = a.GetType("LunaMultiplayer.Client.Main")
                                ?? a.GetType("LMP.Client.Main");
                        if (mainType != null) break;
                    }

                    if (mainType != null)
                    {
                        var myPlayerProp = mainType.GetProperty("MyPlayer", BindingFlags.Public | BindingFlags.Static);
                        if (myPlayerProp != null)
                        {
                            var myPlayer = myPlayerProp.GetValue(null);
                            if (myPlayer != null)
                            {
                                string name = ExtractStringMember(myPlayer, "PlayerName")
                                           ?? ExtractStringMember(myPlayer, "Name")
                                           ?? ExtractStringMember(myPlayer, "UserName");
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    _cachedPlayerName = SanitizePlayerName(name);
                                    Debug.Log("[KSCSwitcher] Detected Luna player: " + _cachedPlayerName);
                                    return _cachedPlayerName;
                                }
                            }
                        }
                    }

                    // Strategy 2: SettingsSystem.CurrentSettings.PlayerName
                    foreach (var a in assemblies)
                    {
                        Type settingsType = a.GetType("LmpClient.Systems.SettingsSys.SettingsSystem")
                                         ?? a.GetType("LMP.Client.Systems.Settings.SettingsSystem")
                                         ?? a.GetType("LunaMultiplayer.Client.Systems.SettingsSys.SettingsSystem");
                        if (settingsType == null) continue;

                        object currentSettings = null;
                        var currentSettingsProp = settingsType.GetProperty("CurrentSettings",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (currentSettingsProp != null)
                        {
                            try { currentSettings = currentSettingsProp.GetValue(null, null); } catch { }
                        }

                        if (currentSettings == null)
                        {
                            var singletonProp = settingsType.GetProperty("Singleton",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                                ?? settingsType.GetProperty("Instance",
                                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                            if (singletonProp != null)
                            {
                                object singleton = null;
                                try { singleton = singletonProp.GetValue(null, null); } catch { }
                                if (singleton != null)
                                {
                                    var innerProp = singleton.GetType().GetProperty("CurrentSettings",
                                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                    if (innerProp != null)
                                        try { currentSettings = innerProp.GetValue(singleton, null); } catch { }
                                }
                            }
                        }

                        if (currentSettings != null)
                        {
                            string name = ExtractStringMember(currentSettings, "PlayerName")
                                       ?? ExtractStringMember(currentSettings, "Name")
                                       ?? ExtractStringMember(currentSettings, "UserName");
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                _cachedPlayerName = SanitizePlayerName(name);
                                Debug.Log("[KSCSwitcher] Detected Luna player (settings): " + _cachedPlayerName);
                                return _cachedPlayerName;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KSCSwitcher] Error detecting Luna player name: " + e.Message);
            }

            if (!IsLunaEnabled)
                _cachedPlayerName = "SinglePlayer";

            return "SinglePlayer";
        }

        public static void ClearCache()
        {
            _cachedPlayerName = null;
        }

        private static bool DetectLunaMultiplayer()
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = a.GetName().Name ?? "";
                    if (name.IndexOf("LunaMultiplayer", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.Equals("LMP.Client", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("LmpClient", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static string ExtractStringMember(object source, string memberName)
        {
            if (source == null) return null;
            var type = source.GetType();

            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(string))
            {
                try { return prop.GetValue(source, null) as string; } catch { }
            }

            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string))
            {
                try { return field.GetValue(source) as string; } catch { }
            }

            return null;
        }

        private static string SanitizePlayerName(string playerName)
        {
            var sb = new StringBuilder();
            foreach (char c in playerName)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }
    }
}
