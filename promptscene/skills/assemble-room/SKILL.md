---
name: assemble-room
description: >
  Assemble a PromptScene ROOM **skeleton** in the XumFlow **studio** project and LIVE-PROVE it with
  a QuickTest: clone a sample room, register it in the Content Manager (Addressables), build the 4
  skeleton layers (SYSTEMS with RoomCore + player spawner / ENVIRONMENT / UI / empty FEATURES),
  safely reparent the FishNet scene spawner, then run QuickTest (host) and verify the §6.5 signals
  (avatar spawns, RoomCore up with an empty registry + 4 services, WASD-ready). SKELETON ONLY — it
  does NOT add features or the COMPOSITIONS layer (that is add-component's job). Use when the user wants
  to scaffold a new empty room, e.g. "build a room called X", "assemble AssembleTest_1 and prove it".
  Argument = new room name (no extension), optionally "<Room> from <BaseRoom>". e.g. /assemble-room MyRoom_1
---

# Assemble a studio ROOM skeleton and QuickTest-prove it

Builds a working, **empty** room skeleton in **studio** (`c:\J_0\XumFlow-studio`, hot-update/Addressables model)
and proves it live with a single-editor host QuickTest. It was frozen from the hand-done "structure cleanup" that
produced `PromptSceneRoom_1`, and it reproduces that room's skeleton (5 layers, minus the ones studio legitimately
omits) for any new name.

**This skill wraps the procedure — it does NOT restate it.** The one source of truth for every step is
`${CLAUDE_PLUGIN_ROOT}/docs/build-studio-room.md` (§0 model, §1 clone+register, §2 RoomCore, §3 layers + SceneId
safety, §4 QuickTest). Read it when a step's *why* is unclear. Contract §1 (5-layer convention) is in
`${CLAUDE_PLUGIN_ROOT}/docs/promptscene-content-contract.md`.

**Argument:** `<Room>` = new room leaf name, no extension (default `AssembleTest_N`). Optional base:
`/assemble-room <Room> from <BaseRoom>` (default base = the sample `T_RoomB`). `<Room>` is used as the scene name,
the Addressables address, and the QuickTest `roomSceneKey`.

## Scope — skeleton only (boundary, enforce strictly)
IN:
- Clone base sample room → `<Room>.unity` (byte copy preserves the spawner SceneId).
- Register in Content Manager (Addressables leaf address + `RoomScene` label).
- Build the **4 skeleton layers**: `===== SYSTEMS =====` (RoomCore + `Player/--PLAYER_SPAWNER`),
  `===== ENVIRONMENT =====` (base lights/floor/primitives), `===== UI =====` (base canvases),
  `===== FEATURES =====` (**empty**).
- Safe reparent of the FishNet scene spawner (build-studio-room §3, 4-step SceneId check).
- QuickTest §6.5 auto-judge (avatar spawns / RoomCore + empty registry + 4 services).

OUT (do NOT do these — they belong to `add-component`):
- ⛔ **No `===== COMPOSITIONS =====` layer.** It is created only when a COMPOSITION is added (no empty layer without demand — launchpad/D2/loop-3 lesson).
- ⛔ **No FEATURE code/content.** The `FEATURES` folder stays empty.
- ⛔ No server exe build, no 2-client deploy, no Smart-Deploy (studio hot-update; deploy = `build-studio-deploy.md`, not yet written).

## Ground rules (honesty contract)
- studio MCP (`ai-game-developer`) must be connected. Drive Unity via MCP; do not hand-edit `.unity`.
- Do **not** scene-save `QuickStart.unity` — QuickTest edits it in memory only (Setup/Teardown restore it).
- The proof this skill can give = **skeleton assembled + §6.5** (avatar spawns, RoomCore/registry/services). FEATURE/COMPOSITION behavior and 2-client parity are **out** (other skills / V2).
- If a step is blocked, do **not** work around it — read `build-studio-room.md`, report, and wait.

## Key resources (studio, paths stable)
- Sample base rooms: `Assets/App/Scenes/T_RoomB.unity` (default), `T_RoomA.unity`
- Boot scene (NetworkManager + QuickTestStarter): `Assets/App/Scenes/QuickStart.unity`
- Core: `Assets/App/Scripts/ContentLogic/PromptScene/Core/` (`RoomCore` etc., in `App.HotUpdate`)
- QuickTest result file (Read after Check): `c:\J_0\XumFlow-studio\Temp\ps_qt_result.txt`
- Assets (set `ROOM`/`BASE` const at the top of each before running via `script-execute`):
  `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/{duplicate_and_register,build_skeleton,verify_quicktest}.cs`

