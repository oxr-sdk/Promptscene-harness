---
name: add-component
description: >
  Put a user-intended COMPONENT (a FEATURE or a COMPOSITION) onto a RoomCore-bearing PromptScene room in the
  XumFlow **studio** project and LIVE-PROVE it with a QuickTest (contract §5 + §6.5). This is the studio
  content-adder: it (1) CONSULTS — classifies the intent as FEATURE vs COMPOSITION vs UXRM (a UnifiedXRMotion
  motion/avatar/retargeting preset — its own uxrm-* tool path, §5 registry checks N/A), grades buildability per
  SLICE against the capability map in 3 grades (재조합 ✅ / 코드-대체 ⚠ = build it in code / 개척 ⛔ = only human
  aesthetics, owner sign-off, or absent infra), and routes "how to attach" through oxr-docs-routing, promising only
  what §5 can prove; (2) picks the room (or reference-calls /assemble-room to lay a fresh 5-layer skeleton first);
  (3) gets the component — reuse an already-ported type, AI-generate a FEATURE from the frozen Ruler template, or
  wire a human-written script; (4) places it under the right layer (FEATURES / COMPOSITIONS), wires its scene-embed
  prefab fields (§3b) and registers any new network prefab (C1); (5) QuickTest-proves §5 (FEATURE self-registers +
  SetEnabled exception-free + Meta valid ; COMPOSITION scene-resident + NOT registered) AND §6.5 (avatar still
  spawns = SYSTEMS unbroken); (6) runs the **OPTION GATE** — one AskUserQuestion window asking whether to add a
  pointing UI and whether to run a 2-client check, **skipped entirely when the user already declared their intent**
  — then reference-calls /cross-platform-ui and /multiplayer-check accordingly; (7) ALWAYS asks the human whether
  they want to drive it in the UI themselves (handoff — leaves the room in Play with an operating recipe).
  So the shape is 컴포넌트 만들기 → UI 확인(옵션) → 네트워크·멀티 확인(옵션) → 사람 핸드오프. It absorbed
  scaffold-content's studio role (see the retrospective-B note). Reference-calls /assemble-room, /cross-platform-ui,
  /multiplayer-check, oxr-docs-routing — never duplicates their procedures. Use when the user wants to add a capability to a room, e.g.
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
  correctness and aesthetics need a human/vision loop, **so hand it to the human: Phase 8 is a mandatory ask, not a
  disclaimer.** **Ray injection caveat:** the agent injects at the `SubmitExternalRay`/reflected-`OnClick` boundary,
  **not** a real OS pointer-event → raycast. **Out of scope:** 2-client parity (MPPM queue), real-device / XRI hand
  manipulation (human + simulator), Smart-Deploy.
- **UXRM (retrospective A′) is a routed addition, not a frozen loop.** It proves only a **bound RetargetSystem**
  (`uxrm-describe-scene` `MotionAvatarPath` non-null + bind `Success=true`) + SYSTEMS-unbroken + Error 0. It does
  **not** prove runtime motion fidelity, nor that an edit-time bind survives the runtime FishNet avatar spawn — that
  bind-target question must be resolved via oxr-docs-routing first (Phase 2U step 0).
