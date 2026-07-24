---
name: cross-platform-ui
description: >
  Lay a REUSABLE cross-platform pointing UI (World Space uGUI HUD) onto ANY RoomCore-bearing PromptScene room in the
  XumFlow **studio** project, and LIVE-PROVE it with a QuickTest. The HUD hardcodes no room: it walks the RoomCore
  registry and renders one ON/OFF button per toggleable FEATURE (plus a Ruler-only "측정 지우기" via runtime lookup),
  wires each button's onClick to the feature at runtime (contract §3b — serialized onClick resolves to target=null), and
  claims SuppressWorldClick so panel clicks don't leak to the floor. Modes (matching add-component §6): PC검증용 (mouse
  only — World Space PC or desktop-only Screen Space Overlay PCSS), PC+XR (adds TrackedDeviceGraphicRaycaster +
  XRWorldClicker for the shared Near-Far interactor), and 크로스플랫폼 대비 (same structure, cross-platform framing). All
  live-QuickTest-proven to desktop mouse + XR Simulator controller (real devices = V2). Procedure SSOT =
  build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS Korean font + SuppressWorldClick) and §6 (XRI
  world-click via SubmitExternalRay). This is a PART called by add-component §6; it can also be run directly, e.g.
  "/cross-platform-ui 크로스플랫폼용", "add a pointing UI to my room". Argument = mode (PC | PCSS | PCXR | Cross, default
  Cross) and optionally the target room.
---

# Lay a reusable cross-platform World Space UI onto a room and QuickTest-prove it

Turns the proven Ruler cross-platform HUD (migration §9 "Ruler 크로스플랫폼 UI", 2026-07-23) into a **reusable part**:
a World Space uGUI panel that binds itself from `RoomCore.Instance.Contents` — so it drops onto *any* room that has a
RoomCore, with no room-specific code. It is the **UI analogue** of `assemble-room` (SYSTEMS skeleton) and
`scaffold-content` (a FEATURE): those prove structure; this proves a cross-platform-ready pointing surface for whatever
FEATUREs the registry holds.

**This skill wraps the procedure — it does NOT restate it.** The one source of truth for every step and trap is
`${CLAUDE_PLUGIN_ROOT}/docs/build-studio-room.md` **§5** (World Space Canvas + `TrackedDeviceGraphicRaycaster` +
billboard + dynamic OS Korean font + `SuppressWorldClick`) and **§6** (XRI world-click: `XRWorldClicker` +
`SimpleClickProvider.SubmitExternalRay`, the shared Near-Far interactor, `deviceMode` sim). Contract §1 (5-layer /
registry) is in `${CLAUDE_PLUGIN_ROOT}/docs/promptscene-content-contract.md`. Read those when a *why* is unclear.

**Argument:** the mode, and optionally the room. `MODE` ∈ `PC` | `PCSS` | `PCXR` | `Cross` (default `Cross`).
`/cross-platform-ui Cross` or `/cross-platform-ui PCSS on PromptSceneRoom_1`. The room defaults to a QuickTest-verified
room that has content to bind (e.g. `PromptSceneRoom_1`, which has Ruler).

## The modes (identical registry-driven binder; the difference is the canvas + input plumbing)
| Mode | Label (add-component §6) | Canvas | Raycaster(s) | XRWorldClicker | Live-verified with |
|------|--------------------------|--------|--------------|----------------|--------------------|
| `PC`    | PC검증용 (World Space) | World Space | `GraphicRaycaster` only | no | desktop mouse (human) |
| `PCSS`  | PC검증용 (Screen Space) | **Screen Space Overlay** (desktop-only 2D HUD) | `GraphicRaycaster` only | no | ✅ desktop mouse (QuickTest, this session) |
| `PCXR`  | PC+XR (sim 검증) | World Space | + `TrackedDeviceGraphicRaycaster` | yes | + XR Simulator **controller** (human) |
| `Cross` | 크로스플랫폼 대비 (sim 검증) | World Space | + `TrackedDeviceGraphicRaycaster` | yes | ✅ same as PCXR (QuickTest, this session) |

- `PCXR` and `Cross` are **structurally identical** — controller *and* hand share the same Near-Far interactor
  (build-studio-room §6), so the same code covers hand tracking with 0 additions. `Cross` is the cross-platform *framing*;
  its extra coverage over `PCXR` (real hand/XREAL/tablet/Vision) is **V2**, not proven here (honesty contract).
- `PC` vs `PCSS` — both are mouse-only. `PC` keeps the **World Space** canvas (VR-portable later, build-studio-room §5
  default); `PCSS` is a classic **Screen Space Overlay** panel pinned to the corner of the screen — desktop-only, **not**
  VR-portable (no billboard / eventCamera). Same registry-driven binder either way (the binder skips the World-Space-only
  billboard + eventCamera when the canvas is an Overlay).
