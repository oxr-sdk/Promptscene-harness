---
name: add-component
description: >
  Put a user-intended COMPONENT (a FEATURE or a COMPOSITION) onto a RoomCore-bearing PromptScene room in the
  XumFlow **studio** project and LIVE-PROVE it with a QuickTest (contract §5 + §6.5). This is the studio
  content-adder: it (1) CONSULTS — classifies the intent as FEATURE vs COMPOSITION vs UXRM (a UnifiedXRMotion
  motion/avatar/retargeting preset — its own uxrm-* tool path, §5 registry checks N/A), judges buildability against
  the capability map (재조합 ✅ vs 개척 ⛔), and routes "how to attach" through oxr-docs-routing, promising only
  what §5 can prove; (2) picks the room (or reference-calls /assemble-room to lay a fresh 5-layer skeleton first);
  (3) gets the component — reuse an already-ported type, AI-generate a FEATURE from the frozen Ruler template, or
  wire a human-written script; (4) places it under the right layer (FEATURES / COMPOSITIONS), wires its scene-embed
  prefab fields (§3b) and registers any new network prefab (C1); (5) QuickTest-proves §5 (FEATURE self-registers +
  SetEnabled exception-free + Meta valid ; COMPOSITION scene-resident + NOT registered) AND §6.5 (avatar still
  spawns = SYSTEMS unbroken); (6) optionally reference-calls /cross-platform-ui to lay a pointing UI. It absorbed
  scaffold-content's studio role (see the retrospective-B note). Reference-calls /assemble-room, /cross-platform-ui,
  oxr-docs-routing — never duplicates their procedures. Use when the user wants to add a capability to a room, e.g.
  "룰러 붙여줘", "add a click-spawner to MyRoom", "/add-component a chat feature". Argument = the component request
  (natural language), optionally "... on <Room>".
---

# Add a COMPONENT to a studio room and QuickTest-prove it

Turns "put X on a room" into a proven result in **studio** (`c:\J_0\XumFlow-studio`, hot-update/Addressables model).
It is the content-filling counterpart to `assemble-room` (which lays the empty 5-layer skeleton): assemble-room
reserves the `FEATURES` + `COMPOSITIONS` layers **empty**; **add-component fills them on demand** (contract §1
"골격=5층 예약, 채움=수요 시"). It works whether the component already exists in the project, is AI-generated from
the frozen template, or is a human-written script the agent only wires + verifies.

**This skill wraps procedures — it does NOT restate them.** The one source of truth for every mechanism is
`${CLAUDE_PLUGIN_ROOT}/docs/build-studio-room.md` (§2 RoomCore, §3 layers + SceneId safety, §3b serialization/XRI,
§3c C1 network-prefab, §4 QuickTest, §6.5 COMPOSITION) and `${CLAUDE_PLUGIN_ROOT}/docs/xumflow-migration.md`
**§9–§15** (the six live component loops this skill was frozen from — the "retrospective A" below distills them).
Contract §0 (판별 테스트), §1 (5-layer + registry), §2 (interfaces), §5 (checks) are in
`${CLAUDE_PLUGIN_ROOT}/docs/promptscene-content-contract.md`. Read those when a *why* is unclear.

**Argument:** the component request (natural language), optionally `... on <Room>`. Examples:
`/add-component 룰러`, `/add-component a spawn-a-cube-where-I-click tool on PromptSceneRoom_1`,
`/add-component a target-shootout game mode` (→ COMPOSITION),
`/add-component 아바타에 UnifiedXRMotion 모션 붙여줘` (→ UXRM — retrospective A′).

## Retrospective A — the common spine vs the per-kind differences (frozen from migration §9–§15)

**COMMON to every component loop (Ruler/Chat/Grab/TargetProps/ScoreHud/Match):**
- **Source = one assembly.** Code goes under `Assets/App/Scripts/ContentLogic/PromptScene/` (`Content/<Feature>/`
  or `Compositions/<Comp>/`) → compiles into **`App.HotUpdate`** (no separate asmdef). **Compile-0-errors gate**
  first (`isCompiling==false` + types load in AppDomain), THEN the live QuickTest gate.
