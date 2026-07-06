---
name: build-working-room
description: >
  Assemble a working PromptScene ROOM scene in XRCollabDemo from docs/build-working-room.md and
  LIVE-PROVE it end-to-end: apply the C1–C4 invariants, rebuild Room.exe with the new scene, run
  Master+Room servers, join from an editor client, and verify the §6.5 runtime signals
  (avatar spawns, lobby unloads, WASD-ready). Use when the user wants to scaffold a new room
  (e.g. "build a room called X", "assemble BasicRoom_N and prove it works") and confirm it actually runs.
  Argument = the new room name (no extension), e.g. /build-working-room BasicRoom_3
---

# Build a working ROOM and live-prove it

This skill reproduces a verified, end-to-end run: assemble `<Room>.unity` purely from
`c:\J_0\docs\build-working-room.md`, then build/run/verify per its §6 (build mechanics delegated to
`c:\J_0\docs\build-xumlobby-server.md`). It was validated by building **BasicRoom_2** from scratch and
seeing `Client N has become a player` with an owner avatar in the networked room.

**Argument:** `<Room>` = new room name without extension (default `BasicRoom_N`). Used everywhere below.

## Ground rules (honesty contract)
- Build the scene **only** from `build-working-room.md`. Do **NOT** open or copy any existing room
  (e.g. `BasicRoom.unity`) — the whole point is proving the doc alone suffices. Use only the prefabs/assets
  the doc names by path.
- The build/run step legitimately reads `build-xumlobby-server.md` because §6.1 of the room doc delegates to it.
- Unity is driven via the `ai-game-developer` MCP tools; servers via PowerShell. If MCP is disconnected, reconnect first.