- ⚠️ **`TrackedDeviceGraphicRaycaster` is required for XR, and ONLY on a World Space canvas.** An XR ray/poke can hit a
  uGUI button only through this raycaster, so the XR modes (`PCXR`/`Cross`) add it. It is **useless on a Screen Space
  Overlay** (`PCSS`) — an overlay renders straight to the screen with no world position for a ray to intersect — which is
  why XR needs a World Space canvas and `PCSS` is inherently mouse-only. **The raycaster is only the canvas-side half:**
  the EventSystem-side `XRUIInputModule` + `NearFarInteractor` (controller **and** hand share it) come from the XR avatar
  rig, not from this skill (build-studio-room §6); this skill supplies the canvas raycaster + `XRWorldClicker` (the
  non-UI floor/world-click path).

## What this proves — and what it does NOT (honesty contract)
- **What is built** = a **cross-platform-READY** World Space UI structure: an authored, editable canvas carrying the
  desktop-mouse raycaster and (XR modes) the XR raycaster + `XRWorldClicker`, driven by a registry-bound hot binder.
  Controller and hand share one interactor, so hand input rides the **same code path**.
- ⭐ **What is PROVEN (verified)** = desktop **mouse** (human) **+ XR Interaction Simulator CONTROLLER** (human: UI
  button click + floor measure, via `deviceMode=Controller`). The agent additionally proves, non-interactively: the HUD
  exists with the right raycaster(s), the binder **self-wired from the registry** (`_wired`, rows = one per toggleable),
  and an **injected** `onClick` drives the feature's `SetEnabled` (the onClick→feature path).
- ❌ **NOT proven here (V2 — human + real device):** real-device **hand** pinch/poke, **XREAL**, **tablet touch**,
  **Vision gaze**; a **real pointer event → raycast** (the agent injects `onClick.Invoke`/`SubmitExternalRay`, not an OS
  pointer); bundled Korean font (runtime uses a dynamic OS font = desktop only). **Simulator limit:** controller `select`
  works; **hand mode only changes hand SHAPE, no `select`** — hands are not live-demoable in the editor.
- Nothing here is framed as "five platforms proven." The claim is **structure = cross-platform-ready; verification =
  desktop mouse + XR Simulator controller.**

## Ground rules
- studio MCP (`ai-game-developer`) must be connected. Drive Unity via MCP; do not hand-edit `.unity`.
- Do **not** scene-save `QuickStart.unity` — QuickTest edits it in memory only (Setup/Teardown restore it).
- **Reusable = no room hardcoding.** The binder reads only the registry; the Ruler "측정 지우기"/count appears **only**
  when `Contents.GetById("ruler")` is non-null at runtime (absent room → just not shown). Never bake a room/feature name.
- Idempotent: re-running replaces the skill's own `CrossPlatformRoomHud` object and never touches other UI.
- If a step is blocked, do **not** work around it — read `build-studio-room.md §5/§6`, report, and wait.

## Key resources (studio, paths stable)
- Reusable binder (registry-driven): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/CrossPlatformRoomHud.cs`
- XR world-click bridge (guard-copied only if the type is absent): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/XRWorldClicker.cs`
- Assembly (set `ROOM` + `MODE`): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/assemble_ui.cs` → `PS_AssembleUI.Run`
- Verify (set `ROOM` + `EXPECT_XR`): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/verify_ui.cs` → `PS_VerifyUI.{Setup,Check,Teardown}`
- Proven reference (Ruler-specific, DO NOT edit): `Assets/App/Scripts/ContentLogic/PromptScene/Content/Ruler/{RoomHudBinder,XRWorldClicker}.cs`
- QuickTest result (Read after Check): `c:\J_0\XumFlow-studio\Temp\ps_ui_result.txt`
- Boot scene: `Assets/App/Scenes/QuickStart.unity`; Core: `Assets/App/Scripts/ContentLogic/PromptScene/Core/`

---

## EXECUTE

### Phase 1 — Install the reusable scripts (guarded — avoid duplicate-type CS errors)
1. Target dir: `Assets/App/Scripts/ContentLogic/PromptScene/UI/` (create if missing). Copy `CrossPlatformRoomHud.cs`
   there **only if** type `CrossPlatformRoomHud` isn't already loaded. For XR modes, copy `XRWorldClicker.cs` there
   **only if** type `XRWorldClicker` isn't already present anywhere (studio already ships one under Content/Ruler —
   reuse it; a second copy is a `CS0101` duplicate-type error).
2. `assets-refresh`, then wait for `EditorApplication.isCompiling == false` and confirm the types loaded (a quick
   `script-execute` `AppDomain…GetType`). If `error CS`, fix before proceeding.

