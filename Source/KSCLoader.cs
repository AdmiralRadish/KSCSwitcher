using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace regexKSP
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class ScenarioSpawn : MonoBehaviour
    {
        void Start()
        {
            KSCLog.Verbose("ScenarioSpawn.Start: ensuring KSCLoader singleton.");
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
            KSCLog.Verbose($"KSCLoader.OnGameStateCreated: scene={HighLogic.LoadedScene}");
            LastKSC.CreateSettings(game);
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                // Load last site from XML prefs (per-player)
                string savedSite = KSCPrefsIO.LoadLastSite();
                KSCLog.Verbose($"KSCLoader: XML prefs returned lastSite='{savedSite}'");

                if (!string.IsNullOrEmpty(savedSite))
                {
                    ConfigNode site = Sites.GetSiteByName(savedSite);
                    if (site != null)
                    {
                        Sites.lastSite = savedSite;
                        KSCSwitcher.SetSiteAndResetCamera(site);
                        KSCLog.Log($"KSCLoader: restored last site '{savedSite}' from XML prefs.");
                        return;
                    }
                    KSCLog.Warn($"KSCLoader: saved site '{savedSite}' not found in config, falling back to default.");
                }

                // Fall back to default site
                if (!string.IsNullOrEmpty(Sites.defaultSite))
                {
                    ConfigNode site = Sites.GetSiteByName(Sites.defaultSite);
                    if (site == null)
                    {
                        KSCLog.Error($"KSCLoader: default site '{Sites.defaultSite}' not found in config!");
                        return;
                    }
                    Sites.lastSite = Sites.defaultSite;
                    KSCSwitcher.SetSiteAndResetCamera(site);
                    KSCLog.Log($"KSCLoader: set initial launch site to default '{Sites.defaultSite}'.");
                }
            }
        }

        public KSCLoader()
        {
            KSCLog.Log("KSCLoader: constructor, registering onGameStateCreated.");
            GameEvents.onGameStateCreated.Add(OnGameStateCreated);
        }
    }
}
