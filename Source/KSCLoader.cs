using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace regexKSP
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class ScenarioSpawn : MonoBehaviour
    {
        void Start()
        {
            KSCLoader.instance ??= new KSCLoader();
            enabled = false;
        }
    }

    public class KSCLoader
    {
        public static KSCLoader instance = null;
        public KSCSiteManager Sites = new KSCSiteManager();

        private void OnGameStateCreated(Game game)
        {
            LastKSC.CreateSettings(game);
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                ProtoScenarioModule m = HighLogic.CurrentGame.scenarios.FirstOrDefault(m => m.moduleName == "LastKSC");

                if (m == null) return;

                // Read the site directly from the PSM's stored ConfigNode.
                // We can't rely on ProtoScenarioModule.Load() calling OnLoad()
                // because Game.Start() already called Load() before we could
                // patch targetScenes — the cached moduleRef means OnLoad() is
                // never re-invoked for SPACECENTER.
                string siteName = ReadSiteFromPsm(m);

                // In-memory fallback: if the server PSM lacks the site key (common
                // after LMP reconnect — the 30-second sync may not have fired
                // before disconnect), reuse the cached value from the previous
                // session.  KSCLoader.instance is static and survives reconnects.
                if (string.IsNullOrEmpty(siteName) && !string.IsNullOrEmpty(Sites.lastSite))
                {
                    siteName = Sites.lastSite;
                    Debug.Log("[KSCSwitcher] PSM had no site key; using cached lastSite=" + siteName);
                }

                // Also ensure the LastKSC module is loaded and has data for OnSave
                LastKSC l = m.moduleRef as LastKSC;
                if (l == null)
                    l = (LastKSC)m.Load(ScenarioRunner.Instance);
                if (l != null && string.IsNullOrEmpty(l.lastSite))
                {
                    // Force OnLoad so the module has _allValues populated for OnSave.
                    // Find the PSM's ConfigNode using the same reflection as ReadSiteFromPsm.
                    var reflFlags = System.Reflection.BindingFlags.Instance |
                                    System.Reflection.BindingFlags.Public |
                                    System.Reflection.BindingFlags.NonPublic;
                    ConfigNode psmData = null;
                    foreach (var f in m.GetType().GetFields(reflFlags))
                    {
                        if (f.FieldType == typeof(ConfigNode))
                        {
                            psmData = f.GetValue(m) as ConfigNode;
                            if (psmData != null && psmData.values.Count > 0) break;
                        }
                    }
                    if (psmData != null)
                    {
                        l.OnLoad(psmData);
                        Debug.Log("[KSCSwitcher] forced OnLoad from PSM, lastSite=" + l.lastSite);
                    }
                }

                if (!string.IsNullOrEmpty(siteName))
                {
                    ConfigNode site = Sites.GetSiteByName(siteName);
                    if (site != null)
                    {
                        KSCSwitcher.SetSiteAndResetCamera(site);
                        Sites.lastSite = siteName;
                        if (l != null) l.lastSite = siteName;
                        Debug.Log("[KSCSwitcher] set the launch site to the last site, " + siteName);
                        return;
                    }
                    Debug.LogWarning("[KSCSwitcher] site '" + siteName + "' not found in site list");
                }

                // Fallback to default
                if (!string.IsNullOrEmpty(Sites.defaultSite))
                {
                    ConfigNode site = Sites.GetSiteByName(Sites.defaultSite);
                    if (site != null)
                    {
                        KSCSwitcher.SetSiteAndResetCamera(site);
                        Sites.lastSite = Sites.defaultSite;
                        if (l != null) l.lastSite = Sites.defaultSite;
                        Debug.Log("[KSCSwitcher] set the initial launch site to the default " + Sites.defaultSite);
                    }
                }
            }
        }

        /// <summary>
        /// Reads the last launch site from the PSM's stored ConfigNode, bypassing
        /// ScenarioModule.OnLoad() entirely. Uses the same key priority as LastKSC.OnLoad().
        /// </summary>
        private static string ReadSiteFromPsm(ProtoScenarioModule m)
        {
            var flags = System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;

            ConfigNode bestData = null;
            int bestValueCount = -1;
            foreach (var field in m.GetType().GetFields(flags))
            {
                if (field.FieldType == typeof(ConfigNode))
                {
                    var cn = field.GetValue(m) as ConfigNode;
                    if (cn != null && cn.values.Count > bestValueCount)
                    {
                        bestValueCount = cn.values.Count;
                        bestData = cn;
                    }
                }
            }

            if (bestData == null)
            {
                Debug.LogWarning("[KSCSwitcher] could not find ConfigNode data on PSM");
                return null;
            }

            // Per-install key (LMP multiplayer)
            string key = LastKSC.GetSiteKey();
            if (bestData.HasValue(key))
            {
                string val = bestData.GetValue(key);
                if (!string.IsNullOrEmpty(val))
                {
                    Debug.Log("[KSCSwitcher] found site '" + val + "' via key '" + key + "'");
                    return val;
                }
            }

            // Legacy single key
            if (bestData.HasValue("LastLaunchSite"))
            {
                string val = bestData.GetValue("LastLaunchSite");
                if (!string.IsNullOrEmpty(val))
                {
                    Debug.Log("[KSCSwitcher] found site '" + val + "' via legacy key");
                    return val;
                }
            }

            // Wildcard: any *_LastLaunchSite
            foreach (ConfigNode.Value v in bestData.values)
            {
                if (v.name.EndsWith("_LastLaunchSite") && !string.IsNullOrEmpty(v.value))
                {
                    Debug.Log("[KSCSwitcher] found site '" + v.value + "' via wildcard key '" + v.name + "'");
                    return v.value;
                }
            }

            return null;
        }

        public KSCLoader()
        {
            GameEvents.onGameStateCreated.Add(OnGameStateCreated);
        }
    }
}
