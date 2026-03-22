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
    /// <summary>
    /// Migration shim: reads old scenario data on load and migrates it to the XML prefs file.
    /// Kept as a ScenarioModule so existing saves don't throw errors on load.
    /// All new persistence goes through KSCPrefsIO instead.
    /// </summary>
    public class LastKSC : ScenarioModule
    {
        public string lastSite = "";

        public override void OnLoad(ConfigNode config)
        {
            if (config.HasValue("LastLaunchSite"))
            {
                lastSite = config.GetValue("LastLaunchSite");
            }

            // Migrate old scenario data to the XML file if no XML pref exists yet
            if (!string.IsNullOrEmpty(lastSite))
            {
                string xmlSite = KSCPrefsIO.LoadLastSite();
                if (string.IsNullOrEmpty(xmlSite))
                {
                    KSCLog.Log($"LastKSC.OnLoad: migrating '{lastSite}' from scenario to XML prefs.");
                    KSCPrefsIO.SaveLastSite(lastSite);
                }
            }
        }

        public override void OnSave(ConfigNode config)
        {
            // No-op: persistence is handled by KSCPrefsIO now.
            // We intentionally don't write back to the scenario to avoid LMP conflicts.
        }

        /// <summary>
        /// Ensures the scenario module exists in the game so old saves can load without errors.
        /// </summary>
        public static void CreateSettings(Game game)
        {
            if (!game.scenarios.Any(p => p.moduleName == typeof(LastKSC).Name))
            {
                ProtoScenarioModule proto = game.AddProtoScenarioModule(typeof(LastKSC), GameScenes.TRACKSTATION);
                proto.Load(ScenarioRunner.Instance);
                KSCLog.Verbose("LastKSC.CreateSettings: registered scenario module for migration.");
            }
        }
    }
}
