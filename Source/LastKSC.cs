using System.Collections.Generic;
using UniLinq;
using UnityEngine;

/******************************************************************************
 * Copyright (c) 2014~2016, Justin Bengtson
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice,
 * this list of conditions and the following disclaimer.
 *
 * 2. Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation
 * and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 ******************************************************************************/

namespace regexKSP
{
    // Taniwha graciously offered the use of this code/method for saving our settings per save game.
    // I've changed where appropriate and reformatted because of 1TBS.
    public class LastKSC : ScenarioModule
    {
        public string lastSite = "";
        private static LastKSC instance;

        // Preserve all ConfigNode values so other installs' data is retained on save.
        private readonly Dictionary<string, string> _allValues = new Dictionary<string, string>();

        private static string _installId;
        private static bool? _lmpLoaded;
        private static System.Reflection.PropertyInfo _networkStateProp;

        /// Short per-install identifier (first 8 chars of Unity's deviceUniqueIdentifier).
        /// Each KSP install produces a different key, so multiplayer scenario sync
        /// won't clobber another player's last-site preference.
        private static string InstallId
        {
            get
            {
                if (_installId == null)
                {
                    string raw = SystemInfo.deviceUniqueIdentifier ?? "local";
                    _installId = raw.Length > 8 ? raw.Substring(0, 8) : raw;
                }
                return _installId;
            }
        }

        /// <summary>True when LunaMultiplayer client DLL is loaded (checked once, cached).</summary>
        private static bool IsLmpLoaded
        {
            get
            {
                if (_lmpLoaded == null)
                {
                    var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "LmpClient");
                    _lmpLoaded = asm != null;
                    if (asm != null)
                    {
                        var mainSys = asm.GetType("LmpClient.MainSystem");
                        if (mainSys != null)
                            _networkStateProp = mainSys.GetProperty("NetworkState",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    }
                }
                return _lmpLoaded.Value;
            }
        }

        /// <summary>
        /// True when LMP is loaded AND the client is connected to a server.
        /// Threshold is Starting (35) rather than Running (36) because OnLoad
        /// fires during Game.Start() — before the first Update() promotes
        /// the state to Running.  At Starting the scenario data has already
        /// been synced from the server, so the per-install key is correct.
        /// </summary>
        private static bool IsLmpConnected
        {
            get
            {
                if (!IsLmpLoaded || _networkStateProp == null) return false;
                int state = (int)_networkStateProp.GetValue(null, null);
                return state >= 35; // ClientState.Starting — scenarios already synced
            }
        }

        /// <summary>
        /// Returns the ConfigNode key for this install's last launch site.
        /// Connected to LMP server: "ab12cd34_LastLaunchSite" — unique per machine
        /// so scenario sync won't clobber another player's preference.
        /// Singleplayer or LMP not connected: "SinglePlayer_LastLaunchSite" — portable across machines.
        /// </summary>
        internal static string GetSiteKey()
        {
            return IsLmpConnected
                ? InstallId + "_LastLaunchSite"
                : "SinglePlayer_LastLaunchSite";
        }

        public static LastKSC fetch
        {
            get
            {
                if (instance == null)
                {
                    Game g = HighLogic.CurrentGame;
                    instance = g.scenarios.Select(s => s.moduleRef).OfType<LastKSC>().FirstOrDefault();
                }
                return instance;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private bool _onLoadCalled = false;

        public override void OnLoad(ConfigNode config)
        {
            _onLoadCalled = true;
            // Preserve all values so other installs' data survives the save cycle.
            // Skip KSP metadata keys — the framework writes these itself on save.
            _allValues.Clear();
            foreach (ConfigNode.Value v in config.values)
                if (v.name != "name" && v.name != "scene")
                    _allValues[v.name] = v.value;

            string key = GetSiteKey();
            if (config.HasValue(key))
                lastSite = config.GetValue(key);
            else if (config.HasValue("LastLaunchSite"))
                lastSite = config.GetValue("LastLaunchSite"); // migrate from legacy single key
            else
            {
                // Fallback: pick any *_LastLaunchSite value (covers timing edge
                // cases where IsLmpConnected returns the wrong answer during load).
                foreach (var kvp in _allValues)
                {
                    if (kvp.Key.EndsWith("_LastLaunchSite") && !string.IsNullOrEmpty(kvp.Value))
                    {
                        lastSite = kvp.Value;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(lastSite))
            {
                // Only update Sites.lastSite if it's empty or matches what we read.
                // When LMP is connected and OnGameStateCreated already set the correct
                // site, a stale duplicate PSM's OnLoad must not clobber it.
                string current = KSCLoader.instance.Sites.lastSite;
                if (string.IsNullOrEmpty(current) || current == lastSite)
                    KSCLoader.instance.Sites.lastSite = lastSite;
                else
                    Debug.Log("[KSCSwitcher] OnLoad skipped Sites.lastSite update (current=" + current + ", loaded=" + lastSite + ")");
            }
        }

        public override void OnSave(ConfigNode config)
        {
            // If OnLoad() was never called (e.g. scene mismatch), don't write
            // empty data that would overwrite the server's good data.
            if (!_onLoadCalled)
            {
                Debug.Log("[KSCSwitcher] LastKSC.OnSave() skipped — OnLoad was never called");
                return;
            }

            string key = GetSiteKey();
            _allValues[key] = lastSite;

            foreach (var kvp in _allValues)
                config.AddValue(kvp.Key, kvp.Value);
        }

        public static void CreateSettings(Game game)
        {
            // Deduplicate: LMP's Game.Start() can reload from disk and create a second
            // LastKSC PSM alongside the original server-synced one. The stale duplicate
            // causes OnLoad() to overwrite Sites.lastSite with old data.
            var allLastKSC = game.scenarios.Where(p => p.moduleName == typeof(LastKSC).Name).ToList();
            if (allLastKSC.Count > 1)
            {
                // Keep the first (server-authoritative) PSM, remove the rest.
                for (int i = 1; i < allLastKSC.Count; i++)
                    game.scenarios.Remove(allLastKSC[i]);
                Debug.Log("[KSCSwitcher] removed " + (allLastKSC.Count - 1) + " duplicate LastKSC PSM(s)");
            }

            var existing = game.scenarios.FirstOrDefault(p => p.moduleName == typeof(LastKSC).Name);
            if (existing == null)
            {
                ProtoScenarioModule proto = game.AddProtoScenarioModule(typeof(LastKSC), GameScenes.TRACKSTATION, GameScenes.SPACECENTER);
                proto.Load(ScenarioRunner.Instance);
            }
            else if (!existing.targetScenes.Contains(GameScenes.SPACECENTER))
            {
                // Server/save may store scene=8 (TRACKSTATION only). Add SPACECENTER so
                // ProtoScenarioModule.Load() won't skip OnLoad during SpaceCenter load.
                existing.targetScenes.Add(GameScenes.SPACECENTER);
            }
        }
    }
}
