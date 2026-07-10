---
name: scaffold-content
description: >
  Scaffold a NEW PromptScene FEATURE module from a natural-language description and LIVE-PROVE it
  end-to-end. Generates a contract-conforming IToggleableContent from the frozen Ruler template
  (self-registers to RoomCore, depends only on PromptScene.Core), drops it into a fresh RoomCore-bearing
  test room under ===== FEATURES =====, rebuilds Room.exe, runs the Master+Room servers, joins from an
  editor client, and verifies BOTH the SYSTEMS §6.5 signals (avatar spawns, lobby unloads) AND the
  contract §5 FEATURES signals (self-registered + SetEnabled(true/false) exception-free + Meta valid)
  IN the live networked room. Use when the user wants a new room feature/content module from a prompt,
  e.g. "add a laser pointer feature", "scaffold a spawn-a-cube-where-I-click tool", "/scaffold-content marker".
  Argument = a natural-language feature description (and optionally a name).
---

# Scaffold a PromptScene FEATURE and live-prove it

This skill freezes the **Ruler pilot** (`Assets/PromptScene/Content/Ruler/RulerContent.cs`) into a repeatable
procedure: turn a natural-language ask into a contract-conforming FEATURE, then **prove it actually runs** in a
real networked room. It is the FEATURES analogue of `assemble-room` (which proves the SYSTEMS layer).

**Argument:** a natural-language feature description. Optionally lead with a name, e.g.
`/scaffold-content laser-pointer — a laser that draws a beam from the camera to where I click`.
Derive: `<FeatureClass>` (PascalCase, ends `Content`), `<feature-id>` (lower-kebab), `<DisplayName>`, `<Category>`.

## What this proves — and what it does NOT (honesty contract)
- ✅ **Proves**: compiles clean; the feature **self-registers** to `RoomCore.Instance.Contents` in a live
  networked room; `SetEnabled(true/false)` (and a double-on) are **exception-free**; `Meta` is valid; and the
  room itself still works (avatar spawns, lobby unloads — SYSTEMS unbroken by the new content).
- ❌ **Does NOT prove** the feature *does what the prose intended*, nor that it looks good. Behavioural
  correctness and aesthetics are **not** structurally verifiable (see `${CLAUDE_PLUGIN_ROOT}/docs/promptscene-launchpad-attempt.md`) —
  they need a human / vision loop. Report the structural verdict plainly; do not claim the feature "works" beyond §5.
- The room is built from the **verified docs**, not by copying an existing feature room. Only `RulerContent.cs`
  is read, as the reference the template is derived from.

## Contract & dependencies (read first)
- **Spec (SSOT):** `${CLAUDE_PLUGIN_ROOT}/docs/promptscene-content-contract.md` — §2 interfaces, §3 lifecycle, §5 FEATURES checks, §1 hierarchy.
- **Room mechanics:** `${CLAUDE_PLUGIN_ROOT}/docs/build-working-room.md` (§1–§5 scene) + `${CLAUDE_PLUGIN_ROOT}/docs/build-xumlobby-server.md` (§2-B build, §4 run).
- Unity is driven via the `ai-game-developer` MCP tools; servers via PowerShell. If MCP is disconnected, reconnect first.
- FEATURE code compiles into Assembly-CSharp (PromptScene has no asmdef), alongside `PromptScene.Core`.

## Key resources (stable paths)
- Feature template: `${CLAUDE_PLUGIN_ROOT}/skills/scaffold-content/assets/FeatureContent.cs.template`
- Reference feature: `Assets/PromptScene/Content/Ruler/RulerContent.cs` (the pattern being frozen)
- Core contract: `Assets/PromptScene/Core/` (`Contracts.cs`, `RoomCore.cs`, `RoomContentRegistry.cs`)
- Room-scene build assets (reused from assemble-room): `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/{build_room.cs,run_servers.ps1,drive_matchmaking.cs,verify_client.cs}`
- Test room output: `Assets/App/Scenes/<Room>.unity` (default `FeatureLab_1`)

---

## Phase A — Generate the FEATURE from the prompt

