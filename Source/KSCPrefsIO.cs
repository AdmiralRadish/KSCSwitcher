using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace regexKSP
{
    /// <summary>
    /// Reads and writes the last-selected launch site to an XML file in PluginData.
    /// PluginData is local to each install, so LunaMultiplayer players each have their own copy.
    /// </summary>
    public static class KSCPrefsIO
    {
        private const string PrefsFileName = "KSCSwitcher.xml";
        private const string RootElementName = "KSCSwitcherPrefs";

        private static string _prefsPath;

        public static string GetPrefsPath()
        {
            if (_prefsPath != null)
                return _prefsPath;

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var pluginDir = new DirectoryInfo(assemblyDir);
            DirectoryInfo modDir = pluginDir.Parent;

            if (modDir != null)
                _prefsPath = Path.Combine(modDir.FullName, "PluginData", PrefsFileName);
            else
                _prefsPath = Path.Combine(assemblyDir, PrefsFileName);

            KSCLog.Verbose($"KSCPrefsIO: prefs path = {_prefsPath}");
            return _prefsPath;
        }

        /// <summary>
        /// Load the last launch site name. Returns empty string if nothing saved yet.
        /// </summary>
        public static string LoadLastSite()
        {
            string path = GetPrefsPath();
            if (!File.Exists(path))
            {
                KSCLog.Verbose("KSCPrefsIO.LoadLastSite: no prefs file exists yet.");
                return "";
            }

            try
            {
                var doc = new XmlDocument();
                doc.Load(path);

                XmlElement root = doc.DocumentElement;
                if (root == null) return "";

                XmlElement lastSiteEl = root["LastLaunchSite"];
                string site = lastSiteEl?.InnerText ?? "";
                KSCLog.Verbose($"KSCPrefsIO.LoadLastSite: site='{site}'");
                return site;
            }
            catch (Exception e)
            {
                KSCLog.Warn($"KSCPrefsIO.LoadLastSite: error reading prefs: {e.Message}");
            }

            return "";
        }

        /// <summary>
        /// Save the last launch site name.
        /// </summary>
        public static void SaveLastSite(string siteName)
        {
            string path = GetPrefsPath();

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var doc = new XmlDocument();

                if (File.Exists(path))
                {
                    try { doc.Load(path); }
                    catch { /* corrupt file, start fresh */ }
                }

                XmlElement root = doc.DocumentElement;
                if (root == null)
                {
                    root = doc.CreateElement(RootElementName);
                    doc.AppendChild(root);
                }

                XmlElement lastSiteEl = root["LastLaunchSite"];
                if (lastSiteEl == null)
                {
                    lastSiteEl = doc.CreateElement("LastLaunchSite");
                    root.AppendChild(lastSiteEl);
                }
                lastSiteEl.InnerText = siteName;

                doc.Save(path);
                KSCLog.Verbose($"KSCPrefsIO.SaveLastSite: site='{siteName}'");
            }
            catch (Exception e)
            {
                KSCLog.Error($"KSCPrefsIO.SaveLastSite: failed to save: {e.Message}");
            }
        }
    }
}
