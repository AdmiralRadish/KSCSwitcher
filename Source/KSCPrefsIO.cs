using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace regexKSP
{
    /// <summary>
    /// Reads and writes the last-selected launch site to an XML file in PluginData,
    /// keyed by save folder name so each save game tracks its own site independently.
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
        /// Returns the current save folder name, or "_global" when no save is loaded.
        /// </summary>
        private static string GetSaveKey()
        {
            string folder = HighLogic.SaveFolder;
            return string.IsNullOrEmpty(folder) ? "_global" : folder;
        }

        /// <summary>
        /// Finds the &lt;Save name="key"&gt; element, or null if it doesn't exist.
        /// </summary>
        private static XmlElement FindSaveElement(XmlElement root, string saveKey)
        {
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node is XmlElement el
                    && el.Name == "Save"
                    && el.GetAttribute("name") == saveKey)
                {
                    return el;
                }
            }
            return null;
        }

        /// <summary>
        /// Loads an XML document from the prefs path and migrates the old flat format
        /// to per-save format if necessary. Returns null if the file doesn't exist.
        /// </summary>
        private static XmlDocument LoadDocument()
        {
            string path = GetPrefsPath();
            if (!File.Exists(path))
                return null;

            var doc = new XmlDocument();
            try { doc.Load(path); }
            catch { return null; }

            XmlElement root = doc.DocumentElement;
            if (root == null) return null;

            // Migrate old flat format: move bare <LastLaunchSite> into <Save name="_global">
            XmlElement legacyEl = root["LastLaunchSite"];
            if (legacyEl != null)
            {
                string legacySite = legacyEl.InnerText;
                root.RemoveChild(legacyEl);

                XmlElement globalSave = doc.CreateElement("Save");
                globalSave.SetAttribute("name", "_global");
                XmlElement siteEl = doc.CreateElement("LastLaunchSite");
                siteEl.InnerText = legacySite;
                globalSave.AppendChild(siteEl);
                root.AppendChild(globalSave);

                try { doc.Save(path); }
                catch { /* best effort */ }

                KSCLog.Verbose($"KSCPrefsIO: migrated flat format -> _global save (site='{legacySite}')");
            }

            return doc;
        }

        /// <summary>
        /// Load the last launch site name for the current save. Returns empty string if nothing saved yet.
        /// </summary>
        public static string LoadLastSite()
        {
            XmlDocument doc = LoadDocument();
            if (doc == null)
            {
                KSCLog.Verbose("KSCPrefsIO.LoadLastSite: no prefs file exists yet.");
                return "";
            }

            try
            {
                string saveKey = GetSaveKey();
                XmlElement root = doc.DocumentElement;
                XmlElement saveEl = FindSaveElement(root, saveKey);
                if (saveEl == null)
                {
                    KSCLog.Verbose($"KSCPrefsIO.LoadLastSite: no entry for save '{saveKey}'.");
                    return "";
                }

                XmlElement lastSiteEl = saveEl["LastLaunchSite"];
                string site = lastSiteEl?.InnerText ?? "";
                KSCLog.Verbose($"KSCPrefsIO.LoadLastSite: save='{saveKey}', site='{site}'");
                return site;
            }
            catch (Exception e)
            {
                KSCLog.Warn($"KSCPrefsIO.LoadLastSite: error reading prefs: {e.Message}");
            }

            return "";
        }

        /// <summary>
        /// Save the last launch site name for the current save.
        /// </summary>
        public static void SaveLastSite(string siteName)
        {
            string path = GetPrefsPath();

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                string saveKey = GetSaveKey();

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

                // Migrate old flat format if present
                XmlElement legacyEl = root["LastLaunchSite"];
                if (legacyEl != null)
                {
                    string legacySite = legacyEl.InnerText;
                    root.RemoveChild(legacyEl);

                    XmlElement globalSave = doc.CreateElement("Save");
                    globalSave.SetAttribute("name", "_global");
                    XmlElement legacySiteEl = doc.CreateElement("LastLaunchSite");
                    legacySiteEl.InnerText = legacySite;
                    globalSave.AppendChild(legacySiteEl);
                    root.AppendChild(globalSave);
                }

                XmlElement saveEl = FindSaveElement(root, saveKey);
                if (saveEl == null)
                {
                    saveEl = doc.CreateElement("Save");
                    saveEl.SetAttribute("name", saveKey);
                    root.AppendChild(saveEl);
                }

                XmlElement lastSiteEl = saveEl["LastLaunchSite"];
                if (lastSiteEl == null)
                {
                    lastSiteEl = doc.CreateElement("LastLaunchSite");
                    saveEl.AppendChild(lastSiteEl);
                }
                lastSiteEl.InnerText = siteName;

                doc.Save(path);
                KSCLog.Verbose($"KSCPrefsIO.SaveLastSite: save='{saveKey}', site='{siteName}'");
            }
            catch (Exception e)
            {
                KSCLog.Error($"KSCPrefsIO.SaveLastSite: failed to save: {e.Message}");
            }
        }
    }
}
