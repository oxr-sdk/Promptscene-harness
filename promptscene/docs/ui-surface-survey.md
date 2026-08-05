# UI 표면 · 토글 · 입력 바인딩 인벤토리 (Shell action-space 실측) — SSOT

> 2026-08-03 조사. **산출물 = 조사 기록**(구현·수정 0건). `grab-ownership-survey.md` / `prediction-survey.md`와 같은
> 성격의 정찰 문서다. 대상 프로젝트는 **XumFlow studio**(`c:\J_0\XumFlow-studio`) — XRCollabDemo가 아니다.
>
> **이 문서의 용도:** 소환 런치패드(Shell)의 **action space를 실측으로 확정**하는 단일 출처. 셸의 섹션·탭 구조,
> 소환 키, "그릴 수 있는 설정 항목"의 판정을 여기서부터 읽는다. 추측으로 만든 카테고리는 나중에 전부 갈아엎어야
> 하므로, 이 문서의 가치는 **확실성 구분**(측정됨 / 소스로 확인됨 / 미확인)에 있다.
>
> 공개 레포 규율: private SDK(`com.oxr-sdk.*`)·상용 에셋(FishNet)·Unity 샘플의 **코드 원문은 싣지 않는다** —
> 동작 서술 + `파일:라인` 포인터로 기록한다.

---

## 판정 (결론 먼저)

1. **3분류(설정 / 기능 / 나가기)로는 안 담긴다. 네 번째가 필요하다.** `Players list`가 미분류로 남는다 —
   그런데 이건 "네 번째 섹션 후보"이기 이전에 **백엔드가 없는 죽은 버튼**이다(§6). 셸이 흡수할 대상이 아니라
   **개척 청구서 행**이다.
2. **실측 Category는 5종**(`상호작용 / 소통 / 물체 / 측정 / 게임`)이고, **코드 어디에서도 읽히지 않는다.**
   지금까지 가정해 온 "측정/소통/게임"은 5종 중 3종만 맞았다. 셸 탭이 Category를 쓰려면 **셸이 첫 소비자**가 된다.
3. **Y 키는 우리 것이 아니다.** 소유자는 **XRI 3.3.1 XR Interaction Simulator 샘플**이고, 그 샘플은
   `QuickStart.unity`(스튜디오 QuickTest 부트 씬)에만 있다 — **출시 콘텐츠 번들에는 안 나간다.** 그래도 QuickTest
   판정 중 항상 켜져 있으므로 소환 키 후보에서 **제외**한다(§4).
4. **소환 키 최종 후보: 데스크톱 `M`(1순위) / `F1`(2순위), XR 왼손 `secondaryButton`(=Y, 1순위) / 왼손
   `primaryButton`(=X, 2순위).** 오른손 `primaryButton`(A)은 XRI `JumpProvider`가 선점했다.
5. **텍스트 입력 중 핫키 억제 장치는 존재하지 않는다 — 현존 버그로 확정.** 채팅 입력창에 `e`를 치면 의자에 앉고,
   `wasd`를 치면 아바타가 걸어간다(§4-C).
6. **실제로 그릴 수 있는 설정은 3개뿐이다**(그래픽 품질 2단계 / 마스터 볼륨 / 마이크·스피커 음소거).
   해상도·렌더스케일은 조건부, 마이크 **장치 선택**과 개별 볼륨 슬라이더는 **개척 청구서행**(§5).
7. **`Leave game`은 흡수할 수 없다 — 지금 아무것도 하지 않는다.** 직렬 `onClick`은 타깃 null, 코드 경로는
   구독자 0. 셸이 "나가기"를 가지려면 **우리가 만들어야** 한다(§6).

---

## 0. 조사 방법과 확실성 등급

| 등급 | 뜻 | 이 문서에서의 표기 |
|---|---|---|
| **측정** | 에디터에 살아있는 런타임에서 리플렉션으로 읽음(MCP `script-execute`, 읽기 전용) | ✅측정 |
| **소스** | 씬/프리팹 YAML·`.inputactions` JSON·`.cs` 원문을 직접 읽음 | ✅소스 |
| **미확인** | 확인 경로가 없거나 라이브 검증이 필요 | ⚠️미확인 |

- **읽기 전용 준수:** 씬·프리팹·코드 **수정 0건**. `script-execute`는 두 번만 썼고 둘 다 조회다.
  Meta 실측은 `HideFlags.HideAndDontSave` 스크래치 오브젝트(씬 직렬화 대상에서 제외)에 컴포넌트를 붙였다 즉시
  `DestroyImmediate` 하는 방식 — 대상 클래스에 `ExecuteAlways`/`ExecuteInEditMode`가 **0건**이라 라이프사이클도 안 돈다.
  조사 전후 열린 씬은 `QuickStart` / `RootCount 4` / `IsDirty:true`로 **동일**(dirty는 조사 이전부터).
- **Play 모드 진입 안 함.** 열린 `QuickStart`가 이미 dirty였기 때문에 저장 프롬프트 리스크를 만들지 않았다.
  대신 Meta는 리플렉션으로 직접 읽었고(§1), 룸별 배치는 씬 YAML로 전수 확인했다(§2).
- 툴: `Assets`+`Library/PackageCache` 전체의 `.cs.meta`/`.dll.meta`에서 **guid→스크립트 11,764개 맵**을 만들어
  씬/프리팹의 `m_Script` guid를 타입 이름으로 역해석했다. UGUI·FishNet·XRI 컴포넌트까지 이름이 붙는다.

### 지형 메모 (이 조사에서 확정)

| 씬 | 정체 | RoomCore | Addressable 주소 |
|---|---|---|---|
| `Assets/App/Scenes/QuickStart.unity` | **QuickTest 부트 씬**(EditorBuildSettings의 유일한 enabled 씬, index 0) | 없음 | **없음** |
| `Assets/App/Scenes/T_RoomA.unity` | 샘플 룸 A (QuickStart의 기본 `roomSceneKey`) | 없음 | `Scenes/T_RoomA` |
| `Assets/App/Scenes/T_RoomB.unity` | 샘플 룸 B | 없음 | `Scenes/T_RoomB` |
| `Assets/App/Scenes/AssembleRoom.unity` | `/assemble-room`이 만든 5층 룸 (**우리 콘텐츠가 사는 유일한 룸**) | ✅ 1개 | `AssembleRoom` |
| `Assets/App/Scenes/QuickRoom.unity` | 잔여물(층 헤더 없음, 씬 EventSystem 1개) | 없음 | 없음 |

- QuickStart의 `QuickTestStarter.roomSceneKey = "Scenes/T_RoomA"`(✅소스,
  `XumFlow-studio/Assets/App/Scenes/QuickStart.unity:155`) — **기본 QuickTest는 콘텐츠가
  하나도 없는 T_RoomA를 연다.** 화면에서 본 `Players list` / `Leave game` / `Input Selection`은 전부 이 조합이다.
