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
  All visual values come from the frozen theme HudTheme (glass **v6**: dark Scrim panel, light semi-opaque Film circles
  with DARK glyphs, one-meaning cyan accent, 4-per-page icon grid with drag/wheel paging, Material Symbols subset icon
  font, and the KeyBadge world prompt) — this skill authors no literal color or size, and every ink/alpha decision is
  re-derived by the contrast gate on every run.
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
- **Design: the floor is machine-judged; the ceiling is not.** ✅ The *floor* — legibility (angular cap height, tap-target
  angle) , token compliance (zero literal colors/px, on-scale-or-derived spacing, one-meaning accent, no faux-bold),
  **contrast arithmetic against the worst environment**, composition, angular-size fixing, icon determinism and paging —
  is in the automated verdict as **U6/U7/U9/U10/U11/U12**. ❌ **미감(aesthetic quality) is UNVERIFIED** and stays a
  human/vision judgement: U8 captures PNGs as *evidence* and is deliberately kept out of PASS/FAIL. A green gate means
  "obeys the frozen theme", never "looks good".
- ⚠️ **그리고 그 경계는 실제로 두 번 값을 했다.** 산술이 통과시킨 것을 **캡처를 눈으로 봐서** 잡은 사례가 둘이다:
  (1) 고정 높이 카드 밖으로 흘러나온 줄바꿈 2번째 줄, (2) 16px 한글에 2px 아웃라인을 사방으로 깔아 획이 서로 먹은 것.
  둘 다 U7이 초록이었다. **판정에 안 넣는 증거를 왜 만드는가**에 대한 답이 이것이다 — 캡처는 장식이 아니라 절차다.
- **정지 규칙이 실제로 디자인을 바꿨다(이번 판 3건).** 게이트를 느슨하게 고치지 않고 값을 옮겼다:
  `Film α.28→.60`(어두운 글리프가 검은 환경에서 2.24:1), `Scrim α.42→.78`(라벨 1.72:1 → 6.09:1, 아웃라인 제거),
  `HUD z 2.5→1.5 m`(16px 라벨이 2.5 m에서 13.2′로 하한 미달). 전부 결과 파일에 숫자로 남는다.
- ⬜ **개척 청구서:** 배경 **블러**는 **미구현**이다 — 경로 3개(URP Renderer Feature / 전용 카메라 + 저해상도 RT /
  정적 굽기)와 **Quest 실기기 프레임 예산 실측 없이는 머지 금지** 게이트를 [hud-blur-invoice.md](../../docs/hud-blur-invoice.md)에
  적어 두었다(에디터에서 되고 실기기에서 안 된 전례가 있다). 같이 실린 항목: PyeojinGothic 400/600 번들링,
  물려받은 월드 텍스트 `MessageWindow`의 각크기 고정, 실물 다중 페이지 재확인.

## Ground rules
- studio MCP (`ai-game-developer`) must be connected. Drive Unity via MCP; do not hand-edit `.unity`.
- ⛔ **부트 흐름 3대 함정은 `docs/build-studio-room.md` §4.1~4.3 이 SSOT다.** 여기서 반복하지 않는다(사본은 뒤처진다).
  요약만: ① `roomSceneKey`가 없는 룸 → 정지  ② **룸 씬이 additive 로 열린 채 ▶ 하면 스폰이 빠진다**(룸·RoomCore·HUD는
  올라오는데 아바타·카메라만 없음) → 정지  ③ 키는 **Unity 씬 이름(leaf)** 이어야 한다 — `ResolveAddress()`가 주는
  등록 주소를 그대로 쓰면 안 된다(주소 형태면 FishNet `GetSceneByName()`이 실패한다). **등록 자체를** leaf 주소 +
  `RoomScene` 라벨로 맞춘다(§4.2). 세 개 다 Setup 진입 전에 단정한다.