1. Read the template `${CLAUDE_PLUGIN_ROOT}/skills/scaffold-content/assets/FeatureContent.cs.template` and its HARD RULES (R1–R5).
2. From the NL description, decide `<FeatureClass>`, `<feature-id>`, `<DisplayName>`, `<Category>`, `<DefaultOn>` (usually false).
3. Copy the template to `Assets/PromptScene/Content/<Feature>/<FeatureClass>.cs`, replace every `__TOKEN__`, and fill
   ONLY the `===== FEATURE LOGIC (edit) =====` regions. Leave all CONTRACT PLUMBING byte-for-byte.
   - Input/spawn services come **only** from `_core.TryGet<T>` in `TryResolveServices()` (R2, v0.2 contract): e.g. `_core.TryGet<IInteraction>(out _interaction)` then `_interaction.AddClick(OnClick)`. If a required service is missing, warn + stay disabled (return false) — never throw.
   - Track runtime spawns with `Track(...)`; `OnDeactivate()` must undo `OnActivate()` and `ClearSpawned()` (R4).
   - Any helper MonoBehaviour = a separate **top-level** class in the same file (R5).
   - **No** reference to other FEATURES / RoomManager / FishNet / XumLobby / platform Input (R1, R2).
4. Create the folder + `.cs` via the MCP asset tools (or write the file then `assets-refresh`). Then confirm
   **compile 0 errors**: `console-get-logs(Error)` has no `error CS`. Fix and re-check before proceeding.

> If the ask needs server-authoritative shared state (scores, roles — "mafia" style), that is a **networked**
> feature beyond this template's local scope (LocalNetSpawn / desktop click). Say so and stop at a local stand-in;
> the networked-feature pattern is shelved (see docs/promptscene-launchpad-attempt.md §기술적으로 배운 것 4).

---

## Phase B — Build a RoomCore test room with the feature under FEATURES

Use `${CLAUDE_PLUGIN_ROOT}/skills/scaffold-content/assets/build_feature_room.cs` (set `ROOM` + `FEATURE_TYPE`), run via `script-execute`
(className=`SC_BuildFeatureRoom`, methodName=`Run`). In one pass it: creates `<Room>.unity`; instantiates the **4**
R- prefabs (NOT `R-PlayerSpawner`) + `Room-PlayerSpawner`; applies **C1** (`_spawnablePrefabs=DefaultPrefabObjects`)
and **C3** (`_onlineScene=<Room>`, `_offlineScene=Client.unity`); adds a **RoomCore** under SYSTEMS; adds the
generated feature component on a GameObject under `===== FEATURES =====`; builds Floor+walls+camera+light;
organizes the standard hierarchy (contract §1); saves; registers `Client`+`<Room>` in EditorBuildSettings.

- ⚠️ **Never `EditorOnly`-tag a folder parent that holds children** (children get build-excluded — contract §1). The script leaves headers untagged.
- Confirm the log line: `C1=True C3online=True C3offline=True RoomCore=True feature(<Class>)Added=True`. If
  `feature ... NOT found`, the `.cs` didn't compile — return to Phase A.

---

## Phase C — Rebuild Room.exe hosting `<Room>` (build-xumlobby-server.md §2-B)

Reuse `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/build_room.cs` — set `ROOM = "<Room>"`, run via `script-execute`
(className=`BR_BuildRoom`, methodName=`Run`). Only set `ALSO_BUILD_MASTER=true` if the machine LAN IP changed
(compare `MasterAndSpawner/application.cfg` `mstMasterIp` vs current `Get-NetIPAddress`; note multi-NIC machines can
auto-detect a different IP — if Room's cfg `mstMasterIp` ends up ≠ the master's, align them or set the client `serverIp` to match).
- Success = fresh `LastWriteTime` on `Room/application.cfg` **and** `Room/Room_Data/level0` (the tiny `Room.exe`
  bootstrap may keep its old timestamp). `BuildPipeline` blocks the main thread → the MCP call may return
  "Response data is null"; that's normal — confirm via the artifacts.
- ⚠️ **Wait for the editor to be idle before building.** Right after Phase A/B (adding a `.cs` + the FishNet prefab
  postprocess), a build can fail with `Error building Player because scripts are compiling` — BuildPlayer switches to
  the server scripting-defines and recompiles Assembly-CSharp (now incl. your new feature). Guard with
  `EditorApplication.isCompiling==false && !isUpdating` before invoking, and if it fails, just retry once the editor settles.