- **"출시 빌드 포함"의 의미:** studio의 출하물은 **Addressables 콘텐츠 번들**(`Default Local Group` = 룸 씬 3종 +
  `Default Prefab Objects` = 네트워크 프리팹 10종)이다. `QuickStart.unity`는 **어느 그룹에도 없다.**
  → QuickStart 안에만 있는 것(FishNet HUD, XR Interaction Simulator)은 **콘텐츠 번들에 안 나간다.**
  `HotUpdate` 그룹은 현재 **비어 있다**(✅소스).

---

## 1. S1 — 우리 소유 (레지스트리 콘텐츠) ✅측정

`IToggleableContent` 구현체는 **정확히 6개**, 전부 `App.HotUpdate` 어셈블리(✅측정 — 리플렉션 전수 순회).
아래 Meta 값은 **런타임 인스턴스에서 읽은 실측값**이다(소스 리터럴 추정이 아니다).

| Id | DisplayName | **Category** | DefaultOn | MutuallyExclusive | Icon | 소스 파일 |
|---|---|---|---|---|---|---|
| `chair-sit` | 의자 착석 | **상호작용** | False | `[]` | **NULL** | `PromptScene/Content/ChairSit/ChairSitContent.cs:34` |
| `chat` | 채팅 | **소통** | False | `[]` | **NULL** | `PromptScene/Content/Chat/ChatContent.cs:25` |
| `grabbable-props` | 잡기 소품 | **물체** | False | `[]` | **NULL** | `PromptScene/Content/GrabbableProps/GrabbableProps.cs:22` |
| `ruler` | 룰러 | **측정** | False | `[]` | **NULL** | `PromptScene/Content/Ruler/RulerContent.cs:11` |
| `score-hud` | 점수판 | **게임** | False | `[]` | **NULL** | `PromptScene/Content/ScoreHud/ScoreHud.cs:36` |
| `target-props` | 과녁 | **게임** | False | `[]` | **NULL** | `PromptScene/Content/TargetProps/TargetProps.cs:32` |

경로 접두사는 `XumFlow-studio/Assets/App/Scripts/ContentLogic/`.

### ⚠️ Category 전수 결과 — 셸 탭 설계에 직접 영향

- **실측 5종: `상호작용` · `소통` · `물체` · `측정` · `게임`.** (`게임`만 2개, 나머지는 각 1개.)
- **`Meta.Category`를 읽는 코드는 프로젝트 전체에 0건**(✅소스, grep `\.Category`). `CrossPlatformRoomHud`는
  레지스트리를 **평평하게** 순회해 4개/페이지로 페이징할 뿐 Category로 묶지 않는다. 아이콘도 Category가 아니라
  **Id**로 찾는다(`HudIcons.ByContentId`).
  → 셸이 Category로 탭을 만들면 **셸이 이 필드의 첫 소비자**가 된다. 지금 값은 "1기능 1카테고리"에 가까워서
  탭으로 쓰면 탭 5개 중 4개가 항목 1개짜리다. **매핑 테이블이 필요하다**(§7-2 판정).
- **Icon은 6개 전부 NULL**(✅측정, 그리고 `AssembleRoom.unity`의 두 인스턴스도 `icon: {fileID: 0}` ✅소스).
  아이콘은 Meta가 아니라 `HudIcons`(Material Symbols 서브셋 폰트, Id→코드포인트)가 공급한다 —
  6개 Id 전부 + 액션 `clear` 매핑 존재(✅소스 `PromptScene/UI/HudIcons.cs:54-68`).
- **DefaultOn 6/6 False, MutuallyExclusive 6/6 빈 배열** → 셸에 "기본 켜짐" / "상호배타 그룹" UI는 **현재 불필요**.
  (계약상 존재하지만 소비자가 없다 — 그리지 않는다.)

### 룸별 실제 배치 (✅소스, 씬 YAML 전수)

| 씬 | RoomCore | 배치된 콘텐츠 |
|---|---|---|
| `AssembleRoom` | ✅ | `ChairSitContent`, `ChatContent`(+`ChatWorldPanel`), `CrossPlatformRoomHud`, `XRWorldClicker` |
| `T_RoomA` / `T_RoomB` / `QuickRoom` / `QuickStart` | 없음 | **0개** |

⚠️ **즉, 지금 어떤 룸에도 6개가 다 올라가 있지 않다.** 셸을 "레지스트리 6개가 다 보이는 상태"로 테스트하려면
룸에 나머지 4종을 얹는 선행 작업이 필요하다(`/add-component`). 셸 자체의 결함이 아니라 **테스트 전제**다.

---

## 2. 통합 표 — 모든 UI 표면 (S2 + S3 포함)

**종류:** 토글 / 액션 / 값 / 표시전용 · **소유자:** 우리 / studio / SDK
**빌드 포함:** `콘텐츠✅` = Addressables 룸 씬에 포함(출하) · `QT만` = QuickStart 전용(출하 안 됨)