- **§3b serialization discipline.** Prefab = **base components only** (NetworkObject / Rigidbody / renderer /
  a hot View whose serialized fields are **0** = runtime-wired). The FEATURE-root/COMPOSITION hot serialized
  fields (`measurementPrefab`, `channelPrefab`, `matchPrefab`, …) are wired by **scene-embed** — the scene loader
  fills them; a prefab-asset loader would not (the load-bearing hazard). No ScriptableObjects. `[Serializable]`
  data containers → `App.Bridges` (baked), never hot.
- **Scene layer.** FEATURE → `===== FEATURES =====` (one child GameObject per feature). COMPOSITION →
  `===== COMPOSITIONS =====`. **Never re-parent `--PLAYER_SPAWNER`** — add header content additively (SceneId churn
  trap). The skeleton already reserves both layers empty.
- **§5 QuickTest signals (host):** SYSTEMS unbroken (`Desktop(Clone)` still spawns) · content-specific check ·
  **Error 0** (console `console-get-logs` floods a benign "2 event systems" warning → filter to Errors only).

**DIFFERENCES (branch on these):**
| Axis | Values | What changes |
|------|--------|--------------|
| **Network prefab?** | YES: Ruler/Chat/Grab/Target/Match · NO: ScoreHud | YES → full **C1** (build-studio-room §3c: `FishNet…Generator.GenerateFull(null,false,true)` → `DefaultPrefabObjects` re-register; assert `IsSpawned=True` + **spawn-once**). Per-prefab Addressables entry `Network/Prefabs/<P>` is **not needed for QuickTest** but **required for Smart-Deploy** → add now or record as a deploy TODO. NO → skip C1 (pure IMGUI/local). |
| **XRI?** | YES: Grab/Dart · NO: rest | §11.3 **3b XRI boundary**: XR Grab Interactable/Rigidbody are **base-assembly** → serialize **directly on the prefab** (values persist). Hot View stays field-0, wires `selectEntered.AddListener` at runtime in `OnStartClient`. Traps: `OnStartClient` fires **one tick after** spawn (read `_wired` a tick later, not same frame); `using` BOTH `…Toolkit` and `…Toolkit.Interactables` (XRI 3.3.1 moved the type). |
| **FEATURE vs COMPOSITION** | IToggleableContent vs plain MonoBehaviour | FEATURE **self-registers** (in `Contents.All`, has Meta, SetEnabled) → FEATURES layer, depends only on `PromptScene.Core`, **0 refs to other FEATUREs**. COMPOSITION is **NOT registered** (scene-resident, subscribes to the bus in `Start`) → COMPOSITIONS layer; may reference FEATUREs' **event types** but not their classes, and checks presence via runtime `Contents.GetById(...)`. |

