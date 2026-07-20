---
name: compose-room
description: >
  Compose a working PromptScene ROOM from a one-line natural-language request by SELECTING existing
  FEATURE modules from the Content catalog, ASSEMBLING them into one RoomCore-bearing room, and
  LIVE-PROVING it: rebuild Room.exe, run Master+Room servers, join from an editor client, and auto-judge
  BOTH the SYSTEMS §6.5 signals (avatar spawns, lobby unloads, WASD-ready) AND the contract §5 FEATURES
  signals (each feature self-registers + SetEnabled exception-free + Meta valid). This is the ORCHESTRATOR:
  it does NOT re-implement assemble-room / scaffold-content — it reference-calls their procedures. Use when
  the user wants a room described by capability rather than by name, e.g. "측정 도구 있는 룸 만들어줘",
  "make a room with a ruler and a click-spawner", "/compose-room a collaboration room with measuring".
  Argument = a natural-language room request.
---

# Compose a ROOM from a prompt and live-prove it

This skill is the **composition orchestrator**. It turns *"a room with X"* into a selection of already-built
FEATURE modules, assembles them onto a verified SYSTEMS base, and proves the result runs. It is the layer
**above** `assemble-room` (SYSTEMS) and `scaffold-content` (one FEATURE).

**Argument:** a natural-language room request. Derive the room name if the user gives one (`ComposedRoom_N`
otherwise) and the desired capabilities.

## What compose-room OWNS vs. DELEGATES (design contract — do not violate)
- **Owns exactly two things:** (1) **natural-language → feature selection** (PARSE·RESOLVE·PLAN), and
  (2) **multi-part execution orchestration + the final composite verdict** (EXECUTE·VERIFY).
- **Delegates everything else by reference-call:** at each mechanical step, **read the referenced skill's
  SKILL.md and follow its procedure** — do NOT inline-copy its assembly steps into this file (SSOT; prevents
  drift). The two assets this skill ships (`build_composed_room.cs`, `verify_composition.cs`) are only the
  N-feature *generalizations* of the single-feature assets those skills already own; the invariants/procedures
  stay owned by them.

## What this proves — and what it does NOT (honesty contract)
- ✅ **Proves (structural):** the selected features compile; the composed room's SYSTEMS still works (avatar
  spawns, lobby unloads — §6.5); and **every** feature in the plan self-registers to `RoomCore.Instance`,
  toggles `SetEnabled(true/false)` exception-free, and has valid `Meta` (§5) — all IN the live networked room.
- ❌ **Does NOT prove** the features *do what the prose intended*, that they *interact sensibly*, or that the
  room *looks good*. Behaviour, cross-feature interaction, and aesthetics are not structurally verifiable
  (see `${CLAUDE_PLUGIN_ROOT}/docs/promptscene-launchpad-attempt.md`) — they need a human / vision loop.
- v1 composes **orthogonal** features (measure + spawn + memo). Features that must *talk to each other* are the
  COMPOSITIONS layer, deliberately shelved (see `${CLAUDE_PLUGIN_ROOT}/docs/design-directions-2026-07.md` D2).

## Read first (SSOT)
- **Contract:** `${CLAUDE_PLUGIN_ROOT}/docs/promptscene-content-contract.md` — §1 hierarchy, §2 interfaces, §5 FEATURES checks, §6.5 SYSTEMS signals, composition-plan schema.
- **Referenced procedures:** `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/SKILL.md` (SYSTEMS scene + build/run/join/verify), `${CLAUDE_PLUGIN_ROOT}/skills/scaffold-content/SKILL.md` (RoomCore + FEATURES placement, §5 verify).
- Unity is driven via the `ai-game-developer` MCP tools; servers via PowerShell. If MCP is disconnected, reconnect first.

## Key resources (stable paths)
- Feature catalog source: `Assets/PromptScene/Content/*/*.cs` (each `IToggleableContent` — **no separate manifest**).
- compose-room assets: `${CLAUDE_PLUGIN_ROOT}/skills/compose-room/assets/{build_composed_room.cs,verify_composition.cs}`.
- Reused from assemble-room: `${CLAUDE_PLUGIN_ROOT}/skills/assemble-room/assets/{build_room.cs,run_servers.ps1,drive_matchmaking.cs,verify_client.cs}`.
- Composed room output: `Assets/App/Scenes/<Room>.unity` (default `ComposedRoom_N`).
- Composition record: `${CLAUDE_PLUGIN_ROOT}/skills/compose-room/composition-plan.json` (written each run; diffable; separates plan bugs from execution bugs).