| 표시명 | 종류 | 위치 (씬/프리팹/스크립트) | 소유자 | 백엔드 실재 | 빌드 포함 | 입력 바인딩 | 이동·숨김 | 제안 분류 |
|---|---|---|---|---|---|---|---|---|
| **아이콘 토글 ×6** (레지스트리 자동) | 토글 | `AssembleRoom` › `CrossPlatformRoomHud`(Canvas **World Space**) / `PromptScene/UI/CrossPlatformRoomHud.cs` | **우리** | ✅ 있음 (`IToggleableContent.SetEnabled`) | 콘텐츠✅ | 마우스 좌클릭 · XR `UI Press`(트리거) | 가능(우리 씬 오브젝트) | **기능** |
| **측정 지우기** | 액션 | 위와 동일 (런타임 생성, `ruler` 있을 때만) | **우리** | ✅ 있음 (`RulerMeasurementView.ClearAll` 리플렉션) | 콘텐츠✅ | 위와 동일 | 가능 | **기능** |
| **페이지 넘김(드래그/휠/점)** | 값 | `Viewport` › `HudPager` / `PromptScene/UI/HudPager.cs:23` | **우리** | ✅ 있음 | 콘텐츠✅ | 드래그 · 마우스 휠(`IScrollHandler`) | 가능 | **기능**(셸 내부) |
| **채팅 패널**(IMGUI: 입력창·전송·스크롤) | 액션+값 | `ChatContent.cs:146` `OnGUI` | **우리** | ✅ 있음 (FishNet RPC 채널) | 콘텐츠✅ | `Enter`/`KeypadEnter` 전송, 마우스 | 가능 | **기능** |
| **채팅 월드 패널**(VR: `InputField`+시스템 키보드) | 액션+값 | `ChatVR/ChatWorldPanel.cs`, `ChatVR/SystemKeyboardBinder.cs`(`IPointerClickHandler`) | **우리** | ⚠️ 코드는 있음, **실기기 미검증**(주석에 `UNPROVEN ON DEVICE` 명시) | 콘텐츠✅ | XR 셀렉트/포인터 클릭 → `TouchScreenKeyboard` | 가능 | **기능** |
| **점수판**(IMGUI) | 표시전용 | `ScoreHud.cs:129` `OnGUI` | **우리** | ✅ 있음 (`IEventBus` 구독) | 콘텐츠✅ | 없음 | 가능 | **기능** |
| **승자 배너**(IMGUI) | 표시전용 | `Compositions/TargetShootoutMatch/TargetShootoutMatch.cs:146` `OnGUI` | **우리** | ✅ 있음 (서버권위 집계) | 콘텐츠✅ | 없음 | 가능 | **기능** |
| **의자 `E` 프롬프트 배지** | 표시전용 | `PromptScene/UI/KeyBadge.cs` (런타임 부착) | **우리** | ✅ 있음 | 콘텐츠✅ | `E`(§4-A 충돌) | 가능 | **기능** |
| **Players list** | 액션 | `T_RoomA`/`T_RoomB`/`AssembleRoom` › `RoomHudView`(Canvas **Screen Space Overlay**) › `PlayerListButton` / `ContentLogic/PlayerListButton.cs` | **studio** | ❌ **없음** — §6 참조 | 콘텐츠✅ | 마우스 좌클릭(Overlay) | 가능(우리 씬 파일) | **미분류 → 개척 청구서** |
| **Leave game** | 액션 | 위 캔버스 › `LeaveButton` / `ContentLogic/LeaveRoomButton.cs` | **studio** | ❌ **없음** — §6 참조 | 콘텐츠✅ | 마우스 좌클릭(Overlay) | 가능 | **나가기(단, 우리가 새로 지어야)** |
| **MessageWindow**(`- Ownership Change -`) | 표시전용 | 룸 씬 › `Canvas`(World Space) › `MessageWindow` (TMP) | **studio** | ❌ **없음** — 이 텍스트를 쓰는 코드 0건(✅소스 grep) | 콘텐츠✅ | 없음 | 가능 | **셸 밖(방치)** |
| **FishNet HUD** (`Stop Server` / `Stop Client`) | 액션 | `QuickStart` › `NetworkManager`(FishNet **Demos** 프리팹) › 중첩 `NetworkHudCanvas` / `com.firstgeargames.fishnet/Demos/Scripts/NetworkHudCanvases.cs:85` `OnGUI` | **SDK** | ✅ 있음 (`ServerManager`/`ClientManager` 연결 토글) | **QT만** | 마우스(IMGUI, 화면 좌상단 `Rect(4,110)`) | 프리팹 인스턴스 제거는 가능 / 스크립트는 PackageCache=읽기전용 | **셸 밖(방치)** |
| **XR Interaction Simulator 메뉴** (`Input Selection` · `Close/Open` · 장치/액션 패널) | 액션+표시 | `QuickStart` › `XR Interaction Simulator`(XRI 3.3.1 **샘플** 프리팹) / `Samples/.../XRInteractionSimulatorPlayModeMenu.cs` | **SDK(샘플)** | ✅ 있음 (시뮬레이터 조작) | **QT만** | **`Y`** = Input Selection 열기/닫기, **`X`** = Action 메뉴 (+§4의 대량 키) | Assets 하위 샘플이라 편집 가능하나 **건드리지 말 것** | **셸 밖(방치)** |
| **XumNet Diagnostics UI** | 액션+표시 | `com.oxr-sdk.xumnet/Runtime/Diagnostics/XumNetDiagnosticsUI.cs:232` `OnGUI` | **SDK** | ✅ 있음 | ❌ **어느 씬에도 미배치**(샘플 씬에만) | — | — | **셸 밖(방치)** |
| **`IconButtonTemplate`** | (템플릿) | `AssembleRoom` (비활성, `active=0`) | **우리** | n/a — 런타임 복제용 원본 | 콘텐츠✅ | 없음 | 가능 | **셸 밖**(내부 구현) |

### 화면에서 확인된 3건의 정체 (요구 사항)

1. **Players list** = studio 소유 `PlayerListButton`. `RoomHudView` Screen Space Overlay 캔버스의 `buttons`
   (HorizontalLayoutGroup) 아래. → **§6**.
2. **Leave game** = studio 소유 `LeaveRoomButton`. 같은 캔버스, 같은 레이아웃. → **§6**.
3. **Input Selection · Close/Open · `Y`** = **Unity XRI 3.3.1 "XR Interaction Simulator" 샘플**의 플레이모드 메뉴.
   `QuickStart.unity`에 프리팹 인스턴스로 놓여 있다(guid `58d0a4ac…`, ✅소스
   `XumFlow-studio/Assets/App/Scenes/QuickStart.unity:359-415`).
   `Y` 바인딩의 원본은 `XR Interaction Simulator Controls.inputactions` › `UI` 맵 ›
   `ToggleInputSelectionMenu` ← `<Keyboard>/y`(✅소스). UI 라벨 `"Close/ Open"`도 샘플 프리팹에 있다.
   **→ 우리 것이 아니다. 우리 룸 씬에는 없다. 출하 번들에도 안 나간다.**

---

## 3. 캔버스·EventSystem 지형 (셸 배치에 직접 영향)

| 씬 | 캔버스 | 렌더 모드 |
|---|---|---|
| `AssembleRoom` | `CrossPlatformRoomHud` | **World Space** |
| `AssembleRoom` / `T_RoomA` / `T_RoomB` | `Canvas`(MessageWindow) | **World Space** |
| `AssembleRoom` / `T_RoomA` / `T_RoomB` | `RoomHudView`(Players/Leave) | **Screen Space – Overlay** |

⚠️ **`RoomHudView`가 Overlay라는 건 HMD에서 안 보인다는 뜻이다.** Overlay 캔버스는 XR에서 렌더되지 않고
`TrackedDeviceGraphicRaycaster`도 없다(`GraphicRaycaster`만). → **Players/Leave는 데스크톱 전용 표면**이다.
셸이 크로스플랫폼이라면 이 둘을 "흡수"하는 건 **World Space로 다시 짓는 것**과 같다.

### EventSystem 개수 (studio 2개 혼재 이력의 실체) ✅소스

- 룸 씬 자체엔 EventSystem이 **0개**(`AssembleRoom`/`T_RoomA`/`T_RoomB`/`QuickStart`). `QuickRoom`만 씬에 1개.
- 런타임에 **두 곳에서 생긴다**:
  1. `QuickTestStarter.EnsureClientInputInfrastructure()` — EventSystem이 하나도 없으면
     `[QuickTest] EventSystem` + **`StandaloneInputModule`** 생성 (`QuickTestStarter.cs:130-159`).
  2. 아바타 프리팹 — `Desktop.prefab` 경로 `Desktop/OnlyClient/EventSystem`에 EventSystem +
     **`InputSystemUIInputModule`**. `UnityXR.prefab`/`XrealXR.prefab`은 EventSystem + **`XRUIInputModule`**.
     `NetworkEnabler`(`OnlyClient` 위)가 **오너 클라에서만** 자식을 켜므로 원격 아바타는 EventSystem을 안 켠다.
