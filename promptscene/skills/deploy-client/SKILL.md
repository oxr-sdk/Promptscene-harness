---
name: deploy-client
description: >
  Build (and, for Android targets, deploy) the XRCollabDemo CLIENT app for a target platform,
  reproducing the verified Meta Quest flow in docs/build-meta-client.md. Argument = platform:
  Meta (default) | XReal | Tablet | Vision. Applies the matching Xum Build Kit device preset,
  persists the required scripting defines, includes the room scene in the build list, bakes the
  master IP into application.cfg, disables the room's flat camera for VR, builds via BuildPipeline,
  and (Android) installs+launches over adb then verifies the master connection. Use when the user
  wants to build or deploy the client for a headset/device, e.g. "/deploy-client Meta",
  "build the Quest client", "make an XReal build".
---

# Build & deploy the XRCollabDemo CLIENT for a platform

This skill reproduces the verified end-to-end client flow (built to Quest 3 and confirmed
master-connect → room-join → move). Mechanics live in **`${CLAUDE_PLUGIN_ROOT}/docs/build-meta-client.md`** — read it;
this skill parameterizes it per platform and adds preflight/deploy.

**Argument:** `<Platform>` ∈ `Meta` (default) | `XReal` | `Tablet` | `Vision`.

## Platform matrix (the extensible part)
| `<Platform>` | Preset name | Preset dir / BuildTarget | XR loader | Build on Windows? | Deploy |
|---|---|---|---|---|---|
| **Meta** (default) | `Meta-v1.3.4` | `Android` / `BuildTarget.Android` | OpenXR + Meta Quest | ✅ **verified** | `adb` (Quest) |
| **XReal** | `XREAL-v1.3.4` | `Android` / `BuildTarget.Android` | OpenXR + XREAL | ✅ likely (Android, same path) — not yet live-verified | `adb` |
| **Tablet** | *(none shipped — create one)* | `Android` / `BuildTarget.Android` | none (flat, no XR loader) | ✅ once a preset exists | `adb` |
| **Vision** | `Apple-v1.3.4` | `VisionOS` / `BuildTarget.VisionOS` | PolySpatial | ❌ **requires macOS + Xcode + `com.unity.polyspatial.visionos`** | Xcode → device |

Presets live at `XumBuildKit/{CustomProjectSettings,CustomXRSettings,Plugins}/<PresetDir>/<Preset>/`
(installed via *Xum Build Kit → Settings → Device Presets → Import Demo Device Presets*).

## Ground rules
- Drive Unity via the `ai-game-developer` MCP tools; adb/servers via PowerShell. **If MCP is disconnected, reconnect first.**
- **Vision on Windows → STOP.** If `<Platform>=Vision` and the OS is Windows, do not attempt the build — visionOS needs a Mac + Xcode + PolySpatial. Report that and offer to prep the preset/settings only.
- **Tablet has no shipped preset.** If `<Platform>=Tablet` and no `Tablet-*` preset exists, either create an Android (non-XR) preset first (XR Plug-in Management: no loader) or tell the user. Don't silently reuse an XR preset.
- Reuse today's verified escapes from `build-meta-client.md`; don't re-derive.

---