---

## Stage 1 — PARSE (owned)

From the user's request extract:
- **roomName** — if the user named it, use it; else `ComposedRoom_N` (next free `N` under `Assets/App/Scenes/`).
- **desired capabilities** — the phrases describing what the room should do ("측정 도구", "measuring", "spawn cubes", "메모").
- **per-feature params** — any tunables the user mentioned (reserved; v1 leaves `params` empty and warns if the user asked for a value the catalog feature doesn't expose).

Do not match to features yet — that is RESOLVE.

## Stage 2 — RESOLVE (owned)

Build the **feature catalog from source** (the two-consumers rule forbids a separate manifest file). For each
`Assets/PromptScene/Content/**/*.cs` that implements `IToggleableContent`, read directly:
- class name (= feature type for the builder), `Id` (`public string Id => "…"`), and the `ContentMeta` literal:
  `DisplayName`, `Category`, `DefaultOn`, `MutuallyExclusive`.

Then:
1. **Match** each desired capability against the catalog by `Category` / `DisplayName` / `Id` / obvious synonyms
   (e.g. "측정 도구"/"measuring" → Category `측정` → `RulerContent`, id `ruler`). Record matched features.
2. **Unresolved** — any capability with no catalog match goes into `unresolved[]` (do NOT invent a class).
3. **Conflicts** — if two selected features list each other (or a shared token) in `MutuallyExclusive`, record
   in `conflicts[]`.

## Stage 3 — PLAN (owned, and a HARD STOP gate)

Write `composition-plan.json` (schema in contract) **before** executing:
```json
{ "roomName": "ComposedRoom_1", "mode": "create",
  "features": [ { "id": "ruler", "class": "RulerContent", "params": {} } ],
  "unresolved": [], "conflicts": [] }
```
- `mode` is fixed to `"create"` in v1 (field reserved for future `extend`).
- **If `unresolved` OR `conflicts` is non-empty, STOP here and report to the human** — do NOT auto-chain to
  `scaffold-content` and do NOT drop conflicting features silently. Ask, e.g. *"'메모'는 카탈로그에 없습니다 —
  scaffold-content로 새로 만들까요, 아니면 이 기능 없이 진행할까요?"*, then wait.
- Only when `unresolved` and `conflicts` are both empty do you proceed to EXECUTE.

## Stage 4 — EXECUTE (owned orchestration; mechanics delegated)

> **Reference-call, do not duplicate.** The scene-assembly invariants (4 R-prefabs, NO `R-PlayerSpawner`,
> C1 `DefaultPrefabObjects`, C3 online/offline, hierarchy §1) are owned by **assemble-room SKILL.md Phase 1**;
> the RoomCore-under-SYSTEMS-so-features-self-register step is owned by **scaffold-content Phase B**. Read those
> before running so you know what the asset reproduces and what to check.

1. **Assemble** the composed room in one pass with `assets/build_composed_room.cs` (set `ROOM` = plan.roomName,
   `FEATURE_TYPES` = plan.features[].class), run via `script-execute` (className `CR_BuildComposedRoom`,
   methodName `Run`). Confirm the log: `C1=True C3online=True C3offline=True RoomCore=True features[<Class>=True,…]
   sceneIdsGenerated=1`. If any feature reports `=MISSING`, its `.cs` didn't compile — fix (or it isn't really in
   the catalog) and stop. If `sceneIdsGenerated=0/-1`, FishNet SceneId assignment failed → the avatar will silently
   not spawn in VERIFY (contract §1); do not proceed until it reports ≥1.
   - ⚠️ Never `EditorOnly`-tag a folder header that holds children (contract §1) — the asset leaves them untagged.
   - **Wire feature prefab fields (orchestration post-step, M4 backfeed):** the generic builder only AddComponents —
     it cannot know per-feature serialized prefab fields. After assembly, for each planned feature assign its
     prefab reference via `SerializedObject` and re-save the scene BEFORE the Room.exe rebuild (current catalog:
     `ChatContent.channelPrefab`←`Content/Chat/ChatChannel.prefab`, `RulerContent.measurementPrefab`←
     `Content/Ruler/RulerMeasurement.prefab`, `GrabbableProps.grabbablePrefab`←`Content/GrabbableProps/GrabbableProp.prefab`).
     §5 passes even unwired (graceful warning), so a missed wire shows up only when the feature actually runs.