- ⇒ **QuickTest에서는 ①이 먼저 생기고 ②가 뒤에 스폰되어 2개가 공존한다.** 입력 모듈 종류도 다르다
  (`StandaloneInputModule` vs `InputSystemUIInputModule`/`XRUIInputModule`).
  Unity는 `EventSystem.current`를 **하나만** 유효하게 두고 경고를 낸다 → **어느 쪽이 이기느냐에 따라 uGUI 클릭
  경로가 달라진다.** 셸을 uGUI로 지을 때 반드시 밟게 될 지점이다. ⚠️미확인: 실제로 어느 쪽이 `current`가 되는지는
  라이브 1회 확인이 필요(현 조사는 읽기 전용이라 미실행).

---

## 4. S4 — 입력 바인딩 전수 + 충돌표 ⛔

### 4-A. 확정 사실

- **`activeInputHandler: 2` (Both)** ✅소스 `ProjectSettings/ProjectSettings.asset:967` — HANDOFF 기록과 일치, 재확인됨.
  레거시 `UnityEngine.Input`과 신형 Input System이 **둘 다 동작**한다.
- **프로젝트 전역(project-wide) Input Actions = `XRI Default Input Actions.inputactions`**
  (✅소스 `EditorBuildSettings.asset` › `m_configObjects.com.unity.input.settings.actions` → guid `c348712b…`).
  → **XRI 액션맵은 런타임에 자동 활성**된다.
- **`Assets/InputSystem_Actions.inputactions`(Unity 템플릿 기본)는 사실상 죽어 있다.** `preloadedAssets`에만
  올라 있고 어떤 `PlayerInput`/UI 모듈/컴포넌트도 참조하지 않는다(✅소스, guid `052faaac…` 역참조 = ProjectSettings 1건뿐).
  → 그 안의 `Jump=Space`, `Interact=E`, `Crouch=C`, `Previous=1`, `Next=2` 등은 **바인딩되어 있지 않다.**
- **`OVRInput` 0건** ✅소스 — Meta 전용 SDK는 프로젝트에 없다. XR 입력은 전부 **OpenXR + XRI**다.
- **UXRM `UnifiedMoveInputActions`**(WaypointManager용)는 **어느 씬/프리팹에도 미배치** ✅소스 → 비활성.

### 4-B. 충돌표 — 키·버튼 → 동작 → 소유자 → 소스

**A) 데스크톱 키보드 · 마우스**

| 키 | 동작 | 소유자 | 소스 | 살아있는 범위 |
|---|---|---|---|---|
| `W` `A` `S` `D` | 아바타 이동 (`Horizontal`/`Vertical` 레거시 축) | **우리(app)** | `ContentLogic/DummyController.cs:10-11` + `ProjectSettings/InputManager.asset`(alt 버튼 a/d/w/s) | 룸 전역 |
| `←` `→` `↑` `↓` | 위와 **동일 축**(negative/positive 버튼) | **우리(app)** | `InputManager.asset` | 룸 전역 |
| `E` | **의자 착석/기립** | **우리** | `Content/ChairSit/ChairSitContent.cs:206-212` (`kb.eKey.wasPressedThisFrame`, 폴백 `Input.GetKeyDown`) | `chair-sit` ON일 때 |
| `Enter` / `KeypadEnter` | 채팅 전송 (IMGUI 포커스 조건부) | **우리** | `Content/Chat/ChatContent.cs:159-161`, `ChatVR/ChatWorldPanel.cs:200` | `chat` ON일 때 |
| `마우스 좌클릭` | 월드 클릭(룰러 측정·과녁 명중) | **우리** | `Core/SimpleClickProvider.cs:58` (+`SuppressWorldClick`·`IsPointerOverGameObject` 가드) | 룸 전역 |
| `마우스 좌클릭` | 데스크톱 그랩 | **우리(app)** | `ContentLogic/DesktopMouseGrabInteractor.cs:92` | 오너 아바타 |
| `마우스 휠` | 셸 HUD 페이지 넘김 | **우리** | `PromptScene/UI/HudPager.cs:89` | HUD 위에서 |
| `T` | NetCube 색 토글 | studio | `ContentLogic/NetCubeColorToggle.cs:19,70` | ❌ **어느 룸에도 미배치** → 실질 free |
| `W A S D` / 화살표 | uGUI Navigate | SDK(XRI) | `XRI Default Input Actions` › `XRI UI` 맵 | 항상(프로젝트 전역 액션) |
| `Enter`/`Space` = Submit, `Esc` = Cancel | uGUI Submit/Cancel | SDK(XRI) | 위와 동일 (`*/{Submit}`, `*/{Cancel}`) | 항상 |

**A-2) XR Interaction Simulator (QuickStart 전용 — 그러나 QuickTest 판정 내내 살아있음)**
소스: `Samples/XR Interaction Toolkit/3.3.1/XR Interaction Simulator/XR Interaction Simulator Controls.inputactions` ✅소스

