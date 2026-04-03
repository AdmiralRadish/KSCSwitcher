# KSCSwitcher — Detail Instructions

---

## USER INSTRUCTIONS
- **Always read `co_instructions.md` (workspace root) and this `detail_instructions.md` before acting on any prompt.**

---

## Project Overview

- KSCSwitcher allows players to change the KSC launch site to any configured location.
- **Source:** `KSCSwitcher-2.2.0.0/Source/` — forked and modified for LMP compatibility
- Solution: `Source/KSCSwitcher.sln`
- Target: .NET Framework 4.7.2

---

## Build

```powershell
dotnet build KSCSwitcher-2.2.0.0/Source/KSCSwitcher.csproj -c Release /p:KSPRoot="G:\Steam\steamapps\common\Kerbal Space Program"
```

Output: `GameData/KSCSwitcher/Plugins/KSCSwitcher.dll`

**Not currently in the workspace build tasks** — must be built manually or tasks.json should be updated.

## Deploy

DLL: `<KSP>\GameData\KSCSwitcher\Plugins\KSCSwitcher.dll`

---

## Key Architecture

### KSCLoader.cs
Plain C# class (not MonoBehaviour). Static `instance` created once at MainMenu by `ScenarioSpawn`, persists for entire KSP process.

**`OnGameStateCreated(Game game)`** — The main entry point for site restoration:
1. `LastKSC.CreateSettings(game)` — deduplicates PSMs, adds SPACECENTER to targetScenes
2. `ReadSiteFromPsm(m)` — reads site from PSM's ConfigNode via reflection (bypasses OnLoad)
3. **In-memory fallback** — if PSM has no site key, uses `Sites.lastSite` (cache from previous session)
4. Force-loads LastKSC module and calls OnLoad so `_allValues` is populated for future OnSave
5. Applies the site or defaults to `Sites.defaultSite`

**`ReadSiteFromPsm(ProtoScenarioModule m)`** — Reads the PSM's ConfigNode via reflection. Search priority:
1. Per-install key: `{installId}_LastLaunchSite` (e.g. `5376f74b_LastLaunchSite`)
2. Legacy key: `LastLaunchSite`
3. Wildcard: any `*_LastLaunchSite`

### LastKSC.cs
ScenarioModule that persists the last-selected launch site per-install.

**Key fields:**
- `lastSite` — current site name
- `_allValues` — dictionary preserving all ConfigNode values (multi-install safe)
- `_onLoadCalled` — guard preventing empty OnSave when OnLoad was skipped

**`GetSiteKey()`** — Returns `"{installId}_LastLaunchSite"` when LMP connected, `"SinglePlayer_LastLaunchSite"` otherwise.

**`OnLoad(ConfigNode)`** — Populates `_allValues`, reads site from per-install → legacy → wildcard keys. Guards against overwriting `Sites.lastSite` if already set.

**`OnSave(ConfigNode)`** — Writes `_allValues` + current site. Skipped if `_onLoadCalled` is false (prevents empty data overwriting server).

**`CreateSettings(Game)`** — Deduplicates LastKSC PSMs (LMP creates duplicates); picks PSM with best data via `ScorePsm()`; adds SPACECENTER to targetScenes.

**`ScorePsm(ProtoScenarioModule)`** — Peeks at a PSM's ConfigNode via reflection and returns a score: +1000 if any `*_LastLaunchSite` key exists, plus the raw value count. Used by `CreateSettings()` to pick the PSM with actual site data during deduplication.

### KSCSwitcher.cs
MonoBehaviour for the tracking station UI. `SetSite()` updates both `KSCLoader.instance.Sites.lastSite` and `LastKSC.fetch.lastSite`.

---

## LMP Integration

### Scenario Sync Pipeline
- LMP syncs scenarios every 30 seconds via `ScenarioSystem.SendScenarioModules()`
- Calls `scenarioModule.Save(configNode)` → triggers our `OnSave()`
- Hash-dedup skips unchanged modules
- Server does COMPLETE REPLACE via `RawConfigNodeInsertOrUpdate`

### Known Issues & Fixes

1. **`Game.Start()` loads scenarios BEFORE `onGameStateCreated`** — `targetScenes` can't be patched in time. Fix: read PSM ConfigNode directly via reflection (`ReadSiteFromPsm`), don't rely on `OnLoad()`.

2. **LMP disconnect sends scenarios AFTER disconnecting** — `DisconnectFromGame()` called `Disconnect()` before `SendScenarioModules()`. **Fixed**: swapped order so scenarios are sent while connection is alive.

3. **PSM has no site key on reconnect** — The 30-second sync interval means site changes within the last 30s before disconnect are lost. **Fixed**: in-memory fallback uses `Sites.lastSite` which persists in the static `KSCLoader.instance` across reconnects. Self-healing: on next 30s sync after reconnect, the correct key is written and sent to server.

4. **Duplicate PSMs — wrong one kept** — LMP adds server PSMs first via `scenarios.Add()`, then KSP may add a disk PSM from persistent.sfs. Old code kept index 0 blindly (stale/empty disk PSM) and discarded index 1+ (server PSM with site keys). **Fixed**: `CreateSettings()` now scores each PSM via `ScorePsm()` — +1000 for having a `*_LastLaunchSite` key, plus raw value count — and keeps the highest-scoring one. This also prevented a destructive cascade where the empty PSM's `OnSave()` would overwrite server data with just the default site, wiping all other players' keys.

5. **Scene mismatch** — Server stores `scene=8` (TRACKSTATION only). **Fixed**: `CreateSettings()` adds SPACECENTER to targetScenes at runtime.

### Install ID
First 8 chars of `SystemInfo.deviceUniqueIdentifier`. Each machine gets a unique scenario key so multiplayer sync doesn't clobber another player's site preference.

---

## Important Gotchas

- **`KSCLoader.instance` is static** — survives scene changes and LMP reconnects. `Sites.lastSite` is the in-memory cache that bridges the gap when server data is stale.
- **`_onLoadCalled` guard is critical** — Without it, an empty OnSave would overwrite the server's good data with nothing during scenarios where OnLoad was never called (scene mismatch).
- **`_allValues` preserves other installs' data** — When one player saves, other players' `*_LastLaunchSite` keys are preserved in the dictionary and written back.

---

## Change Log

| Date | Change | Files |
|------|--------|-------|
| Various | LMP-compatible per-install site keys, PSM deduplication, scene-gating bypass | `KSCLoader.cs`, `LastKSC.cs` |
| 2026-03-27 | Added in-memory fallback: `Sites.lastSite` reused when PSM lacks site key on reconnect | `KSCLoader.cs` |
| 2026-03-27 | Fixed LMP `DisconnectFromGame`: send scenarios BEFORE disconnecting | `LunaMultiplayerRSS-source/LmpClient/MainSystem.cs` |
| 2026-03-29 | Fixed PSM dedup: keep PSM with best data (site keys) instead of index 0. Prevents cascade where empty PSM overwrites server data on next sync. Added `ScorePsm()` helper. | `LastKSC.cs` |
| 2026-03-31 | Removed noisy "no site key found in PSM data" log from `ReadSiteFromPsm()`. This was a benign fallback path when PSM has non-site values — caller already logs its own message ("PSM had no site key; using cached lastSite=…"). | `KSCLoader.cs` |