- ⚠️ **A cold Room build can take 10+ minutes and blocks MCP entirely** (every MCP call returns "Response data is null"
  while `BuildPipeline` holds the main thread). Do NOT diagnose this as a hang — **poll the filesystem** (`level0`/`cfg`
  `LastWriteTime`), not MCP responsiveness. Flat Unity CPU during this window is normal (I/O-bound serialization of ~170MB).

---

## Phase D — Run servers + editor client joins

1. Servers: `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/run_servers.ps1` (Master → 6s → Room). Verify:
   `master.log` `listening to .*:5000` + `Spawner successfully created`; `room.log` `Online Scene: <Room>` +
   `Room registered successfully`. (A `room.log` warning that `Client.unity` couldn't load on the server is non-fatal.)
2. Client: `scene-open Assets/App/Scenes/Client.unity` **Single** (do NOT additively load `<Room>` — that spins a
   second local server). Check `C-ClientMasterConnector` base `ConnectionHelper.serverIp` == master IP from the cfg;
   if not, set it and `scene-save` (in-memory reverts on Play domain reload).
3. Enter Play (`EditorApplication.isPlaying=true`), wait ~12s for connect + guest auth.
4. Matchmaking: `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/drive_matchmaking.cs` (`BR_DriveMatchmaking.Run`) —
   guest-auth → `FindGames` → `MatchmakingBehaviour.StartMatch(games[0])`. (⚠️ `GameInfoPacket` has no `Ip`/`Port`.)

---

## Phase E — Verify (SYSTEMS §6.5 + FEATURES §5, both in the live room)

Wait ~10s after StartMatch, then run two editor snapshots:

**SYSTEMS (§6.5)** — `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/verify_client.cs` (`BR_VerifyClient.Run`, set `ROOM`). Confirms the
new content didn't break the platform:

| # | Pass | Where |
|---|---|---|
| 1 | `room.log`: `Client N has become a player` | room.log |
| 2 | Client scene unloaded + `MovedObjectsHolder` scene; active == `<Room>` (C3) | snapshot |
| 3 | `Desktop(Clone)` with `NetworkObject`, `IsOwner==True`, child camera active | snapshot |
| 4 | avatar has `DummyController`/head-follower/`NetworkTransform` (WASD-ready) | snapshot |

**FEATURES (§5)** — `${CLAUDE_PLUGIN_ROOT}/skills/scaffold-content/assets/verify_feature.cs` (`SC_VerifyFeature.Run`, set `FEATURE_ID`),
then Read `C:\J_0\sc_verify_feature.txt`:

| # | Pass condition |
|---|---|
| A | `RoomCore.Instance != null` (RoomCore lives in the live networked room) |
| B | `registry.GetById("<feature-id>")` returns the content = **self-registered** |
| C | `SetEnabled(true/false)` + double-on **exception-free**; `IsEnabled` tracks (true→then→false) |
| D | `Meta.DisplayName` and `Meta.Category` non-empty |

The file ends with `=== §5 FEATURES VERDICT: PASS|FAIL ===`. All of A–D **and** SYSTEMS 1–4 must pass.

Failure map: `registered ... NOT FOUND` → RoomCore missing / feature not under FEATURES / didn't compile.
`SetEnabled ... THREW` → R2/R3/R4 violated (touched platform input, non-idempotent, or bad teardown) — fix the
feature's `OnActivate/OnDeactivate` and re-run from Phase A. SYSTEMS #1 "Failed to confirm the access" → C2; lobby
covers room → C3 offline empty; avatar invisible → C1 mismatch.

---

## Cleanup
Exit Play (`isPlaying=false`), `Stop-Process -Name Room,MasterAndSpawner -Force`, delete `C:\J_0\sc_verify_feature.txt`
and any `br_verify.txt`. Leave the generated `<FeatureClass>.cs`, `<Room>.unity`, and the rebuilt `Room.exe` in place.

## Report
Give BOTH acceptance tables with the actual values: the §5 FEATURES verdict (A–D) and the §6.5 SYSTEMS signals
(quote the room.log "become a player" line). State plainly that behavioural correctness/aesthetics are out of
scope for structural verification and, if relevant, suggest a human/screenshot check of the feature's actual effect.