| 키 | 동작 |
|---|---|
| **`Y`** | **Input Selection 메뉴 열기/닫기** ← 화면에서 본 그것 |
| **`X`** | Action 메뉴 열기/닫기 |
| `W`/`S` | Z 이동, `A`/`D` | X 이동, `Q`/`E` | Y 이동 |
| `↑↓←→` | 키보드 회전 |
| `V` / `C` / `Z` | X / Y / Z 축 구속 |
| `R` | 리셋 · `Tab` | 장치 순환 · `H` | 헤드 조작 토글 |
| `[` / `]` | 좌/우 컨트롤러 조작 토글 |
| `` ` `` | Quick Action 순환 · `Space` | Quick Action 수행 |
| `Shift` | 좌측 장치 액션 · `9` / `0` | Primary/Secondary 2D축 타깃 토글 |
| `마우스 우클릭` | 마우스 조작 토글 · `마우스 이동/휠` | 회전/스크롤 |

**B) XR 컨트롤러 (`XRI Default Input Actions`, 프로젝트 전역 = 항상 활성)** ✅소스

| 버튼 | 동작 | 비고 |
|---|---|---|
| `트리거`(TriggerButton) 좌/우 | `Activate` + **`UI Press`** + 월드 클릭(`XRWorldClicker`가 select 엣지에서 `SubmitExternalRay`) | 우리 HUD 클릭도 이것 |
| `그립`(GripButton) 좌/우 | `Select`(잡기) + `Teleport Mode Cancel` + `Grab Move` | |
| `썸스틱`(Primary2DAxis) 좌/우 | `Move` / `Turn` / `Snap Turn` / `Teleport Mode` / `UI Scroll` / 조작 축 | 로코모션 프로바이더 실재(`XR Origin (XR Rig)` › `Move`/`Turn`/`Teleportation`/`Grab Move`/`Jump`) |
| `썸스틱 클릭`(Primary2DAxisClick) 좌/우 | `Scale Toggle` | |
| **`A`(오른손 PrimaryButton)** | **`Jump`** (`JumpProvider` 실재) | **선점됨** |
| **`X`(왼손 PrimaryButton)** | — | **미바인딩 = FREE** |
| **`Y`(왼손 SecondaryButton)** | — | **미바인딩 = FREE** |
| **`B`(오른손 SecondaryButton)** | — | **미바인딩 = FREE** |
| **`menu`** | — | 액션 에셋에 바인딩 0건이나, Quest에서는 런타임/OS가 시스템 메뉴로 가져간다 → **회피**(브리프 지시와 일치) |

XR 리그 구성(✅소스): `UnityXR.prefab` → `XR Origin Hands (XR Rig)`(XRI Hands 샘플) → `XR Origin (XR Rig)` +
`Left_NearFarInteractor` / `Right_NearFarInteractor`. **핸드 메뉴(`HandMenuRig`) 프리팹은 포함되지 않았다** —
XRI 기본 핸드 메뉴와 충돌할 일은 없다.

### 4-C. ⛔ 텍스트 입력 중 핫키 억제 — **장치 없음 (현존 버그)**

프로젝트에 존재하는 억제 장치는 **`SimpleClickProvider.SuppressWorldClick` 하나뿐**이고, 이건 **월드 "클릭"만**
막는다(클레임 기반, `Core/SimpleClickProvider.cs:24-33`). **키보드 핫키를 막는 장치는 0건**(✅소스, grep 전수).

실제로 깨지는 시나리오 (전부 코드 경로로 확인):

| 상황 | 결과 | 원인 |
|---|---|---|
| 채팅 입력창에 `e` 타이핑 | **의자에 앉거나 일어난다** | `ChairSitContent.SitKeyPressed()`가 `Keyboard.current.eKey`를 조건 없이 읽음. IMGUI `TextField`는 `Event.current`만 소비하고 `Keyboard.current`는 못 막는다 |
| 채팅 입력창에 `wasd` 타이핑 | **아바타가 걸어간다** | `DummyController.Update()`가 `Input.GetAxis`를 조건 없이 읽음 |
| 채팅 입력창에 `Space` 타이핑 (QuickTest) | 시뮬레이터 Quick Action 발동 | XR Simulator `Toggle Perform Quick Action` |

**신설 비용 견적(구현 착수 아님):** `IRoomCore` 서비스로 `ITextInputFocus`(또는 `SuppressWorldClick`과 동형의
정적 클레임 API)를 하나 추가하고 — 계약 §4.5 메커니즘-비정책, `SuppressWorldClick` 선례와 동형이므로
**IRoomCore 인터페이스 무변경**으로 가능 — ①`ChatContent`/`ChatWorldPanel`이 포커스 시 클레임,
②`DummyController`·`ChairSitContent`가 읽기 전에 확인. **터치 지점 4곳, SYSTEMS 파일 1개(`SimpleClickProvider`
옆에 추가), 계약 무수정.** M3에서 `SuppressWorldClick`을 클레임화한 작업과 같은 크기다(= 세션 내 소품).
⚠️ 단 `DummyController`는 `ContentLogic/` 루트(우리 PromptScene 폴더 밖, studio 앱 코드)라 **소유권 확인 필요**.

### 4-D. 미사용 키 목록 (소환 키 후보)

두 맥락 모두에서 비어 있는 키만 골랐다 — **(A) 출하 룸**(시뮬레이터 없음)과 **(B) studio QuickTest**(시뮬레이터 있음).

| 키 | (A) 출하 룸 | (B) QuickTest | 평가 |
|---|---|---|---|
| **`M`** | free | free | ⭐ **1순위** — 니모닉(Menu), 어느 맥락에도 충돌 없음 |
| **`F1`** | free | free | ⭐ **2순위** — 관습적 "도움말/메뉴", 텍스트 입력과 절대 안 겹침(§4-C가 안 고쳐져도 안전) |
| `G` `I` `J` `K` `L` `N` `O` `P` `U` `B` | free | free | 후보군 |
| `F2`~`F12` | free | free | 후보군 |
| `1`~`8` | free | free | 후보군(단 `9`/`0`은 시뮬레이터가 사용) |
| `Tab` | free | ❌ 시뮬레이터 장치 순환 | 비추천 |
| `` ` `` `Space` `Shift` `R` `H` `V` `C` `Z` `[` `]` `X` `Y` | free | ❌ 시뮬레이터 | **제외** |
| `Esc` | ❌ uGUI Cancel | ❌ | **제외** |

> **`F1`을 강하게 권함(2순위지만 실질 동률):** §4-C가 미해결인 동안 `M`은 채팅 타이핑 중 셸을 열어버린다.
> `F1`은 텍스트 입력에 절대 섞이지 않으므로 **억제 장치 없이도 안전**하다. 억제 장치를 먼저 만들면 `M`이 낫다.

**XR 소환 버튼 후보:** **왼손 `secondaryButton`(Y) ⭐1순위** / 왼손 `primaryButton`(X) 2순위 / 오른손
`secondaryButton`(B) 3순위. `menu`는 회피, 오른손 `primaryButton`(A)은 Jump가 선점.
(왼손을 권하는 이유: 오른손은 트리거·그립·썸스틱이 상호작용/로코모션으로 가장 바쁘다.)

---

## 5. S5 — 설정 후보의 백엔드 실재 확인