- **Promise only what §5 can keep — but build everything code can reach (Phase 0 step 2).** A `⛔` grade is reserved
  for the three real gates (human aesthetics / platform-owner sign-off / absent infrastructure). A missing asset, a
  missing public setter, or "nobody did this yet" is **⚠ 코드-대체 → build it in code**, label it as a substitute,
  and carry the gap out as a 추천사항. The 개척 청구서 now covers the **residual** slices only — never a whole
  request that had a buildable slice in it. Do not silently build a stand-in you know to be broken.

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
2. **Slice the request, then judge each slice — 3 grades, not 2** (2026-07-27 개척 완화 결정; grading a whole
   request ⛔ in one lump is forbidden). Against `${CLAUDE_PLUGIN_ROOT}/docs/capability-map.md`:
   - ✅ **재조합** — verified machines recombined → build normally.
   - ⚠ **코드-대체 (code-reachable)** — the map/scout says *"the asset doesn't exist" / "there's no public setter" /
     "nobody has done this yet"*, **but code can reach it without a human creating anything and without editing
     SYSTEMS**: generate the asset procedurally from an editor script (`AnimationClip` + `AnimationCurve`, mesh,
     material, curve-authored pose), reflect over a private platform field at runtime, or implement a **content-side
     substitute** for the missing platform component. → **BUILD IT.** This is now the default for anything Claude
     Code can reach with code. **Missing asset ≠ ⛔.**
   - ⛔ **개척** — exactly three gates, nothing else: **(a)** a human's aesthetic/creative judgment that code cannot
     stand in for at all, **(b)** a platform-owner decision (§4.5 승격 / SYSTEMS 해동 / PackageCache 변경),
     **(c)** absent infrastructure (client-side prediction, 3+ client harness, real device).
   Before grading any slice **⛔(b)**, you must first hunt for a **content-side path that leaves SYSTEMS untouched** —
   runtime lookup of the spawned object (`GameObject.Find`/`Contents.GetById`/scene walk) + **FEATURE-owned** network
   state (its own `NetworkObject` prefab carrying the `SyncVar`/`[XumRPC]`), instead of reaching into the avatar or
   spawner prefab. Prefab reach-in is the **last resort, not the first blocker**.