- ⚠️ **에이전트 의무 2개** (§4.1/§4.3): 룸 씬을 편집했으면 넘기기 전에 `scene-open QuickStart` **Single**로 되돌린다.
  그리고 `Selection.activeObject=null`로 선택을 해제한다(Inspector가 포커스를 잡으면 사람이 WASD를 눌러도 안 움직인다).
  사람에게 이동을 부탁할 때는 **Game 뷰 중앙~우하단을 클릭한 뒤 길게** 누르라고 안내한다 — ⛔ 좌상단은 FishNet 데모 HUD의
  `Start/Stop Client` 버튼이라 클릭하면 부트가 깨진다(§4.1 ④ / §4.3).
- ℹ️ `No cameras rendering`은 **에디트 모드 전체와 ▶ 직후 수십 초 동안 정상**이다(실측: 방해 없는 부트 t≈27초) — `QuickStart`·룸 씬 모두 카메라가
  0개이고 카메라는 스폰되는 아바타가 들고 온다. 그보다 오래 지속되면 위 ①~④ 중 하나다. **룸 씬이 아직 Hierarchy에 없으면 로딩 중이고, 룸은 올라왔는데
  아바타·카메라가 없으면 고장이다** — 로그의 `Local client is starting` 횟수를 센다(2회면 ④).
- Do **not** scene-save `QuickStart.unity` — QuickTest edits it in memory only (Setup/Teardown restore it).
- **부트 씬의 `roomSceneKey`가 존재하지 않는 룸을 가리키면 카메라가 영원히 생기지 않는다.** QuickStart와 룸 씬은
  **둘 다 카메라를 갖고 있지 않다**(커밋 버전 grep = 0개) — 카메라는 스폰되는 아바타에서 온다. 그래서 룸 로드가
  실패하면 Game 뷰가 계속 `No cameras rendering`이고, 이건 UI 문제로 보이지만 **부트 설정 문제**다.
  확인: `roomSceneKey` 값이 Addressables에 등록된 주소인지(`Assets/App/Scenes/`에 씬 파일이 있는지) 본다.
- **Reusable = no room hardcoding.** The binder reads only the registry; the Ruler "측정 지우기"/count appears **only**
  when `Contents.GetById("ruler")` is non-null at runtime (absent room → just not shown). Never bake a room/feature name.
- Idempotent: re-running replaces the skill's own `CrossPlatformRoomHud` object and never touches other UI.
- **`HudTheme.cs` is 성역 (sanctuary), and the copy is ONE-WAY: plugin assets → studio.** Never edit the studio copy —
  Phase 1b overwrites it every run, so any edit there is silently destroyed. A PreToolUse guard blocks writes to both
  paths; changing a token means editing the plugin-assets file with the guard temporarily lifted (see the guard script's
  header for the exact escape hatch) and re-running Phase 1b.
- **No literal colors and no literal px.** Every color/size/spacing/radius/weight is referenced from `HudTheme`
  (`script-execute` compiles against App.HotUpdate, so tokens are *referenced*, not copied). If you need a value the
  theme does not have, do **not** write it into the code — **propose a token and stop** for approval.
- If a step is blocked, do **not** work around it — read `build-studio-room.md §5/§6`, report, and wait.