| 후보 | 백엔드 실재 | 근거 | 그릴 수 있나 |
|---|---|---|---|
| **그래픽 품질** | ✅ **있음(제한적)** | `QualitySettings.names = ["Mobile","PC"]`, **레벨 2개뿐**, 현재 `1`(PC) ✅측정. 각 레벨이 별도 URP 에셋을 물고 있다(`Mobile_RPAsset` `renderScale 0.8` / `PC_RPAsset` `renderScale 1`) ✅소스 | ✅ **그린다** — 단 **슬라이더 아님, 2지선다 토글/세그먼트** |
| **렌더 스케일** | ✅ **있음** | `currentRenderPipeline = PC_RPAsset (UniversalRenderPipelineAsset)`, `renderScale` **get=True/set=True**, `msaaSampleCount` set=True ✅측정 | ✅ 그릴 수 있음 (0.5~1.5 슬라이더). ⚠️ 주의: **에셋을 직접 쓰므로 프로젝트 에셋이 더럽혀진다** — 에디터 플레이 후 값이 남는다. 런타임 전용 클론 필요 |
| **해상도(데스크톱)** | ✅ 있음 | `Screen.SetResolution` — Unity 표준 API, 별도 인프라 불요 | ✅ 그릴 수 있음 (단 현재 이걸 쓰는 코드 0건 = 신규) |
| **XR renderViewportScale / eyeTextureResolutionScale** | ⚠️ **미확인** | `XRSettings.enabled=False`, `renderViewportScale=0`, `eyeTextureResolutionScale=0` ✅측정 — **XR 런타임이 안 붙은 에디터 상태의 값**이라 판정 불가. 코드에서 이 API를 쓰는 곳 0건 | ⚠️ **HMD에서 1회 확인 전까지 그리지 말 것** |
| **Foveated rendering** | ❌ **없음** | `foveat*` 심볼 프로젝트 전역 0건 ✅소스. OpenXR foveation 설정 노출부 없음 | ❌ 개척 청구서 |
| **마스터 볼륨** | ✅ 있음 | `AudioListener.volume = 1` ✅측정. **`AudioMixer` 에셋은 프로젝트에 0개**(`*.mixer` 전역 0건 ✅소스), 코드에서 `AudioMixer`/`AudioListener` 참조도 0건 | ✅ 그린다 — **`AudioListener.volume` 슬라이더 하나뿐.** 그룹별(음성/효과음/BGM) 분리는 **불가** |
| **효과음 / BGM 볼륨** | ❌ **없음** | 룸 씬·프리팹 전체에서 `AudioSource`는 아바타 프리팹 3종의 **음성 출력 앵커 1개씩**이 전부 ✅소스. BGM/SFX 소스 0개 | ❌ 조절할 실물 없음 → 개척 청구서 |
| **마이크 음소거** | ✅ **있음 (진짜)** | **MetaVoiceChat이 실재하고 아바타에 배선되어 있다.** `Desktop.prefab`/`UnityXR.prefab`/`XrealXR.prefab` 루트에 `XumMetaVc`(=`MetaVc` 상속) + `VcMicAudioInput` + `VcAudioSourceOutput` + `XumFishNetNetProvider` + `XumMicrophonePermissionRequester` ✅소스. 타입 전부 로드됨 ✅측정. 노출 상태: **`isInputMuted`**(내 마이크) / **`isDeafened`**(전체 안 듣기) / `isOutputMuted`(특정 원격) / `isSpeaking`(표시용) — 전부 `MetaSerializableReactiveProperty<bool>` ✅소스 `Assets/MetaVoiceChat/MetaVc.cs:44-51` | ✅ **그린다 — 마이크 토글 + 전체 음소거(deafen) 토글 2개.** ⚠️미확인: 2클라 음성 실동작은 **라이브 미검증** |
| **마이크 장치 선택** | ❌ 없음 | `VcMicAudioInput`에 장치 선택 UI/파라미터 노출 확인 안 됨, 이를 쓰는 코드 0건 | ❌ 개척 청구서 |
| **음성 입출력 볼륨(게인)** | ❌ 없음 | `VcConfig`는 Opus 코덱·지터 설정만(볼륨/게인 필드 없음) ✅소스 | ❌ 개척 청구서 |
| **말하는 사람 표시** | ✅ 있음 | `isSpeaking` 리액티브 프로퍼티 ✅소스 | ✅ 그릴 수 있음 — **설정이 아니라 "참가자" 표면**(§7-1 참조) |
| **닉네임/표시명** | ⚠️ 부분 | `A_DisplayName` 컴포넌트가 아바타에 있음 ✅소스. 편집 경로는 미조사 | ⚠️미확인 |

**요약: 지금 그릴 수 있는 설정 = ① 그래픽 품질(Mobile/PC 2지선다) ② 마스터 볼륨 슬라이더
③ 마이크 음소거 토글 + 전체 음소거 토글.** 나머지는 §8 청구서.

---

## 6. S6 — "Leave game"과 "Players list"의 정체 ⛔

두 버튼 다 **studio 소유**이고, **둘 다 지금 아무 일도 하지 않는다.** 두 개의 독립된 경로가 각각 끊겨 있다.

### 경로 ① 직렬화된 `onClick` (인스펙터) — 둘 다 깨져 있음 ✅소스

| 버튼 | 직렬 호출 | 상태 |
|---|---|---|
| `LeaveButton` | `MasterServerToolkit.Examples.BasicSpawner.RoomHudView.Disconnect` | **타깃 `fileID: 0` = NULL.** 게다가 **`MasterServerToolkit` 어셈블리가 studio 프로젝트에 아예 없다**(`.cs` 0건 ✅소스) → 원본 XumFlow 앱 씬에서 넘어온 **화석** |
| `PlayerListButton` | ① `RoomHudView.SetActive(**false**)` (타깃 = `RoomHudView` **자기 자신**)<br>② `SetActive(true)` — **타깃 NULL** | ①은 **살아있다**. ②는 죽었다 |

⛔ **관측 가능한 결함:** `Players`를 누르면 **`RoomHudView` 캔버스 전체가 사라진다**(Players 버튼과 Leave 버튼이
그 안에 있으므로 **같이 사라진다**). 보여줘야 할 목록 패널은 참조가 끊겨 **아무것도 안 뜬다.**
그리고 HUD를 다시 켤 버튼이 없다 → **되돌릴 수 없는 상태.** (T_RoomA / T_RoomB / AssembleRoom **3개 씬 전부 동일.**)

### 경로 ② 코드 (`AddListener`) — 구독자 0 ✅소스

```
LeaveRoomButton.RequestLeave()      → RoomEventBridge.OnLeaveRequested            → 구독자 0건
PlayerListButton.OnClick()          → RoomEventBridge.OnShowPlayersListRequested  → 구독자 0건
```

주석이 지목하는 실제 처리자 **`RoomLeaveHandler`(AOT, XumRunTime)** 는 studio 프로젝트 어디에도 **존재하지 않는다**
(`Assets` + `Library/PackageCache` + `LocalPackages` 전역 grep 0건 ✅소스). 브리지만 있고 반대편이 없다.

- `RoomEventBridge.cs:10-17` — `OnLeaveRequested` / `OnShowPlayersListRequested` 정의부만 존재.
- `LeaveRoomButton.cs:8-13`의 주석은 `→ RoomLeaveHandler.HandleLeave() → FishNet ClientManager.StopConnection()
  → showGamesListView`라고 적고 있다 — **설계 의도이지 실재가 아니다.**

### 판정

| 질문 | 답 |
|---|---|
| Leave game이 뭘 하나 | **아무것도 안 한다.** 의도상으로는 **방 퇴장**(FishNet 연결 종료 → 로비 목록 복귀)이지 계정 로그아웃도 앱 종료도 아니다 |
| confirm 단계가 있나 | **없다.** 확인 다이얼로그 0건 |
| 셸의 세 번째 섹션 이름 | **"나가기"**(방 퇴장) — 로그아웃 아님 |
| 흡수 가능한가 | **불가.** 흡수할 동작이 없다. 셸이 "나가기"를 가지려면 **우리가 구현**해야 한다 |