2b. **Honesty under the relaxation (완화 ≠ 과장).** A code substitute ships **labelled as one** ("procedurally
   authored seated pose, not an artist clip"; "private buffer written by reflection — fragile across package
   upgrades"). Claim only what §5 proves, and carry the quality/fragility delta out as a **추천사항** in the report,
   not as a refusal. Never pass a code substitute off as the real thing, and never ship a stand-in that is *known
   broken* — a substitute must actually work at the level you claim.
3. **Route "how to attach"** through **oxr-docs-routing** (platform API = source is truth). If a new platform API /
   signature is needed, delegate to the **oxr-source-scout** agent for the ground-truth signature before writing code.
4. **Report the estimate** as a **per-slice ✅/⚠/⛔ table** with the one-line code-substitute plan for every ⚠ slice.
   Ask only at genuine forks (propose the default): which room? create the component (AI-generate / human-writes) or
   reuse an existing type? Promise only what §5 can prove. **Stopping with nothing built is a failure** unless every
   slice landed on ⛔(a)/(b)/(c) — then, and only then, return the 개척 청구서 instead of a build.

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

### Phase 5 — 옵션 게이트 (AskUserQuestion 한 창, 두 질문)
§5가 PASS한 **직후**, 남은 두 검증(UI / 멀티)은 **사람이 고른다.** 만들기는 끝났고 여기서부터는 선택이다.

**① 선행 선언이 있으면 묻지 않는다.** 사용자가 이미 의사를 밝혔으면 그대로 실행하고 창을 띄우지 않는다:
- "UI까지 확인해줘", "멀티까지 봐줘", "2인으로도 확인" → 해당 단계 **YES로 확정**
- "구조만", "검증은 됐고 붙이기만" → 해당 단계 **생략**
- 한쪽만 말했으면 **말한 쪽만 확정하고, 나머지 한 질문만** 띄운다.

**② 선언이 없으면 `AskUserQuestion` 으로 한 창에 두 질문**을 띄운다(두 번 끊지 않는다). UI 모드는 별도 질문으로
쪼개지 말고 **선택지 자체에 넣는다**:

| | 질문 | 선택지 |
|---|---|---|
| Q1 | **UI를 통해 사용자 검증을 하시겠습니까?** | `예 — 크로스플랫폼(Cross)` (권장, 기본) · `예 — PC 전용(PC)` · `예 — PC+XR(PCXR)` · `아니오` |
| Q2 | **네트워크·멀티(2클라) 검증을 하시겠습니까?** | `예 — 2인 스폰(게이트 1)만` · `예 — 게이트 1 + 이 컴포넌트 파리티` (권장) · `아니오` |

⚠ Q2 는 **답을 예측해서 미리 돌리지 않는다.** 2클라는 에디터를 하나 더 띄우는 일이라 사용자가 고르기 전에
시작하면 되돌리기 어렵다. 그리고 **아니오도 정상 결과**다 — 리포트에 "사용자가 생략을 선택"으로 적는다.

### Phase 6 (옵션, Q1=예) — pointing UI (reference-call /cross-platform-ui)
Q1에서 고른 모드로 `/cross-platform-ui <PC|PCSS|PCXR|Cross> on <Room>` 를 **참조 호출**. HUD는 레지스트리에서
스스로 바인딩한다(토글 가능한 FEATURE 하나당 버튼 하나) — 룸 하드코딩 없음. ⚠ 실 XRI 조작 판정은 **사람**
(시뮬레이터) 몫이다. 에이전트가 증명하는 건 onClick→SetEnabled 경로 + `SubmitExternalRay` 주입까지다.

### Phase 7 (옵션, Q2=예) — 네트워크·멀티 검증 (reference-call /multiplayer-check)
`/multiplayer-check <이 컴포넌트> on <Room>` 를 **참조 호출**. 절차·함정은 그 스킬이 갖는다 — 여기서 되풀이하지
않는다. 알아야 할 접점만:
- **클론은 1회성**(`c:\J_0\XumFlow-studio_clone_0`). 있으면 재사용하므로 두 번째부터는 에디터 하나 더 띄우는
  비용뿐이다. 없으면 그 스킬이 만든다(ParrelSync, **매니페스트 무수정**).
- **순서 고정 — 게이트 1(2인 스폰 4신호)이 FAIL이면 인프라 문제이므로 컴포넌트 파리티로 넘어가지 않는다**(오진 방지).
- **대상이 룸에 배치돼 있어야 파리티를 볼 수 있다.** 방금 이 스킬이 배치했으니 정상적으로 충족된다.
- **부분 PASS를 그대로 받는다** — 일부 신호만 서도 실패가 아니고, 어디서 멈췄는지가 산출물이다.
- 끝나면 **A는 다시 단독 host 상태**로 돌아온다(B 종료). 그 뒤에 Phase 8을 진행한다.

⚠ 이 단계가 증명하지 **못하는 것**을 리포트에 그대로 옮긴다: **실 키보드 2인 조작**(에디터 2개 중 활성창만
입력을 받아, 그 절차는 A=MCP·B=파일명령으로 함정을 *우회*한다) · 3인+ · 실기기 · 배포.

### Phase 8 — 사람 루프 핸드오프 (MANDATORY ASK — before the report, not instead of it)
§5 PASS proves **structure**; it never proves the thing *feels* right. Behaviour + aesthetics are the human's call, so
the procedure must **hand the live room over**, not merely disclaim it. Ask, verbatim in the user's language:
**"직접 UI로 테스트해보시겠어요?"** — three options, **default A**:

| | What you do | When |
|---|---|---|
| **A. 지금 몰아보기** (default) | **Skip Phase 4 step 5** (`isPlaying = false`) and skip the Cleanup Play-exit — leave the editor **in Play** and hand over a **조작 레시피**: which keys move (WASD + mouse look), what to click/point at, which HUD button toggles the FEATURE, the XR Simulator keys if PCXR, and **the exact signal to look for with your eyes** ("의자 근처로 걸어가 E — 몸이 앉은 자세로 바뀌는지"). | 사람이 지금 붙어 있을 때 |
| **B. 직접 열어둘게** | Exit Play, run Teardown, then `scene-open Assets/App/Scenes/<Room>.unity` **Single** and leave it open so the user presses Play when they want. Give the same recipe. | 나중에 볼 때 / 씬을 손보고 싶을 때 |
| **C. 안 함** | Normal Cleanup. Report only. | 구조 검증만 필요할 때 |

⚠ Traps for A/B: (1) `Teardown` has **not** run in option A — tell the user **"QuickStart는 저장하지 마세요"**
(disk is still clean; an in-memory-modified QuickStart must not be saved), and that the next QuickTest run needs
Teardown / a scene reload first. (2) Never `scene-save QuickStart.unity` in either option.
(3) Report what the human observed (or that they deferred) — do **not** upgrade their silence into a PASS.

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
| H1 | **Phase 8 asked** ("직접 UI로 테스트해보시겠어요?") and the chosen option honoured (A = left in Play + recipe) | the report itself |
| H3 | **Phase 5 옵션 게이트 처리됨** — 선행 선언이 있었으면 그대로 따랐고, 없었으면 AskUserQuestion 한 창으로 물었다. 두 답(UI / 멀티)이 리포트에 **각각** 적혀 있다("아니오"도 결과다) | the report itself |
| H4 | (Q2=예일 때) `/multiplayer-check` 결과가 **부분 PASS까지 그대로** 실려 있고, 실입력 2인·3인+·실기기가 밖이라고 재기술됨 | the report itself |
| H2 | Every ⚠ 코드-대체 slice is **labelled as a substitute** in the report + has a 추천사항 line | the report itself |
| — | `=== §5/§6.5 ADD-COMPONENT VERDICT (<KIND>): PASS ===` | result file (FEATURE/COMPOSITION) |

Failure map: `NOT FOUND` (FEATURE) → RoomCore missing / not under FEATURES / didn't compile. `SetEnabled … THREW` →
R2/R3/R4 violated (touched platform input, non-idempotent, bad teardown). COMPOSITION `leaked=True` → it implements
IRoomContent (make it a plain MonoBehaviour). Avatar missing but room loaded → SceneId churn (you re-parented the
spawner — build-studio-room §3). `_wired=False` for XRI → read it one tick later (§11.4).

## Cleanup
Exit Play if running — **unless Phase 8 option A was chosen** (then leave Play up and say so). Delete
`Temp/ps_addcomp_*.txt`. Leave the placed component, wired room, any authored `.cs` / prefab, and C1 registration in
place.

## Report
Give the VERIFY table with actual result-file values and state PASS/FAIL plainly. State the KIND (FEATURE/COMPOSITION),
which source produced it (reuse / AI-gen / human-written / **코드-대체**), and restate the honesty contract:
**structure/contract proven via single-editor host QuickTest; behaviour/aesthetics, 2-client parity, real-device XRI,
and deploy are out of scope.** Then, always:
- **추천사항** — for every ⚠ 코드-대체 slice shipped: what it is a substitute *for*, and what upgrading it takes
  (an artist clip instead of the procedural one, a package setter instead of reflection, …). These are
  recommendations, not blockers.
- **잔여 개척 청구서** — only the slices that landed on ⛔(a) human aesthetics / (b) owner sign-off / (c) absent
  infra, each with which gate and what would open it. Record it under `promptscene/docs/<topic>-invoice.md`.
- **옵션 게이트 결과(Phase 5)** — UI / 멀티 각각 **예·아니오와 그 근거**(사용자 선행 선언이었는지, 창에서
  골랐는지). 생략도 결과로 적는다 — 안 물어본 것과 사용자가 거절한 것은 다르다.
- **멀티 검증 결과(Phase 7, 돌렸다면)** — 게이트 1 4신호 + 컴포넌트 파리티를 **부분 PASS 포함** 그대로.
  경계 재기술: 같은 머신·에디터 2인·데스크톱까지이고 **실입력 2인 조작·3인+·실기기·배포는 밖**.
- **Phase 8 결과** — which handoff option the user picked, and (for A/B) the 조작 레시피 you handed over plus the
  "QuickStart 저장 금지" warning.

A report with a 청구서 and **no build** is only valid when *every* slice hit ⛔(a)/(b)/(c).