## Key resources (studio, paths stable)
- **Design tokens — 성역, ONE-WAY copy** (plugin assets → studio, never back): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/HudTheme.cs`
- **Approved mockup = the design contract** (1:1 px — the canvas is set to the mockup's 1200 px/m): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/hud-glass-v6.html`
- Reusable binder (registry-driven, icon grid + paging): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/CrossPlatformRoomHud.cs`
- Icon resolver + codepoint table (`ContentMeta.Icon`의 첫 소비자): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/HudIcons.cs`
- Page flick/wheel component: `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/HudPager.cs`
- World interaction prompt (각크기 고정 배지): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/KeyBadge.cs`
- Icon font (Apache-2.0, **정적 인스턴스 8글리프 서브셋 2.8KB**): studio `Assets/Resources/Fonts/MaterialSymbolsOutlined-PS.ttf`
- 개척 청구서(블러·폰트 번들링·물려받은 월드 텍스트): `${CLAUDE_PLUGIN_ROOT}/docs/hud-blur-invoice.md`
- XR world-click bridge (guard-copied only if the type is absent): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/XRWorldClicker.cs`
- Assembly (set `ROOM` + `MODE`): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/assemble_ui.cs` → `PS_AssembleUI.Run`
- Verify (set `ROOM` + `EXPECT_XR`): `${CLAUDE_PLUGIN_ROOT}/skills/cross-platform-ui/assets/verify_ui.cs` → `PS_VerifyUI.{Setup,Check,Teardown}`
- Proven reference (Ruler-specific, DO NOT edit): `Assets/App/Scripts/ContentLogic/PromptScene/Content/Ruler/{RoomHudBinder,XRWorldClicker}.cs`
- QuickTest result (Read after Check): `c:\J_0\XumFlow-studio\Temp\ps_ui_result.txt`
- Boot scene: `Assets/App/Scenes/QuickStart.unity`; Core: `Assets/App/Scripts/ContentLogic/PromptScene/Core/`

---

## EXECUTE

### Phase 1 — Install the reusable scripts (guarded — avoid duplicate-type CS errors)
1. Target dir: `Assets/App/Scripts/ContentLogic/PromptScene/UI/` (create if missing). Copy `CrossPlatformRoomHud.cs`,
   `HudIcons.cs`, `HudPager.cs`, `KeyBadge.cs` there **only if** the matching type isn't already loaded. For XR modes, copy `XRWorldClicker.cs` there
   **only if** type `XRWorldClicker` isn't already present anywhere (studio already ships one under Content/Ruler —
   reuse it; a second copy is a `CS0101` duplicate-type error).
1b. **`HudTheme.cs` → `Assets/App/Scripts/ContentLogic/PromptScene/UI/HudTheme.cs`, ALWAYS OVERWRITE — the guard
   pattern here is the OPPOSITE of the scripts above.** The others are copied *only if the type is absent* (a second
   copy is a `CS0101` duplicate-type error). `HudTheme.cs` is the token SSOT, and overwriting it unconditionally **is**
   the machine that kills drift: a studio-side edit can never survive, so the plugin-assets file stays the only truth.
   Confirm `PromptScene.Core.UI.HudTheme` loads.
1c. **아이콘 폰트** → `Assets/Resources/Fonts/MaterialSymbolsOutlined-PS.ttf` (없으면 설치). Resources 아래여야
   `HudIcons.Font`가 런타임에 찾는다(직렬 필드 0, §3b). 폰트를 다시 구우려면: Material Symbols는 npm
   `@material-symbols/font-400`조차 **가변 폰트**(축 `FILL`)라, fontTools `instancer`로 축을 고정해 **정적**으로
   만든 뒤 필요한 코드포인트만 subset 한다. 리가처 이름이 아니라 **코드포인트**로 넣어야 하고(Unity는 리가처
   미지원), 구운 뒤 U11이 `Font.HasCharacter`로 전부 존재하는지 단정한다.
2. `assets-refresh`, then wait for `EditorApplication.isCompiling == false` and confirm the types loaded (a quick
   `script-execute` `AppDomain…GetType`). If `error CS`, fix before proceeding.

### Phase 2 — Author the HUD onto the room (build-studio-room §5/§6)
1. `scene-open Assets/App/Scenes/<ROOM>.unity` **Single** (keep it open/persistent).
2. Set `ROOM` + `MODE` at the top of `assets/assemble_ui.cs`, then `script-execute` `PS_AssembleUI.Run`. It authors,
   under `===== UI =====`, a `CrossPlatformRoomHud` **root Canvas** (CanvasScaler + `GraphicRaycaster` + — XR modes —
   `TrackedDeviceGraphicRaycaster` + the hot binder) — World Space for `PC`/`PCXR`/`Cross`, **Screen Space Overlay** for
   `PCSS` — with a **`Panel` CHILD** carrying the bg Image. ⚠️ The bg Image is on the **Panel, never the root Canvas**.

   **glass v6 구조 — 제목도 문구도 없다. 원 4개/페이지 + 넘길 때만 보이는 점:**
   ```
   Panel  Image=Scrim + VerticalLayoutGroup(padding PadX/PadY) + ContentSizeFitter
     ├ PanelFrame  Rim   (라운드 프레임 스프라이트 1장, ignoreLayout)   ← 테두리를 상자 2겹으로 만들지 않는다
     ├ PanelEdge   RimLit(상단 스트립, ignoreLayout)
     ├ TopSpacer   높이 DotsRowH                                      ← 점 줄과 같은 높이의 **미러** 여백
     ├ Viewport    RectMask2D + 투명 Image + HudPager                 ← overflow:hidden + 드래그/휠
     │   └ Track └ PageTemplate(INACTIVE, 폭 4*HitD)
     ├ IconButtonTemplate (INACTIVE)  ← ⚠ PageTemplate **밖**. 안에 두면 페이지 복제 때 유령 셀이 딸려온다
     │   ├ Tmpl__disc  Film 원  └ Tmpl__glyph(GlyphDark) / Tmpl__icon(Meta.Icon)
     │   ├ Tmpl__ring  RimTop 2톤 링 1장 (형제·장식)
     │   └ Tmpl__label FontFoot / TextLo (원 밖 아래, 1줄 고정)
     └ Dots  HorizontalLayoutGroup + CanvasGroup  └ DotTemplate(INACTIVE)
   ```
   - **역할 접미사로 authoring 한다**(`__disc`/`__glyph`/`__ring`/`__label`/`__icon`/`__dot`). 바인더는 접미사로
     찾아 행 이름만 갈아끼우고, U6/U7/U9가 같은 접미사로 역할을 판정한다. 템플릿도 예외가 아니다.
   - **간격은 유도한다**: `InnerGap = HitPad*2`, `OuterMargin = InnerGap*2`, `PadX = Outer−HitPad`,
     `PadY = Outer−HitPad−DotsRowH`. 손으로 넣은 padding은 U7이 off-scale로 잡는다.
   - Card는 **0개**. 그려지는 판은 Panel 하나뿐이고 원은 컨트롤이다(중첩 깊이 0).
   - 상태 텍스트(`": ON"`) **0건** — ON은 원의 Accent 채움 + 라벨 강조로만 말한다.
   For XR modes it also adds `XRWorldClicker` under `===== SYSTEMS =====`. It saves the scene.

   💡 `script-execute`의 Roslyn 어셈블리는 **플레이 진입 시 도메인 리로드로 사라진다** — Setup/Check/Teardown을
   매번 다시 붙여넣는 이유다. 반복 실행이 잦으면 두 드라이버를 임시 Editor 폴더(+ `App.HotUpdate`를 참조하는
   asmdef)에 두면 리로드를 넘어 살아남는다. 끝나면 지운다.

3. Confirm the read-back: `canvas.renderMode=<WorldSpace|ScreenSpaceOverlay per mode>`, `rootHasNoBgImage=True`,
   `Panel bg Image=True`, `GraphicRaycaster=True`, `TrackedDeviceGraphicRaycaster=<expected for mode>`,
   `CrossPlatformRoomHud comp=True`, children under Panel present, `ButtonTemplate active=False`,
   `XRWorldClicker under SYSTEMS=<expected>`, and `ASSEMBLE-UI: OK`.

### Phase 2.5 — ⛔ CONFIRM the canvas scale. Do not assume it.
Every angular judgement in U6/U7 divides by px-per-metre. If that number is wrong, **every legibility verdict is false
while still reporting PASS** — the worst possible failure of a floor gate.

v6부터 `PxPerMeter`는 실측값이 아니라 **선택값(1200)**이다: 목업의 모든 px가 1200 px/m에 물려 있고(패널 648px =
0.54 m → 1.5 m에서 20.5°), 캔버스 스케일을 `1/PxPerMeter`로 **유도**하므로 목업 px가 1:1로 옮겨온다.
선택값이라고 검증을 안 하는 게 아니다 — 실측이 그 값을 되짚는다:

```csharp
float pxPerMeter = 1f / canvas.transform.lossyScale.x;   // canvas = the HUD root Canvas
```

- Within **±5%** of `HudTheme.Legibility.PxPerMeter` → proceed. (`assemble_ui.cs` prints `PHASE 2.5 measured …`.)
- Outside ±5% → **stop and report.**
- `CapArcmin(FontFoot) < MinCapArcmin` at `PlacementDistanceM` → **stop and report.** 이게 v6에서 실제로 걸렸고,
  해결은 **게이트가 아니라 배치를 옮기는 것**이었다: 1200 px/m에서 16px 라벨은 2.5 m에서 13.2′(하한 20′ 미달),
  1.5 m에서 22.0′. 그래서 HUD_POS의 z를 2.5 → 1.5로 당겨 설계 거리와 실배치 거리를 일치시켰다.
- Canvas or `lossyScale` unreadable → **stop and report.** Never proceed on a guessed scale.

*Confirmed 2026-07-30 (AssembleRoom / MODE=CROSS): 1200.0 px/m, panel 648×244 px = 0.540×0.203 m, 20.6° wide @1.5 m.*

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
| U6 | **Type discipline** — every text size ∈ the ramp `{FontFoot, FontBody, FontTitle}`, **except** role-suffixed marks (`__glyph`/`__keycap`) which must equal `GlyphPx`/`KeycapPx`; every size `CapArcmin ≥ MinCapArcmin` at **both** the design and the placement distance; **≤ 2 distinct RAMP sizes** under `Panel` (glyphs excluded — they are marks, not type); realised weight ∈ `AllowedWeights`; faux-bold = 0. Font fallback = **WARN, not FAIL** | result file U6 |
| U7 | **Colour / spacing / CONTRAST ARITHMETIC** — spacing+padding ∈ `SpaceScale` **∪ derived** (`PadX`/`PadY`); **literal colors = 0**; **accent = one meaning** (visible `Accent` only on a role part of an enabled feature, + a self-exercising POSITIVE case that drives a row ON); ⭐ **every text's real ancestor stack is composited over the WORST environment (white AND black) and must reach `Contrast.MinText` (4.5:1)**; ⭐ a white `Film` with **no `Scrim` ancestor covering it** = structural FAIL; tap targets ≥ `MinTargetDeg`. *Outline clause:* text carrying an **opaque** `Outline` of width ≥ `OutlineW` is judged against its own outline — a different machine, not a loosening; the conditions are asserted separately | result file U7 |
| U8 | **Capture — EVIDENCE, NOT A VERDICT.** Two PNGs, over a **bright (white)** and a **dark (black)** environment, because the whole thesis is that glass dies on the wrong background. Deliberately excluded from PASS/FAIL: the floor is machine-judged, **taste is not** — and looking at these is how two defects that the arithmetic passed were caught (a wrapped line escaping its card; a 2 px outline mashing 16 px Hangul) | result file U8 + PNGs |
| U9 | **Composition** — every node classifies into the v6 components (Page / IconButton / PageDots / KeyBadge + role parts); **unclassified = 0**; **Card-like text-bearing boxes = 0** (nesting depth 0) | result file U9 |
| U10 | **Angular-size runaway** — every world text either owns an angular-fix component (`KeyBadge`) or is on the **documented whitelist with a written reason**; plus a live **1 / 3 / 8 m sweep** proving the badge's angular size is constant (spread ≤ 0.02°). Closes the hole `"E 키로 앉기"` walked through | result file U10 |
| U11 | **Icons** — the `Meta.Icon → codepoint → first letter` chain runs deterministically (all three tiers demonstrated through the production resolver) and **every codepoint we use exists in the atlas**. A mapped-but-missing codepoint is a **STOP**, never a silent fallback | result file U11 |
| U12 | **Paging** — `GoTo` actually moves the track. If the room has ≤ `PageSize` entries the real panel is one page and the rule would pass **trivially**, so the gate drives a **synthetic 3-page** configuration through the same component and says so in the result | result file U12 |
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