⚠️미확인: XumFlow **runtime 플레이어**(`c:\J_0\XumFlow`)에 `RoomLeaveHandler`가 있는지는 확인 불가 —
해당 프로젝트가 이 머신에 없다. **studio에서는 죽어 있다**는 것만 확정.

---

## 7. 흡수 / 숨김 / 방치 분류안

원칙: **SDK 소유는 방치**(MessageWindow 선례) · **백엔드 없는 컨트롤은 그리지 않는다**(D6 규율의 UI판).

### 7-1. 흡수 (셸이 가져간다)

| 대상 | 소유자 근거 |
|---|---|
| 아이콘 토글 ×6 + 측정 지우기 + 페이지 넘김 | **우리가 만든 `CrossPlatformRoomHud`.** 셸은 사실상 이것의 확장 — 새로 짓는 게 아니라 **감싸는 것** |
| 그래픽 품질 / 마스터 볼륨 / 마이크·전체 음소거 | 백엔드 실재 확인됨(§5). 소유자 없음(= 아무도 안 그리고 있음) → 셸이 첫 소유자 |
| **"나가기"(신규 구현)** | 흡수가 아니라 **신설**. 아래 §7-2 참조 |
| (선택) 말하는 사람 표시 | `isSpeaking` 실재. "참가자" 표면을 만들 때 |

### 7-2. 숨김 (우리 씬 파일이라 끌 수 있다 — 끄는 것이 맞다)

| 대상 | 이유 |
|---|---|
| `RoomHudView`의 **`Players list`** | **깨져 있고, 누르면 HUD가 사라져 복구 불능**(§6). 셸을 얹기 전에 **비활성이 안전**. 남겨두면 셸을 지워버린다 |
| `RoomHudView`의 **`Leave game`** | 죽은 버튼. 셸이 진짜 나가기를 갖게 되면 **중복 + 오해 유발**. 단 **셸의 나가기가 실제로 동작하기 전에는 끄지 말 것**(둘 다 없는 상태가 더 나쁨 — 지금은 어차피 둘 다 안 되지만) |
| `MessageWindow` (`- Ownership Change -`) | 쓰는 코드 0건. **표시전용 화석.** 다만 studio 샘플 룸의 일부라 **방치도 무해** — 우선순위 낮음 |

> ⚠️ 이 항목들은 **T_RoomA/T_RoomB/AssembleRoom 3개 씬에 각각 들어 있다.** 하나만 고치면 나머지 둘이 남는다.
> `/assemble-room`이 샘플 룸을 복제하는 구조이므로 **새 룸을 만들 때마다 되살아난다** → 스킬 차원의 처리가 맞다.

### 7-3. 방치 (SDK 소유 — 손대지 않는다)

| 대상 | 근거 |
|---|---|
| **FishNet HUD**(`Stop Server`/`Stop Client`) | `com.firstgeargames.fishnet` **PackageCache = 읽기 전용**(가드 훅이 차단). **QuickStart 전용이라 출하 번들에 안 나간다.** QuickTest에서는 오히려 **유용한 도구** |
| **XR Interaction Simulator 메뉴**(`Y`/`X`) | XRI 3.3.1 샘플. **QuickStart 전용, 출하 안 됨.** 시뮬레이터 없이는 데스크톱 XR 검증이 불가능 → **제거하면 검증 능력이 준다.** `Y`를 비우려고 이걸 지우는 건 손해 |
| **XumNet Diagnostics UI** | 어느 씬에도 미배치. 방치 이상의 조치 불필요 |

---

## 8. 개척 청구서 — 백엔드가 없어서 못 그리는 것

> D6 규율: **의향만 기록한다.** 여기 있는 항목은 셸에 컨트롤을 그리지 않는다.

| # | 항목 | 없는 것 | 비용 감각 |
|---|---|---|---|
| **I-1** | **"나가기"(방 퇴장)** | `RoomEventBridge.OnLeaveRequested`의 **구독자**. studio에 `RoomLeaveHandler`가 없다 | ⚠ **코드-대체 가능**: FishNet `ClientManager.StopConnection()`은 직접 도달 가능(우리 코드가 이미 `InstanceFinder`를 쓴다). 다만 **"나간 뒤 어디로 가는가"**(로비 복귀 = MST `showGamesListView`)는 studio에 로비가 없어 **정의되지 않음** → QuickTest에선 "연결 끊김" 이상은 불가. **확인 다이얼로그도 신설** |
| **I-2** | **Players list(참가자 목록)** | 마스터 서버 조회 경로. 브리지 반대편 부재 | ⛔ **개척**: 목록의 출처가 MST이고 studio는 MST를 안 쓴다. 대안 = **FishNet 관측자 기반 로컬 목록**(아바타 순회 + `A_DisplayName` + `isSpeaking`) → 이건 코드-대체 ⚠로 가능하고, **셸의 네 번째 섹션("참가자")의 실질 후보** |
| **I-3** | 마이크 **장치 선택** | 장치 열거/전환 API 노출부 | ⛔ 개척 (`VcMicAudioInput` 내부 수정 필요 = MetaVoiceChat 에셋 손대기) |
| **I-4** | 음성 **입출력 볼륨/게인** | `VcConfig`에 게인 필드 없음 | ⛔ 개척 |
| **I-5** | **효과음/BGM 볼륨 분리** | `AudioMixer` 에셋 0개, BGM/SFX `AudioSource` 0개 | ⛔ 개척 — **조절할 소리 자체가 없다.** 믹서 도입은 소리가 생긴 뒤의 일 |
| **I-6** | **XR 렌더 스케일 / foveation** | XR 런타임 미부착 상태라 판정 불가 + foveation 노출부 0건 | ⚠️미확인 → **HMD 1회 실측이 선행 조건** |
| **I-7** | **텍스트 입력 중 핫키 억제** | 억제 장치 자체가 없음(§4-C) | ⚠ **코드-대체**: `SuppressWorldClick` 클레임 패턴 복제, 계약 무수정, 터치 4곳. **셸보다 먼저 해야 한다**(`M` 키를 쓰려면 필수) |
| **I-8** | 닉네임 편집 | `A_DisplayName`은 있으나 편집 경로 미조사 | ⚠️미확인 |
| **I-9** | 해상도 변경 | API는 표준(`Screen.SetResolution`)이나 **현재 쓰는 코드 0건** = 신규 | ⚠ 코드-대체(작음). XR에서는 무의미하므로 **데스크톱 전용 섹션** 문제 동반 |

---

## 9. S8 — 판정 질문 답변

**① 설정 / 기능 / 나가기 3분류로 전부 담기는가?**
아니오. **네 번째가 필요하다.** 다만 `Players list`를 그대로 흡수해서 담는 게 아니라 —
그건 죽은 버튼이다(§6) — **"참가자"** 섹션을 **새로 짓는** 것이 후보다(I-2, 로컬 아바타 순회 기반이면 코드-대체 ⚠).
현재 백엔드가 실재하는 재료: 아바타 목록 · `A_DisplayName` · `isSpeaking` · `isOutputMuted`(개별 음소거).
**참가자 섹션을 안 만든다면 3분류로 담긴다** — 흡수 대상이 아무것도 안 남기 때문이다.