## Phase 0 — Preflight (abort early with a clear reason)
1. **Resolve platform → preset/target** from the matrix. Unknown arg → ask.
2. **OS gate:** target `VisionOS` on non-macOS → stop (see Ground rules).
3. **Preset exists:** `XumBuildKit/CustomProjectSettings/<PresetDir>/<Preset>` present. Missing (e.g. Tablet) → stop with guidance.
4. **Module installed:** for Android, `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/{NDK,SDK,OpenJDK,Variations/il2cpp}` all exist. For VisionOS, the visionOS module (Mac only).
5. **Room scene:** determine the room scene to bundle (the room server's online scene — read `R-RoomServer.DefaultScene._onlineScene` or the current active room, e.g. `BasicRoom_3`). The client build **must** include it (§2.4-C of the doc).
6. **Compile 0 errors** (`console-get-logs` Error → no `error CS`). Input handling should be **New (1)**; if it's **Both**, note the Android build warning (dismissable) or switch to New — but New requires legacy-Input code to be ported (`DummyController` is already ported).

## Phase 1 — Apply preset + persist defines  (`${CLAUDE_PLUGIN_ROOT}/skills/deploy-client/assets/build_client.cs`, set `PLATFORM`)
1. `EditorUserBuildSettings.SwitchActiveBuildTarget(<Group>, <Target>)`.
2. `XumBuildKit.Editor.Utility.XRSettingsUtility.LoadPreset("<Preset>", "<PresetDir>")` — copies the per-platform XR settings (correct OpenXR loader) to `Assets/XR`. **This is what makes the XR provider actually take (works around the XumBuildKit "provider not saved on build" bug).**
3. **Persist scripting defines FIRST** (else the pipeline's `extraScriptingDefines` triggers a domain reload that kills the MCP build — doc §2.4-A/B). Add `UNIXR_USE_FISHNET;EDGEGAP_PLUGIN_SERVERS` to the target's defines via `PlayerSettings.SetScriptingDefineSymbols` + `AssetDatabase.SaveAssets()`, then **wait for recompile** (`isCompiling==false`) before Phase 2. `build_client.cs` is idempotent: if the defines are missing it sets+saves and returns "recompile then re-run"; if present it proceeds to build.

## Phase 2 — Configure + build  (same `${CLAUDE_PLUGIN_ROOT}/skills/deploy-client/assets/build_client.cs`, second pass)
1. **Scenes:** `{ "Assets/App/Scenes/Client.unity", "<RoomScene>.unity" }` (Client = index 0 boot). Doc §2.4-C.
2. **Package id:** distinct, e.g. `com.kisti.xrcollabdemo` (avoids the default `urpblank` signature clash — doc §4). `PlayerSettings.SetApplicationIdentifier`.
3. **application.cfg** → `Assets/StreamingAssets/application.cfg` with `-mstStartClientConnection=True`, `-mstMasterIp=<LAN IP>`, `-mstMasterPort=5000`. Use the **master PC's LAN IP** (never 127.0.0.1 for a remote device). Also inject IP into `Client.unity`'s `ClientToMasterConnector` (serverIp/serverPort) with scene backup→restore.
4. **VR camera fix (Meta/XReal/Vision only):** disable the room scene's `Main Camera` (Camera + AudioListener) and set its tag Untagged — else it fights the XR rig camera → flicker/frozen (doc §2.4-D). Tablet (flat) skips this.
5. **Build:** `BuildPipeline.BuildPlayer(opts)` with `options=None` and **no `extraScriptingDefines`** (defines already persisted). Output `Builds/App/Client/<Preset>/XRCollabDemo.apk` (Android) / Xcode project (VisionOS). Blocks main thread → MCP "Response data is null" is normal; confirm by artifact.

**Success check:** `result==Succeeded`, APK exists. APK zip contains `lib/arm64-v8a/{libil2cpp.so, libopenxr_loader.so, libunity.so}`, `AndroidManifest.xml`, `assets/application.cfg`. Build log shows the platform's OpenXR feature hooks (`ModifyAndroidManifestMeta/…OnPostGenerateGradleAndroidProject`).

## Phase 3 — Deploy (Android targets)  (`${CLAUDE_PLUGIN_ROOT}/skills/deploy-client/assets/deploy_android.ps1`)
adb at `<Unity>/…/AndroidPlayer/SDK/platform-tools/adb.exe`.
1. Wait for authorization (Quest often reverts to `unauthorized` after sleep/replug → user taps **Allow USB debugging** in-headset).
2. `adb install -r <apk>`. Signature clash `INSTALL_FAILED_UPDATE_INCOMPATIBLE` → **rename package + rebuild** (never uninstall the existing app — see memory `prefer-rename-over-uninstall`).
3. Launch: `adb shell monkey -p <pkg> -c android.intent.category.LAUNCHER 1`; confirm `adb shell pidof <pkg>`.
> **Vision:** no adb — open the generated Xcode project on a Mac and deploy. This skill stops after Phase 2 and hands off.

## Phase 4 — Verify connection
- Client logcat: `[Info | ClientToMasterConnector] connected to server at: <IP>:5000`.
- master.log: `Peer [N] opened connection` + `Client N connected`. netstat: device IP `ESTABLISHED` to `:5000`.
- Room join: **different account per client** (editor + device both as guest → guest-id collision → "Room could not validate you" — doc §7). For a remote device the room must be registered with the LAN `RoomIp` (`Room.exe -mstRoomIp <LAN IP>`; doc §6).

---

## Adding a new platform (extensibility recipe)
1. Import/author the Xum Build Kit device preset under `XumBuildKit/.../<PresetDir>/<Preset>` (XR Plug-in Management set correctly for that device).
2. Add a row to the matrix above (preset, BuildTarget, XR loader, buildable-here, deploy).
3. In `${CLAUDE_PLUGIN_ROOT}/skills/deploy-client/assets/build_client.cs`, add the `case "<Platform>"` mapping (preset, presetDir, target, isXR, isFlat).
4. Verify once end-to-end, then mark the row ✅ verified.

## Report
State the platform, preset, and each Phase result: preflight gate, build `result` + APK path/size, install `Success`, and the quoted `connected to server` log line. If anything was skipped (e.g. Vision on Windows), say so plainly.