---

## EXECUTE

### Phase 1 — Clone + register (build-studio-room §1)
Set `ROOM` (and `BASE` if the user named a base) in `assets/duplicate_and_register.cs`, then `script-execute`
`PS_DuplicateAndRegister.Run`. It byte-copies the base → `Assets/App/Scenes/<Room>.unity` (spawner SceneId preserved)
and writes the Addressables entry (leaf address, `RoomScene` label, `Default Local Group`) — the direct write that
skips the GUI Apply login-gate a local baseline doesn't need. Confirm the log: `copied=True` + `registered address=<Room>`.

### Phase 2 — Build the skeleton layers (build-studio-room §2–§3)
1. `scene-open Assets/App/Scenes/<Room>.unity` **Single** — keep it open (persistent open scene is what lets the
   FishNet `sceneSaving` hook preserve the spawner SceneId; do NOT do create→place→save in one shot).
2. Set `ROOM` in `assets/build_skeleton.cs`, then `script-execute` `PS_BuildSkeleton.Run`. It creates the 4 headers,
   adds **RoomCore** under SYSTEMS (auto-adds SimpleClickProvider + registers 4 services in Awake), moves base
   canvases → UI and lights/floor/primitives → ENVIRONMENT, and does the **safe reparent** of `--PLAYER_SPAWNER`
   into `SYSTEMS/Player` then `SaveScene`.
3. Confirm the log read-back: all 4 headers present, `COMPOSITIONS absent=True`, `RoomCore under SYSTEMS=True`,
   `FEATURES child count=0`, and `SceneId-safe=True` (SceneId!=0 && IsSceneObject=True). If `SceneId-safe=False`,
   stop and read build-studio-room §3 (do not proceed to QuickTest — the avatar would silently fail to spawn).

### Phase 3 — QuickTest §6.5 (build-studio-room §4)
Set `ROOM` in `assets/verify_quicktest.cs`, then:
1. `scene-open Assets/App/Scenes/QuickStart.unity` **Single**.
2. `script-execute` `PS_VerifyQuickTest.Setup` (server + host + roomSceneKey=<Room>; snapshots originals).
3. `script-execute` set `EditorApplication.isPlaying = true`. Wait ~12–15s (server start → Addressables room load → spawn).
4. `script-execute` `PS_VerifyQuickTest.Check`, then **Read** `c:\J_0\XumFlow-studio\Temp\ps_qt_result.txt`.
5. `script-execute` set `EditorApplication.isPlaying = false`.
6. `script-execute` `PS_VerifyQuickTest.Teardown` (restores QuickStart in memory; disk untouched).

`console-get-logs` floods with a benign "2 event systems" warning — judge from the result file + scene/object
queries, filtering to Errors only.

---

## VERIFY — §6.5 acceptance (all must pass)

| # | Pass condition | Where |
|---|---|---|
| 1 | Room `<Room>` is a loaded scene (Addressables leaf load) | result file S1 |
| 2 | `Desktop(Clone)` avatar spawned (proves spawner SceneId valid; studio has no lobby — boot scene) with `IsOwner=True` + motion/controller rig | result file S2 |
| 3 | `RoomCore.Instance` initialized, `Contents.All count=0` (empty — skeleton), services = 4 (`IEventBus,IInteraction,INetSpawn,IRoomUserState`) | result file S3 |
| — | `=== §6.5 SKELETON VERDICT: PASS ===` | result file |

If avatar missing but room loaded → SceneId churn (Phase 2 §3 failed). If RoomCore.Instance null → Core not compiled
into ContentLogic. If registry non-empty → something added a feature (boundary violated).

## Cleanup
Exit Play if still running; delete the Temp txt files. Leave `<Room>.unity` + its Addressables entry in place.

## Report
Give the §6.5 table with the actual result-file values, and state plainly PASS/FAIL. Confirm the boundary held:
FEATURES empty, no COMPOSITIONS layer. Note that features/compositions are `add-component`'s job and 2-client/deploy are out of scope.