## Retrospective A′ — a THIRD kind: UXRM (UnifiedXRMotion motion/avatar preset)
Some asks — "아바타에 모션/리타게팅 붙여줘", "add full-body/IK motion", "이 휴머노이드를 UnifiedXRMotion으로 움직이게" —
are **neither** a FEATURE nor a COMPOSITION. They place a **UnifiedXRMotion preset** (a `RetargetSystem` + auto-collected
`IRetargeter`s) into the scene and **bind a humanoid `MotionAvatar`** to it. It never enters `Contents.All` (no
SetEnabled / no Meta), so the §5 registry checks **A/B/C do not apply** — its proof is a **bound RetargetSystem**.
- **It is driven by UnifiedXRMotion's OWN tools, not `add_component.cs` / the FeatureContent template.** SSOT = the
  four `uxrm-*` tool schemas (bundled at `c:\J_0\XumFlow-studio\.claude\skills\uxrm-*`; identical to the native MCP
  tools `mcp__ai-game-developer__uxrm-{describe-scene,pick-preset,place-preset,bind-avatar}`). Workflow:
  `describe-scene` (what RetargetSystems already exist) → `pick-preset` (→ `PrefabPath`) → `place-preset`
  (→ `RootInstanceId` / `RetargetSystemInstanceId` / `RootPath`) → `bind-avatar` (`humanoid` + `retargetSystemHolder`
  = the RetargetSystem's GameObject → `Success` / `Warnings`; **fails when the target is not a humanoid Animator**).
- **⚠ Open integration question — resolve via oxr-docs-routing BEFORE promising.** A PromptScene studio room spawns its
  avatar at **runtime** (`Desktop(Clone)`, FishNet); there is **no humanoid in the room scene at edit time** to bind.
  So *what* the preset binds to (the authored avatar prefab in SYSTEMS/spawner vs the runtime clone) and whether an
  edit-time placement survives the Addressables room-load are **NOT yet live-frozen** like the six loops below. This
  path is a **routed addition, not a frozen loop** — route it through **oxr-docs-routing / oxr-source-scout**
  (UnifiedXRMotion source) first, and do not silently place+bind against a scene that has no humanoid.

## Retrospective B — why add-component absorbed scaffold-content (studio)
The XRCollab `/scaffold-content` did "prompt → generate a FEATURE from the frozen Ruler template → live-verify §5"
against Master/Room.exe servers. add-component's "implement + wire + verify" is a **strict superset** of that studio
role — it also covers COMPOSITIONs, human-written scripts, already-ported types, the consultation/estimate step, and
UI linkage — and it verifies via the studio **QuickTest** (single-editor host), not server exes. **Decision:
add-component absorbs it — no studio port of scaffold-content is made.** The frozen Ruler template lives on here as
`assets/FeatureContent.cs.template` (the FEATURE-generation branch). XRCollab `/scaffold-content` stays as-is on the
XRCollab track (git history). When `/compose-room` is rewritten for studio (next in the migration queue), it will
reference-call **assemble-room (skeleton) + add-component (content)**.

## What this proves — and what it does NOT (honesty contract)
- ✅ **Proves (single-editor host QuickTest, MCP auto-judge):** compiles clean; the component is placed under the
  right layer and its prefab fields wired; **FEATURE** self-registers + `SetEnabled(true/false/double-on)` is
  exception-free + `IsEnabled` tracks + `Meta` valid; **COMPOSITION** is scene-resident and did **not** leak into
  the registry; network prefabs spawn `IsSpawned=True` (spawn-once); SYSTEMS unbroken (avatar spawns); Error 0.
- ❌ **Does NOT prove** the component *does what the prose intended*, nor that it looks good — behavioural
  correctness and aesthetics need a human/vision loop. **Ray injection caveat:** the agent injects at the
  `SubmitExternalRay`/reflected-`OnClick` boundary, **not** a real OS pointer-event → raycast. **Out of scope:**
  2-client parity (MPPM queue), real-device / XRI hand manipulation (human + simulator), Smart-Deploy.
- **UXRM (retrospective A′) is a routed addition, not a frozen loop.** It proves only a **bound RetargetSystem**
  (`uxrm-describe-scene` `MotionAvatarPath` non-null + bind `Success=true`) + SYSTEMS-unbroken + Error 0. It does
  **not** prove runtime motion fidelity, nor that an edit-time bind survives the runtime FishNet avatar spawn — that
  bind-target question must be resolved via oxr-docs-routing first (Phase 2U step 0).
- **Promise only what §5 can keep.** If the ask is a `⛔` capability (capability-map.md — e.g. contested projectile
  = client-side prediction = SYSTEMS thaw, grade "개척"), do **not** silently build a broken stand-in: say what
  blocks it, record a 개척 청구서 (pioneering invoice), and stop for the user.

## Ground rules
- studio MCP (`ai-game-developer`) must be connected. Drive Unity via MCP; do not hand-edit `.unity`.
- Do **not** scene-save `QuickStart.unity` — QuickTest edits it in memory only (Setup/Teardown restore it).
- Never re-parent `--PLAYER_SPAWNER`; add layer content additively. Never modify SYSTEMS/Core or PackageCache to
  make a component fit (that is a contract violation — see §4.5 core-promotion rules).
- If a step is blocked, do **not** work around it — read the SSOT doc, report, and wait (oxr-docs-routing §3).

## Key resources (studio, paths stable)
- Placement + §3b wiring (set `ROOM/KIND/TYPE_NAME/GO_NAME/WIRE_FIELDS/WIRE_PREFABS`):
  `${CLAUDE_PLUGIN_ROOT}/skills/add-component/assets/add_component.cs` → `PS_AddComponent.Run`
- §5+§6.5 verify (set `ROOM/KIND/CONTENT_ID/TYPE_NAME`):
  `${CLAUDE_PLUGIN_ROOT}/skills/add-component/assets/verify_component.cs` → `PS_VerifyComponent.{Setup,Check,Teardown}`
- FEATURE generation template: `${CLAUDE_PLUGIN_ROOT}/skills/add-component/assets/FeatureContent.cs.template`
- Reference implementations (studio, DO NOT edit — read as patterns): `.../ContentLogic/PromptScene/Content/{Ruler,Chat,GrabbableProps,TargetProps,ScoreHud}/`, `.../Compositions/TargetShootoutMatch/`
- QuickTest result (Read after Check): `c:\J_0\XumFlow-studio\Temp\ps_addcomp_result.txt`
- Boot scene: `Assets/App/Scenes/QuickStart.unity`; Prefabs (C1): `Assets/App/Prefabs/`

---

## EXECUTE

### Phase 0 — CONSULT / ESTIMATE (D6 상담층 — 정직 계약 대화판)
1. **Classify** the intent with the contract §0 판별 테스트: does it coordinate several FEATUREs into one loop
   (→ **COMPOSITION**) or is it a single opt-in capability (→ **FEATURE**)? **Or** is it avatar/humanoid
   **motion / retargeting / IK / a UnifiedXRMotion preset** (→ **UXRM** — retrospective A′, its own tool path, §5
   A/B/C N/A)? Is a **network prefab** involved (shared/spawned result)? Is **XRI** involved (grab/throw)?
2. **Judge buildability** against `${CLAUDE_PLUGIN_ROOT}/docs/capability-map.md`: is this a **재조합** (✅, a
   recombination of verified machines) or a **⛔ 개척** (needs new SYSTEMS/prediction/etc.)? If ⛔ → state the
   blocker, record a 개척 청구서, and **stop for the user** — do not build a silently-broken stand-in.
3. **Route "how to attach"** through **oxr-docs-routing** (platform API = source is truth). If a new platform API /
   signature is needed, delegate to the **oxr-source-scout** agent for the ground-truth signature before writing code.
4. **Report the estimate**, ask only at genuine forks (propose the default): which room? create the component
   (AI-generate / human-writes) or reuse an existing type? Promise only what §5 can prove.

### Phase 1 — Room (reference-call /assemble-room only if none)
If the user named a room that already has a RoomCore + the empty 5-layer skeleton, use it (skip). Otherwise
**reference-call `/assemble-room <Room>`** to lay a fresh skeleton (5 layers, empty FEATURES + COMPOSITIONS,
QuickTest §6.5 PASS). Do not duplicate its procedure — invoke the skill.

### Phase 2 — Get the component (three sources; retrospective A "차이" decides FEATURE vs COMPOSITION)
- **Reuse:** the type already compiled into App.HotUpdate (e.g. `RulerContent`) — nothing to author.
- **AI-generate a FEATURE:** copy `assets/FeatureContent.cs.template` → `Content/<Feature>/<Class>.cs`, replace every
  `__TOKEN__`, fill only the `===== FEATURE LOGIC (edit) =====` regions (R1–R5). For a networked result, author its
  prefab under `Assets/App/Prefabs/` with **base components only + a field-0 hot View** (§3b) and do **C1**
  (build-studio-room §3c).
- **COMPOSITION:** author a plain `MonoBehaviour` under `Compositions/<Comp>/` (pattern = `TargetShootoutMatch`) +
  its network-authority prefab if needed (`MatchView` shape: `[ServerRpc(RequireOwnership=false)]` up +
  `[ObserversRpc]` down, server-injected sender). It references FEATUREs' **event types** only; detect FEATURE
  presence via runtime `Contents.GetById(...)`. C1 the prefab.
- **Human-written script:** the human authors the `.cs`; the agent only **wires + verifies** it (step-6 정정:
  창작=사람 선택, 배선·검증=AI). Sanity-check it against R1–R5 / the FEATURE↔FEATURE-0-refs rule before wiring.
- Then confirm **compile 0 errors**: `assets-refresh`, wait `isCompiling==false`, verify the type loads in AppDomain.

### Phase 3 — Place + wire (add_component.cs)
`scene-open Assets/App/Scenes/<Room>.unity` **Single** (keep it open/persistent — SceneId safety). Set `ROOM`,
`KIND` (`FEATURE`|`COMPOSITION`), `TYPE_NAME`, `GO_NAME`, and the parallel `WIRE_FIELDS`/`WIRE_PREFABS` (scene-embed
prefab fields; empty `{}` for no-prefab content) at the top of `assets/add_component.cs`, then `script-execute`
`PS_AddComponent.Run`. Confirm the log: `placed … component=True`, every `wire … -> <prefab>`, `allFieldsWired=True`,
`layer … child count` incremented, `SceneId-safe=True` (spawner untouched). If a `<FIELD NOT FOUND>` /
`<PREFAB NOT FOUND>` appears, fix the field name / C1 the prefab and re-run.

### Phase 4 — QuickTest §5 + §6.5 (verify_component.cs)
Set `ROOM`, `KIND`, `CONTENT_ID` (FEATURE registry id), `TYPE_NAME` in `assets/verify_component.cs`, then:
1. `scene-open Assets/App/Scenes/QuickStart.unity` **Single**.
2. `script-execute` `PS_VerifyComponent.Setup` (server + host + roomSceneKey=<Room>; snapshots originals).
3. `script-execute` set `EditorApplication.isPlaying = true`. Wait ~12–15s (server → Addressables room load → spawn
   → RoomCore up → FEATURE self-registers). **XRI:** let one extra tick pass before reading `_wired` (§11.4 trap).
4. `script-execute` `PS_VerifyComponent.Check`, then **Read** `c:\J_0\XumFlow-studio\Temp\ps_addcomp_result.txt`.
5. `script-execute` set `EditorApplication.isPlaying = false`.
6. `script-execute` `PS_VerifyComponent.Teardown` (restores QuickStart in memory; disk untouched).

Judge from the result file, filtering `console-get-logs` to Errors only (benign "2 event systems" flood). For a
network-prefab component also assert `IsSpawned=True` + spawn-once, and for a COMPOSITION optionally exercise the
server-authoritative loop (build-studio-room §6.5) — picking a **visible (un-occluded) target** for ray injection
(§14.3 Capsule trap; default base T_RoomA has none).

### Phase 2U/3U/4U — UXRM path (REPLACES Phases 2–4 when KIND=UXRM; retrospective A′)
Do **not** use `add_component.cs` / `verify_component.cs` — UnifiedXRMotion's own MCP tools place, wire, and verify.
0. **Route first (mandatory).** Delegate to **oxr-docs-routing** (or **oxr-source-scout** on UnifiedXRMotion source)
   to answer the open bind-target question (edit-time authored avatar vs runtime `Desktop(Clone)`). If it cannot be
   answered, **stop and report** — do not place+bind blind (§0 honesty). Confirm the target humanoid with the user.
1. `scene-open Assets/App/Scenes/<Room>.unity` **Single** (SceneId safety — never re-parent `--PLAYER_SPAWNER`).
2. `uxrm-describe-scene` → note any existing `RetargetSystem` entries (avoid double-placing).
3. `uxrm-pick-preset` → take `PrefabPath` (log `Reason`; surface `Alternatives` to the user if non-empty).
4. `uxrm-place-preset { prefabPath: <PrefabPath> }` → keep `RetargetSystemInstanceId` + `RootPath`.
5. `uxrm-bind-avatar { humanoid: <the confirmed humanoid ref>, retargetSystemHolder: <RetargetSystem GO> }`.
   Assert `Success=true` + empty `Warnings` (Success=false ⇒ target is **not a humanoid Animator** — fix the target,
   do not force). `scene-save` the room (the preset is authored scene content, unlike QuickStart).
6. **Verify** = `uxrm-describe-scene` shows the RetargetSystem with `MotionAvatarPath` **non-null** (bound); then run
   the normal QuickTest §6.5 to confirm **SYSTEMS unbroken** (`Desktop(Clone)` still spawns) + **Error 0**. The §5
   A/B/C registry checks are **N/A** (UXRM never self-registers). Report exactly this — do not claim runtime motion
   fidelity (behaviour is a human/vision loop; and if the bind target was the authored prefab, runtime-clone motion is
   still **unproven** until observed live).

### Phase 5 (optional) — pointing UI (reference-call /cross-platform-ui)
If the user wants to drive the component by pointing, **ask** which mode and reference-call
`/cross-platform-ui <PC|PCSS|PCXR|Cross> on <Room>`. The HUD binds itself from the registry (one button per
toggleable FEATURE) — no room hardcoding. ⚠ Real XRI manipulation is a **human** (simulator) judgment; the agent
proves the onClick→SetEnabled path + `SubmitExternalRay` injection only.

---

## VERIFY — acceptance (all must pass; KIND-branched)
| # | Pass condition | Where |
|---|---|---|
| S1 | Room `<Room>` loaded (Addressables leaf) | result file S1 |
| S2 | `Desktop(Clone)` avatar spawned = SYSTEMS unbroken by the new content | result file S2 |
| S3 | `RoomCore.Instance` up with registry | result file S3 |
| A (FEATURE) | `GetById(<id>)` returns the content = **self-registered** | result file RESULT(A) |
| B (FEATURE) | `SetEnabled(true/false/double-on)` exception-free; `IsEnabled` true→then→false | result file RESULT(B) |
| C (FEATURE) | `Meta.DisplayName` + `Meta.Category` non-empty | result file RESULT(C) |
| A (COMPOSITION) | the COMPOSITION type is present & alive in the room (scene-resident) | result file RESULT(A) |
| B (COMPOSITION) | it did **NOT** leak into the registry (COMPOSITION never self-registers) | result file RESULT(B) |
| A (UXRM) | `uxrm-describe-scene`: the placed `RetargetSystem` has `MotionAvatarPath` **non-null** (bound) | uxrm-describe-scene |
| B (UXRM) | `uxrm-bind-avatar` returned `Success=true`, `Warnings` empty | bind-avatar result |
| C (UXRM) | §5 A/B/C registry checks **N/A** (never self-registers); SYSTEMS unbroken + Error 0 still required | QuickTest §6.5 |
| — | `=== §5/§6.5 ADD-COMPONENT VERDICT (<KIND>): PASS ===` | result file (FEATURE/COMPOSITION) |

Failure map: `NOT FOUND` (FEATURE) → RoomCore missing / not under FEATURES / didn't compile. `SetEnabled … THREW` →
R2/R3/R4 violated (touched platform input, non-idempotent, bad teardown). COMPOSITION `leaked=True` → it implements
IRoomContent (make it a plain MonoBehaviour). Avatar missing but room loaded → SceneId churn (you re-parented the
spawner — build-studio-room §3). `_wired=False` for XRI → read it one tick later (§11.4).

## Cleanup
Exit Play if running; delete `Temp/ps_addcomp_*.txt`. Leave the placed component, wired room, any authored `.cs` /
prefab, and C1 registration in place.

## Report
Give the VERIFY table with actual result-file values and state PASS/FAIL plainly. State the KIND (FEATURE/COMPOSITION),
which source produced it (reuse / AI-gen / human-written), and restate the honesty contract: **structure/contract
proven via single-editor host QuickTest; behaviour/aesthetics, 2-client parity, real-device XRI, and deploy are
out of scope.** If Phase 0 hit a ⛔ capability, report the 개척 청구서 instead of a build.