### Phase 2 — Author the HUD onto the room (build-studio-room §5/§6)
1. `scene-open Assets/App/Scenes/<ROOM>.unity` **Single** (keep it open/persistent).
2. Set `ROOM` + `MODE` at the top of `assets/assemble_ui.cs`, then `script-execute` `PS_AssembleUI.Run`. It authors,
   under `===== UI =====`, a `CrossPlatformRoomHud` **root Canvas** (CanvasScaler + `GraphicRaycaster` + — XR modes —
   `TrackedDeviceGraphicRaycaster` + the hot binder) — World Space for `PC`/`PCXR`/`Cross`, **Screen Space Overlay** for
   `PCSS` — with a **`Panel` CHILD** that carries the bg Image + vertical layout + `Title`/`Buttons`(→hidden
   `ButtonTemplate`)/`Count`/`Hint`. ⚠️ The bg Image is on the **Panel, never the root Canvas** — a root Screen Space
   Overlay canvas is driven to full-screen, so a background on it would cover the whole screen; the Panel stays a small
   corner box (~320px wide, buttons ~44px to match the room's existing toggles). For XR modes it also adds `XRWorldClicker`
   under `===== SYSTEMS =====`. It saves the scene (authored/editable — only the runtime bits per §5 are code).
3. Confirm the read-back: `canvas.renderMode=<WorldSpace|ScreenSpaceOverlay per mode>`, `rootHasNoBgImage=True`,
   `Panel bg Image=True`, `GraphicRaycaster=True`, `TrackedDeviceGraphicRaycaster=<expected for mode>`,
   `CrossPlatformRoomHud comp=True`, children under Panel present, `ButtonTemplate active=False`,
   `XRWorldClicker under SYSTEMS=<expected>`, and `ASSEMBLE-UI: OK`.

### Phase 3 — QuickTest §6.5 + UI verify (build-studio-room §4)
Set `ROOM`, `EXPECT_XR` (true for PCXR/Cross, false for PC/PCSS) and `EXPECT_SCREENSPACE` (true only for PCSS) in
`assets/verify_ui.cs`, then:
1. `scene-open Assets/App/Scenes/QuickStart.unity` **Single**.
2. `script-execute` `PS_VerifyUI.Setup` (host + `roomSceneKey=<ROOM>`; snapshots originals).
3. set `EditorApplication.isPlaying = true`. Wait ~12–15s (server → Addressables room load → spawn → RoomCore up →
   binder wires from the registry).
4. `script-execute` `PS_VerifyUI.Check`, then **Read** `c:\J_0\XumFlow-studio\Temp\ps_ui_result.txt`.
5. set `EditorApplication.isPlaying = false`.
6. `script-execute` `PS_VerifyUI.Teardown` (restores QuickStart in memory; disk untouched).

`console-get-logs` floods with a benign "2 event systems" warning — judge from the result file, filtering to Errors only.

---

## VERIFY — acceptance (all must pass)

| # | Pass condition | Where |
|---|---|---|
| U1 | HUD `CrossPlatformRoomHud` present; canvas render mode matches mode (`WorldSpace`, or `ScreenSpaceOverlay` for PCSS); `GraphicRaycaster` present; `TrackedDeviceGraphicRaycaster` present iff XR mode | result file U1 |
| U2 | Binder **self-wired from the registry** (`_wired == true`) | result file U2 |
| U3 | Generated rows = one per registry `Toggleable` (registry-driven, not hardcoded); `rows > 0`, `rows ≥ toggleables` | result file U3 |
| U4 | Injected `Btn_ruler.onClick.Invoke()` flips `IsEnabled` then restores it — the **onClick → feature.SetEnabled path** (skipped-as-pass if the room has no Ruler: reusable part) | result file U4 |
| U5 | Existing UI intact (canvases listed) + avatar `Desktop(Clone)` spawned = SYSTEMS unbroken | result file U5 |
| — | `=== §5/§6 CROSS-PLATFORM-UI VERDICT: PASS ===` | result file |

Failure map: HUD absent → assemble step didn't run / wrong scene. `_wired=false` → RoomCore not up (wait longer) or Core
not compiled. rows=0 with toggleables>0 → binder didn't clone the template (check `ButtonTemplate`/`Buttons` names).
U4 no flip → onClick not wired (serialized onClick trap — the binder must AddListener at runtime, §3b). Avatar missing →
SceneId churn in the room (see build-studio-room §3) — not a UI fault.

## Cleanup
Exit Play if running; delete `Temp/ps_ui_*.txt`. Leave the authored `CrossPlatformRoomHud` object, the installed
`CrossPlatformRoomHud.cs` (+ `XRWorldClicker.cs` if this skill added it), and the saved room in place.

## Report
Give the VERIFY table with actual result-file values and state PASS/FAIL plainly. Restate the honesty contract: the
**structure is cross-platform-ready** and **verification reached desktop mouse + XR Interaction Simulator controller**;
real-device hand/XREAL/tablet/Vision and a real pointer-event→raycast are **V2**. Confirm reusability held: the HUD bound
itself from the registry with no room/feature name baked in.