2. **Rebuild Room.exe** hosting the room — reuse **assemble-room** `assets/build_room.cs` (`BR_BuildRoom.Run`,
   set `ROOM`). FishNet scene network objects were just placed → the rebuild is mandatory (contract §1 warning).
   - ⚠️ Wait for the editor to be idle (`EditorApplication.isCompiling==false`) before building — a fresh `.cs`
     scene + FishNet postprocess otherwise fails with "scripts are compiling". A cold build is 10min+ and blocks
     MCP ("Response data is null") — **poll `Room/Room_Data/level0` + `application.cfg` LastWriteTime, not MCP**.
   - Only set `ALSO_BUILD_MASTER=true` if the machine LAN IP changed (compare `MasterAndSpawner/application.cfg`
     `mstMasterIp` vs current `Get-NetIPAddress`).

## Stage 5 — VERIFY (owned composite verdict; probes delegated)

Run servers, join, then judge BOTH layers in the live room.

1. **Servers + join** — reuse assemble-room `assets/run_servers.ps1` (Master → 6s → Room; expect
   `Online Scene: <Room>` + `Room registered successfully`), then `scene-open Client.unity` **Single** (never
   additively load `<Room>`), check `C-ClientMasterConnector` base `serverIp` == master IP, enter Play, wait ~12s,
   drive `assets/drive_matchmaking.cs` (`BR_DriveMatchmaking.Run`). Wait ~10s for the avatar to spawn.
2. **SYSTEMS §6.5** — reuse assemble-room `assets/verify_client.cs` (`BR_VerifyClient.Run`, set `ROOM`), Read
   `br_verify.txt`. Signals 1–4: `Client N has become a player` (room.log) / Client unloaded + `MovedObjectsHolder`
   scene + active==`<Room>` / `Desktop(Clone)` `IsOwner==True` + child camera / `DummyController`+follower+`NetworkTransform`.
3. **FEATURES §5 (every feature)** — run `assets/verify_composition.cs` (`CR_VerifyComposition.Run`, set
   `FEATURE_IDS` = plan.features[].id), Read `C:\J_0\cr_verify_composition.txt`. Per feature: (A) RoomCore present,
   (B) `GetById(id)` returns the content = self-registered, (C) `SetEnabled` on/off/double-on exception-free +
   `IsEnabled` tracks, (D) `Meta.DisplayName`/`Category` non-empty. File ends `=== §5 COMPOSITION VERDICT: PASS|FAIL ===`.

**Composite PASS** = SYSTEMS 1–4 all pass **AND** §5 verdict PASS for every feature in the plan.

Failure map: feature `NOT FOUND` → RoomCore missing / feature not under FEATURES / didn't compile.
`SetEnabled … THREW` → the feature violates R2/R3/R4 (fix in scaffold-content, then recompose). SYSTEMS #1
"Failed to confirm the access" → C2; lobby covers room → C3 offline empty; avatar invisible → C1 mismatch.

---

## Cleanup
Exit Play (`isPlaying=false`), `Stop-Process -Name Room,MasterAndSpawner -Force`, delete `br_verify.txt` +
`C:\J_0\cr_verify_composition.txt`. Leave `<Room>.unity`, the rebuilt `Room.exe`, and `composition-plan.json` in place.

## Report
Show the **composition-plan.json** (what was selected, and any unresolved/conflicts), then BOTH acceptance
tables with actual values: the §6.5 SYSTEMS signals (quote the room.log "become a player" line) and the §5
per-feature verdict. State the honesty contract plainly: **structural/contract conformance only is proven —
behaviour, cross-feature interaction, and aesthetics are NOT verified** (suggest a human/screenshot check).