**② 실측 Category와 셸 탭 구조가 맞는가?**
**안 맞는다. 매핑이 필요하다.** 실측 5종(`상호작용/소통/물체/측정/게임`)은 **1기능 1탭**에 가깝고
(`게임`만 2개), `Category`를 읽는 코드가 **0건**이라 지금까지 아무 의미도 갖지 않았다.
권고: **셸의 "기능" 섹션은 현 6개를 평평하게 두고**(지금 `CrossPlatformRoomHud`가 하는 방식 = 4개/페이지 페이징),
탭은 **설정/기능/나가기(/참가자)** 로만 나눈다. Category 기반 그룹핑은 **콘텐츠가 10개를 넘을 때** 재검토한다.
그때 Category를 셸의 정본으로 쓰려면 값 체계부터 다시 정해야 한다(현재 값은 **분류가 아니라 라벨**에 가깝다).

**③ 소환 키 최종 후보**
- **데스크톱: `F1`(1순위) / `M`(2순위).** `F1`은 텍스트 입력에 안 섞여 **I-7 미해결 상태에서도 안전**.
  I-7을 먼저 닫으면 `M`이 더 낫다(니모닉). **`Y`·`X`·`Tab`·`Space`·`` ` ``·`Esc`는 제외**(§4-D).
- **XR: 왼손 `secondaryButton`(Y) 1순위**, 왼손 `primaryButton`(X) 2순위.
  `menu` 회피(브리프 지시 + Quest OS 선점), 오른손 `A`는 `JumpProvider` 선점.

**④ 텍스트 입력 중 핫키 억제 장치가 있는가? 없으면 신설 비용은?**
**없다 — 현존 버그다**(채팅에 `e` → 착석, `wasd` → 이동). 신설 비용은 **작다**:
`SuppressWorldClick`과 동형의 클레임 API 1개(SYSTEMS 파일 1개 추가, **계약 무수정**) + 소비 지점 4곳
(`ChatContent`, `ChatWorldPanel`, `DummyController`, `ChairSitContent`).
⚠️ `DummyController`는 우리 PromptScene 폴더 밖(studio 앱 코드)이라 **수정 소유권 확인 필요**.

**⑤ Leave game 흡수 가능한가?**
**불가 — 흡수할 동작이 없다.** 직렬 `onClick`은 NULL 타깃 + 존재하지 않는 어셈블리(MST) 참조, 코드 경로는
구독자 0, 처리자 `RoomLeaveHandler`는 studio에 부재. **우리가 만들어야 한다**(I-1). 그리고 만들 때
"어디로 나가는가"가 studio에는 정의돼 있지 않다는 점이 먼저 결정돼야 한다.

**⑥ 실제로 그릴 수 있는 설정 항목만 남긴 목록**

| 컨트롤 | 종류 | 백엔드 |
|---|---|---|
| 그래픽 품질 | **2지선다**(Mobile / PC) — 슬라이더 아님 | `QualitySettings.SetQualityLevel` (레벨 2개 ✅측정) |
| 렌더 스케일 | 슬라이더 0.5–1.5 | URP `renderScale` set=True ✅측정. ⚠ 에셋 오염 주의(런타임 클론 필요) |
| 마스터 볼륨 | 슬라이더 0–1 | `AudioListener.volume` ✅측정 (믹서 없음 → **이게 유일한 볼륨**) |
| 마이크 음소거 | 토글 | `MetaVc.isInputMuted` ✅소스 (⚠ 2클라 음성 라이브 미검증) |
| 전체 음소거(안 듣기) | 토글 | `MetaVc.isDeafened` ✅소스 (동일 단서) |

그 외 전부 §8 청구서.

---

## 10. 이 조사가 새로 뒤집은 것 (기존 문서 대비)

1. **"Y 키 충돌"의 소유자 확정** — 우리 것도 studio 것도 아닌 **XRI 샘플**, 그리고 **QuickStart 전용**.
   출하 룸에서는 `Y`가 비어 있다. (그래도 QuickTest 내내 살아있으므로 후보에서 제외하는 결론은 유지.)
2. **`Players list` / `Leave game`이 둘 다 죽어 있다** — 특히 `Players list`는 **누르면 HUD 전체가 사라지는
   복구 불능 상태**를 만든다. 3개 룸 씬 전부. 셸 작업 전 **선행 처리 대상**.
3. **실측 Category 5종** — 지금까지 가정한 "측정/소통/게임"에 `상호작용`·`물체`가 추가되고,
   **아무도 이 필드를 읽지 않는다**는 사실이 확인됨.
4. **음성 채팅이 실재한다** — MetaVoiceChat + Opus + Silero VAD가 아바타 프리팹 3종에 배선되어 있다.
   "마이크 토글은 허구일 것"이라는 브리프의 가정은 **틀렸다**. 마이크/전체 음소거는 **그릴 수 있다**.
5. **AudioMixer는 0개** — 볼륨은 `AudioListener.volume` 하나뿐. 그룹별 볼륨은 청구서행.
6. **EventSystem 2개 공존의 실체** — 씬이 아니라 **QuickTestStarter(StandaloneInputModule) + 아바타 프리팹
   (InputSystemUIInputModule / XRUIInputModule)** 두 출처. 입력 모듈 종류까지 다르다.
7. **`RoomHudView`는 Screen Space Overlay** → **HMD에서 안 보인다.** Players/Leave는 데스크톱 전용 표면.
8. **`InputSystem_Actions.inputactions`는 죽어 있다** — 프로젝트 전역 액션은 `XRI Default Input Actions`.

---

## 11. 정지 규칙 준수 확인

- 고치고 싶어진 것 **5건**(§6 Players 복구불능 / §4-C 핫키 억제 / MessageWindow 화석 / Leave 신설 /
  Category 값 체계) — **전부 고치지 않고 이 문서에 기록만 했다.**
- PackageCache·매니페스트 **읽기만** 함(수정 0건).
- 백엔드 불확실 항목은 전부 **⚠️미확인**으로 표기(XR 렌더스케일, 음성 라이브 동작, EventSystem 승자,
  닉네임 편집, runtime 플레이어의 `RoomLeaveHandler`).

---

*관련: [grab-ownership-survey.md](grab-ownership-survey.md)(정찰 문서 선례) · [prediction-survey.md](prediction-survey.md)(견적 문서 선례) · [capability-map.md](capability-map.md)(재조합✅/코드-대체⚠/개척⛔ 등급) · [build-studio-room.md](build-studio-room.md) §5·§6(HUD·XR world-click 절차 SSOT) · [promptscene-content-contract.md](promptscene-content-contract.md) §3b·§4.5 · HANDOFF §8*