## Key resources (paths are stable in this project)
- R- system prefabs: `Packages/com.kisti.xumlobby/Runtime/Prefabs/2) Room Scene/` → `R-RoomServer`, `R-RoomClient`, `R-ConnectionToMaster`, `R-MasterCanvas`
- Player spawner prefab (reused by every room): `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab`
- Prefab collection (C1): `Assets/DefaultPrefabObjects.asset`
- Client scene (C3 offline): `Assets/App/Scenes/Client.unity`
- Master scene (build): `Assets/App/Scenes/Master.unity`
- Server build output: `c:\J_0\XRCollabDemo\Builds\App\Server\StandaloneWindows64\{MasterAndSpawner,Room}\`
- Scene hierarchy convention: `c:\J_0\docs\promptscene-content-contract.md` §1 (also summarized in build-working-room.md §5). **You MUST build the room to this hierarchy, not flat.**

---

## Phase 1 — Assemble `<Room>.unity` (build-working-room.md §1–§5)

1. `scene-create` at `Assets/App/Scenes/<Room>.unity`, `Single`, `DefaultGameObjects` (gives Main Camera + Directional Light = §4 camera/light).
   - ⚠️ **For VR client (Quest) deploy**, this Main Camera must later be disabled (Camera+AudioListener) + untagged — it conflicts with the persistent XR rig camera (flicker/frozen view). Fine to keep enabled for the editor-client verification below. See `c:\J_0\docs\build-meta-client.md` §2.4-D.
2. Instantiate the **4** R- prefabs via `assets-prefab-instantiate` (§1). **Do NOT** add `R-PlayerSpawner` (5th) — forbidden by C2.
3. Instantiate `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab` (§2, this is C2 — a prefab instance gets a valid scene id; a script-built NetworkObject would fail with "Failed to confirm the access").
4. Apply invariants on **R-RoomServer**:
   - **C1**: `NetworkManager._spawnablePrefabs` must be **DefaultPrefabObjects**. ⚠️ Fresh instance defaults to `SinglePrefabObjects` — you MUST replace it. Use `gameobject-component-modify` pathPatches:
     `[{"Path":"_spawnablePrefabs","Value":{"typeName":"FishNet.Managing.Object.DefaultPrefabObjects","value":{"instanceID":<id of DefaultPrefabObjects.asset>}}}]`
   - **C3**: `DefaultScene._onlineScene = "Assets/App/Scenes/<Room>.unity"`, `_offlineScene = "Assets/App/Scenes/Client.unity"` (jsonPatch). Empty offline → lobby never disappears.
5. Verify `R-RoomClient.RoomClientManager.offlineRoomScene == "Client"` (prefab default — usually already correct).
6. §4 environment: a **Plane named Floor** (Plane primitive ships a MeshCollider — required for movement) + 4 wall cubes. Camera/Light already present.
7. **Organize into the standard hierarchy (promptscene-content-contract.md §1 / build-working-room.md §5).** Do NOT leave objects flat. Create empty "folder" parents and reparent into them:
   ```
   ===== SYSTEMS =====        ├─ Network  : R-RoomServer, R-RoomClient, R-ConnectionToMaster
                              └─ Player   : Room-PlayerSpawner            (RoomCore optional — omit in minimal room)
   ===== ENVIRONMENT =====    Floor, Wall_*, Directional Light, Main Camera
   ===== UI =====             R-MasterCanvas
   ===== FEATURES =====       (empty in minimal room)
   ===== _DYNAMIC =====       (empty — runtime spawns land here)
   ```
   ⚠️ **Never tag a folder parent that holds children with `EditorOnly`** — its children get excluded from the build (contract §1). Leave these parents untagged. Reparenting prefab-instantiated scene objects is safe (scene id stays valid on save); do it at edit time then save. Easiest done in one `script-execute` (create empties + reparent). See `assets/build_hierarchy.cs`.
8. `scene-save`.
9. Read back the saved values with a `script-execute` reflection check (private fields need reflection; the MCP field view often returns empty for base-class fields). Confirm: C1=DefaultPrefabObjects, C3 online/offline correct, offlineRoomScene=Client, spawner present with `[XumPlayerSpawner,XumSimpleSpawnServerExample,NetworkObject,XumNetwork]`, no `R-PlayerSpawner`, Floor has Collider, **and the 5 section headers exist with members reparented correctly**.
10. §3 registration: add `Client.unity` + `<Room>.unity` to `EditorBuildSettings.scenes` (in the same script).

See `assets/verify_scene.cs` for the reflection read-back + build-settings registration, and `assets/build_hierarchy.cs` for the hierarchy step (set `ROOM` in both).

---

## Phase 2 — Rebuild Room.exe with `<Room>` (build-xumlobby-server.md §2-B)

- Drive `XumLobbyServerBuilderWindow` by reflection via `script-execute`. Set `SceneList` to `<Room>.unity`
  (this is the room content) and `MasterScene` to `Master.unity`, then call `BuildRoom`.
- See `assets/build_room.cs` (set `ROOM`). Master is scene-independent — only rebuild it if the machine LAN IP changed
  (check: existing `MasterAndSpawner/application.cfg` `mstMasterIp` vs current `Get-NetIPAddress`). If it changed, also call `BuildMasterForWindows` and re-match the client serverIp.
- **Success check (§3):** `Room/application.cfg` AND `Room/Room_Data/level0` + `globalgamemanagers` get a fresh
  `LastWriteTime` (the tiny `Room.exe` bootstrap may keep its old timestamp — check `level0`, not the exe).
- `BuildPipeline` blocks the main thread; the MCP call may return "Response data is null" — that's normal, confirm via artifacts.

---

## Phase 3 — Run servers (build-working-room.md §6.2, build-xumlobby-server.md §4)

PowerShell: delete old logs, start `MasterAndSpawner.exe` (cwd = its folder, `-logFile master.log`), wait ~6s,
start `Room.exe` (cwd = its folder, `-logFile room.log`). See `assets/run_servers.ps1`.

Verify log signals:
- `master.log`: `listening to: <IP>:5000`, `Successfully initialized modules`, `Spawner successfully created`
- `room.log`: **`Online Scene: <Room>`** (proves the exe hosts your room), `Room Server started ... :7777`, **`Room registered successfully ... RoomName:Room-XXXX-...`**
- A `room.log` warning that offline scene `Client.unity` "couldn't be loaded" on the server is **non-fatal** (server has no Client scene in its build; room still registers).

---

## Phase 4 — Editor client joins (build-xumlobby-server.md §5)

1. `scene-open Assets/App/Scenes/Client.unity` **Single**. ⚠️ Load **Client ALONE** — do NOT additively load `<Room>`.
   Pre-loading the room runs its `R-RoomServer` in-editor = a second local server = conflict. The room arrives over the
   network on entry. (The room doc §6.3 says "Client+Room additive" but the build doc §5 correctly recommends Client-alone; follow Client-alone.)
2. Check `C-ClientMasterConnector` → base `ConnectionHelper.serverIp` (read via reflection) **== master IP** from the cfg.
   If not, set it and `scene-save` (⚠️ in-memory change reverts on Play domain reload → must save).
3. Enter Play: `script-execute` set `EditorApplication.isPlaying = true`. Wait ~12s for connect + guest auth
   (master.log shows a new `Peer [n] opened connection`).
4. Drive matchmaking via the MST API in `script-execute` (see `assets/drive_matchmaking.cs`):
   `Mst.Client.Auth` (SignInAsGuest if needed) → `Mst.Client.Matchmaker.FindGames(games => ...)` →
   find the `MatchmakingBehaviour` MonoBehaviour and invoke its `StartMatch(games[0])`.
   ⚠️ `GameInfoPacket` has **no** `Ip`/`Port` members — don't log them (use `Name/Id/Region/OnlinePlayers/MaxPlayers`).

---

## Phase 5 — Verify §6.5 (ALL four must pass)

Read `room.log` and run an editor `script-execute` snapshot (see `assets/verify_client.cs`, writes results to a temp txt you Read):

| # | Pass condition | Where |
|---|---|---|
| 1 | `room.log`: `Client N has become a player` (NOT "Failed to confirm the access" → that = C2 broken) | room.log |
| 2 | Client scene unloaded + a **`MovedObjectsHolder` scene** appears; active scene == `<Room>` (network-loaded) = lobby auto-dissolved (C3) | editor snapshot |
| 3 | `<Room>` has `Desktop(Clone)` with `NetworkObject`, **`IsOwner==True`**, child Camera active | editor snapshot |
| 4 | Room renders + WASD-ready: avatar has enabled `DummyController` + `DesktopAvatarHeadCameraFollower` + `NetworkTransform`. Optionally `screenshot-isolated` (isolated=false, Top/Front) on `Desktop(Clone)` to show floor/walls/shadow; optionally nudge `transform.position` to prove it's controllable. | editor snapshot / screenshot |

If #1 is "Failed to confirm the access" → C2 (spawner) wrong. If lobby covers room → C3 offline empty. If avatar invisible → C1 mismatch (room=SinglePrefabObjects vs client=DefaultPrefabObjects).

---

## Cleanup
Exit Play (`EditorApplication.isPlaying=false`), `Stop-Process -Name Room,MasterAndSpawner -Force`, delete temp txt files.
Leave `<Room>.unity` and the rebuilt `Room.exe` in place.

## Report
Give the §6.5 acceptance table with the actual quoted log line for #1 and the snapshot values for #2–#4. State plainly which signals passed.
