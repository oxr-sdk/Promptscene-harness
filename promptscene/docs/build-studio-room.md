# build-studio-room.md — Studio(XumFlow) 룸 조립·검증 (겪은 절차)

> **범위·정직 표기.** 이 문서는 XumFlow **studio** 프로젝트(`c:\J_0\XumFlow-studio`, 콘텐츠 저작/Addressables 모델)에서
> **실제로 겪은 것만** 적는다. XRCollabDemo용 문서(`build-working-room.md`·`build-xumlobby-server.md`·`build-desktop-client.md`)는
> **병존**하며 지우지 않는다(다른 모델 = 단일 프로젝트에 런타임+콘텐츠 동거). studio 계약 포인터는 `promptscene-content-contract.md §1`에 이미 있음.
> ⚠️ **배포(Smart Deploy / Build & Package / Bundle Uploader)는 아직 미경험 → 이 문서에 없음.** 별도 `build-studio-deploy.md`는 겪은 뒤 작성.
> 세션 로그·근거는 [xumflow-migration.md](xumflow-migration.md) §7~§9. 이 문서 = **재사용 절차 SSOT**.

---

## 0. studio 모델 (XRCollab과 다른 점 — 조립 전 전제)

| | XRCollabDemo | **studio(XumFlow)** |
|---|---|---|
| NetworkManager | 룸 씬 SYSTEMS 내 | **부트 씬(QuickStart/T_Master)** — 룸 씬엔 없음. RoomCore는 `FishNet.InstanceFinder`(전역)로 접근 |
| 룸 로드 | 빌드 씬리스트 | **Addressables 씬**(`Options.Addressables=true`), 로드 키 = **leaf**(`<Room>`) 폴백 |
| 프리팹 컬렉션(C1) | baked DefaultPrefabObjects + Room.exe 재빌드 | **`Assets/DefaultPrefabObjects.asset`** (FishNet Generator 재생성) + Addressables `Network/DefaultPrefabObjects` (런타임 스왑) |
| 코어/콘텐츠 위치 | `Assets/PromptScene/` | **`Assets/App/Scripts/ContentLogic/PromptScene/`** (`App.HotUpdate` 어셈블리, 별도 asmdef 불요) |
| 검증 | Master/Room.exe + 에디터 클라 조인 | **Quick Test Mode**(QuickStart + QuickTestStarter, `startAsServer`+`hostMode`), MCP 구동 |
| 실행 토폴로지 | Master.exe+Room.exe | 단일 client.exe 두 모드(배포 시); 에디터=QuickTest host |

**API 병존 확인(중요):** studio의 `XumNet`(@06584e0)·`FishNet`(4.6.17) 시그니처가 XRCollab과 **동일** — `XumNetwork.Instantiate(NetworkObject,Vector3,Quaternion,NetworkConnection)`(static, 클라=null 반환), `InstanceFinder.*`, `[ServerRpc(RequireOwnership=false)]`·`[ObserversRpc(BufferLast=true)]`. → **PromptScene.Core/FEATURE 소스는 verbatim 이식**(migration §9 §2 대조 결과 API 차이 0). `App.HotUpdate` references가 이미 `FishNet.Runtime`/`XumNet.Runtime`/`XR.Interaction.Toolkit`/`UnifiedXRMotion`/`InputSystem`/`UI`/`App.Bridges` 커버.

---

## 1. 길 1 — 샘플룸 복제로 룸 생성

1. **복제:** `Assets/App/Scenes/T_RoomB.unity` → `Assets/App/Scenes/PromptSceneRoom_N.unity` (`AssetDatabase.CopyAsset` — 바이트 복사라 `--PLAYER_SPAWNER`의 FishNet SceneId 보존).
2. **Content Manager 등록(Scenes 탭 Scan→Apply):** 주소 = **leaf 씬 이름**(`PromptSceneRoom_N`, `Scenes/` 접두어 불요 — `RoomScene` 라벨 자동), 그룹 `Default Local Group`. 실제 write = `settings.AddLabel("RoomScene")` → `CreateOrMoveEntry(guid, group)` → `entry.address=leaf` → `entry.SetLabel("RoomScene")` → `SaveAssets` (`ContentManagerWindow.RegisterScenes` 동형).
   - ⚠️ **GUI `Apply`는 백엔드 씬이름 중복검사(로그인 게이트, 401)를 먼저 탄다**(`ContentManagerWindow.cs:1068`). **로컬 QuickTest 베이스라인은 이 검사 불요** → Addressables write만 직접 재현하면 됨(타인 번들 충돌 가드는 원격 배포 때만). 스킬은 등록을 이 직접-write로 하고, 실배포 전 GUI Apply(로그인)로 재확인.
3. **베이스라인 QuickTest**(우리 코드 얹기 전 정상 기준): §4 절차로 Play → 룸 로드·아바타 `Desktop(Clone)` 스폰·UXM 모션 rig·Error 0 확인. 여기서 실패면 복제/등록 문제(우리 코드 탓 아님).

---

## 2. Core / FEATURE 이식

- **⚠ Core는 XumFlow 베이스에 없음(로컬 전용/untracked).** `ContentLogic/PromptScene/` 전체가 studio git에서 `??`(untracked) — 새로 XumFlow를 클론/커밋받으면 `PromptScene.Core.RoomCore`가 **없어** `FindType`이 하드 실패한다. 그래서 **Core는 스킬이 스펙으로 들고 부트스트랩한다**: `assemble-room/assets/core/{RoomCore,Contracts,RoomContentRegistry,SimpleClickProvider}.cs`(verbatim 스펙) → 타입 부재 시에만 프로젝트 `Core/`로 복사 → refresh → `isCompiling==false` + AppDomain 타입 로드 확인(컴파일+도메인 리로드 게이트, **씬 열기 전 독립 단계**). 로컬 Core가 이미 있으면 **덮어쓰지 않음**(더 최신일 수 있음). `.meta`는 미동봉(Unity 재생성; Core는 타입명/`using` 참조라 GUID 불요). 상세: assets/core/README.md.
- **위치:** `ContentLogic/PromptScene/Core/`(Contracts·RoomContentRegistry·SimpleClickProvider·RoomCore) + `ContentLogic/PromptScene/Content/<Feature>/`. **별도 asmdef 불요**(App.HotUpdate 안).
- **소스 무개조 이식**(§0 API 병존). 추가/수정 후 `AssetDatabase.Refresh` → **`EditorApplication.isCompiling==false` 확인 + AppDomain에 타입 로드 확인**(= 0 에러의 결정적 신호; script-execute로 `GetTypes()` 조회).
- RoomCore는 `Awake`에서 4서비스 등록(`IInteraction`=SimpleClickProvider(자동 AddComponent) / `INetSpawn`=FishNetSpawn / `IRoomUserState`=로컬스텁 / `IEventBus`=인프로세스). FEATURE는 `Start`에서 `RoomCore.Instance.Contents.Register(this)` 자기등록.

---

## 3. 씬 계층 (5층) + ⚠ SceneId 재부모 안전절차

목표 계층(contract §1): `===== SYSTEMS / ENVIRONMENT / UI / FEATURES / COMPOSITIONS =====`. **골격은 다섯 층을 모두 빈 폴더로 예약**(contract §1 "층의 존재 vs 내용" 규칙 — `/assemble-room`가 FEATURES·COMPOSITIONS를 빈 채로 항상 생성, 내용은 수요 시 add-component). studio 실측 편차: **Network 하위폴더 없음**(NM=부트씬), **_DYNAMIC=런타임만**(런타임 생성물 전용이라 정적 골격엔 없음). ⚠ 손으로 만든 PromptSceneRoom_1은 처음엔 COMPOSITIONS를 안 만들었으나(§9 시점), 이 규칙 확정(2026-07-24) 후 골격은 빈 COMPOSITIONS를 예약한다.

- **비-네트워크 오브젝트**(Light/Plane/Canvas 등) 재부모: SceneId 무관 → 일괄 이동 후 QuickTest 1회.
- **⚠ FishNet 씬 네트워크 오브젝트**(`--PLAYER_SPAWNER`) 재부모(예: SYSTEMS/Player로): SceneId 재생성 위험. **안전 4단계:**
  1. persistent 오픈 씬에서 `transform.SetParent`.
  2. `EditorSceneManager.SaveScene`(FishNet `sceneSaving` 훅 발화).
  3. `NetworkObject.SceneId != 0` **&&** `IsSceneObject == true` 재확인(리플렉션; `SceneId`는 field, `IsSceneObject`는 property).
  4. QuickTest 아바타 스폰 유지 확인.
  - **실측:** SYSTEMS/Player로 재부모해도 SceneId(예 `4290510823`) **불변**·IsSceneObject=True·스폰 유지 PASS. **"SceneId=0 함정"은 *한 script-execute 안 생성→배치→저장*에 국한** — persistent 오픈 씬 재부모+SaveScene은 훅 정상 발화로 보존. `CreateSceneId(force)`는 폴백(불요였음).

## 3b. 직렬화 지뢰 회피 (DETAILS.md 규칙)

- 새 직렬화 스크립트 지양(기본 컴포넌트+Inspector). **ScriptableObject 금지**(HybridCLR 붕괴).
- 커스텀 직렬화 MonoBehaviour → **씬에 직접 박기**(별도 Prefab 자산 지양). 핵심 지뢰: **씬 로더는 hot MonoBehaviour SerializedField를 채우나, Prefab-자산 로더는 안 채우는 케이스**.
- `[Serializable]` 데이터 컨테이너(List<Foo>) → **App.Bridges(baked)**, hot 두면 미스매치.
- NetworkBehaviour(FishNet RPC) = 검증됨. → **승인 패턴: 프리팹=기본 컴포넌트(NetworkObject/XR Grab/Rigidbody), hot 뷰 직렬 필드=씬 임베드 or 런타임 코드 배선.**
- **적용 실증(Ruler):** `RulerMeasurement.prefab`=NetworkObject+RulerMeasurementView(뷰의 LineRenderer/TextMesh는 런타임 `BuildOrUpdate`에서 생성, lineWidth/lineColor는 코드 기본값) → Prefab-로더 미채움 지뢰에 안전. `RulerContent.measurementPrefab`(씬 MonoBehaviour의 GameObject 필드)은 **씬 임베드 배선**(scene 로더가 채움).
- **⭐ XRI 절 (base 어셈블리 컴포넌트 = 프리팹 직렬화 OK — GrabbableProps 실증, migration §11.3):** XR Grab Interactable·Rigidbody 등 **XRI/물리 컴포넌트는 base(패키지, immutable) 어셈블리**라 **프리팹에 직접 박고 인스펙터로 설정해도 필드값이 보존된다**(NetCube 선례 + `GrabbableProp.prefab` 디스크·스폰 인스턴스 양쪽 실측: `m_ThrowOnDetach`/`ownershipMode`/client-auth 플래그 전부 유지). 즉 **XRI FEATURE 프리팹 = base 컴포넌트(XR Grab Interactable/Rigidbody/NetworkObject/XumView/NetworkTransform) 직접 직렬화 + hot 뷰는 직렬 필드 0(런타임 배선)**. hot 뷰(GrabbableView)는 XRI 이벤트만 배선(`selectEntered.AddListener` in `OnStartClient`) → ChatChannelView와 동형(직렬 필드 0 = Prefab-로더 미채움 지뢰 무관). **함정:** FishNet 스폰 콜백(`OnStartClient`)은 스폰 **다음 틱**에 발화 → AddListener도 한 틱 지연(스폰 당프레임엔 미배선; MCP 검증은 한 틱 뒤 확인).

## 3c. 네트워크 프리팹 등록 (신 C1)

`Assets/App/Prefabs/`에 프리팹(NetworkObject 포함) 생성 → **FishNet Generator 재생성**:
```
FishNet.Editing.PrefabCollectionGenerator.Generator.GenerateFull(null,false,true)  // 리플렉션
→ Assets/DefaultPrefabObjects.asset 프로젝트 스캔 재생성(프리팹 편입)
→ RegisterDefaultPrefabObjectsInAddressables (addr "Network/DefaultPrefabObjects")
+ 프리팹 개별 Addressables 엔트리 "Network/Prefabs/<이름>" (그룹 "Default Prefab Objects")
```
검증: `DefaultPrefabObjects.GetObjectCount()` + `GetObject(true,i).name`에 프리팹 존재. (SETUP §4-1 "Network Prefabs 탭 Apply & Generate"와 동형.)

---

## 4. QuickTest 검증 (MCP 자동판정)

1. 룸 편집 → `scene-save`. (Addressables "Use Asset Database"는 **디스크** 로드 → 룸 변경은 반드시 저장.)
2. `QuickStart.unity` 열고 `QuickTestStarter`(SerializedObject) 세팅: `startAsServer=true` + **`hostMode=true`**(단일 에디터 아바타 관측 필수) + `roomSceneKey=<leaf>`. (QuickStart 인메모리만, 디스크 미저장 → 테스트 후 shipped값 원복.)
3. `EditorApplication.isPlaying=true` (script-execute) → 서버 시작 → Addressables 룸 로드 → 아바타 스폰.
4. 판정: `scene-list-opened`(룸 로드) + `scene-get-data`/`gameobject-find`(아바타 `Desktop(Clone)`·오브젝트) + 리플렉션(`RoomCore.Instance`·`Contents.All`·서비스). **`console-get-logs`는 "2 event systems" 경고 폭주 → Error 필터 + 씬/오브젝트 직접 조회가 확실.**
5. `EditorApplication.isPlaying=false` + QuickTestStarter 원복.

- **측정 주입(하네스):** 단일 에디터 MCP는 실제 마우스 이동 불가 → `Physics.Raycast`(바닥 Plane)로 실 RaycastHit 획득 → private `RulerContent.OnClick(hit)` 리플렉션 2회. **정직 캐비엇: "실제 마우스 클릭 이벤트→레이캐스트"는 미검증**(D2/M4 동일 경계) — 검증된 것은 OnClick 이후 측정·스폰·RPC 전파.
- **실증(§9):** §3 베이스라인 / §4 RoomCore(4서비스·SYSTEMS 무손상) / §5 Ruler(자기등록·SetEnabled·측정 스폰 중점+LineRenderer 전파+라벨 √거리·IsSpawned) — 전부 MCP PASS, Error 0.

---

## 5. 크로스플랫폼 룸 UI (World Space uGUI + XRI) — 입력소스 독립

FEATURE를 데스크톱/Meta/XREAL/(태블릿/Vision) 어디서나 **포인팅**으로 조작하는 HUD. IMGUI(데스크톱 전용) 대신 **World Space uGUI**.

- **저작 방식(런타임 생성 아님):** 캔버스·버튼을 **실제 씬 GameObject로 저작·저장**(에디터 편집 가능). 런타임 코드는 **배선만**(studio 정석 = `LeaveButton`의 `LeaveRoomButton` per-button hot 스크립트가 onClick을 런타임 AddListener). **직렬화 onClick→hot 메서드는 target=null로 안 잡힘**(LeaveButton `Disconnect` 실측) → **런타임 배선 필수(3b)**.
- **캔버스:** World Space Canvas + `GraphicRaycaster`(데스크톱 마우스, InputSystemUIInputModule) + **`TrackedDeviceGraphicRaycaster`**(XR ray/poke, XRUIInputModule) — 둘 다 붙여 입력소스 독립. eventCamera(`canvas.worldCamera`)는 **런타임에 활성 카메라로 배정**.
- **⚠ 빌보드 필수:** World Space `GraphicRaycaster`는 `ignoreReversedGraphics=true` 기본 → **뒷면 캔버스는 mirror + 클릭 불가.** 고정 회전 대신 **매 프레임 카메라 향하기**(`LookRotation(pos - cam.pos)`)로 앞면 보장(=정방향 + 클릭 가능). 실측: 고정 Y=180이 뒷면→둘 다 실패, 빌보드로 해소.
- **⚠ 한글 폰트:** studio엔 **한글 TMP/폰트 자산이 없음**(전부 `LiberationSans SDF` = 라틴). → **레거시 uGUI `Text` + 동적 OS 폰트**(`Font.CreateDynamicFontFromOSFont(["Malgun Gothic",...],24)`)로 OS 글리프 폴백 렌더(IMGUI가 한글 되던 것과 같은 엔진 폴백). TMP `CreateFontAsset` 런타임 경로는 NRE로 불안정 — 레거시 Text 채택. (실기기 한글 = 번들 한글 폰트 필요 = 개척 청구서.)
- **SuppressWorldClick:** 패널 배경 `EventTrigger` PointerEnter/Exit(마우스·XR 둘 다 발화)로 `SimpleClickProvider.SetWorldClickSuppressed` 클레임 → 버튼 클릭이 바닥 측정으로 안 샘.
- **재사용:** 레지스트리(`RoomCore.Contents`)만 읽어 어느 룸에도 얹힘. Ruler 전용 "측정 지우기"는 `GetById("ruler")` 런타임 조회로만(없는 룸엔 미표시).
- **IMGUI 대안(참고):** `OnGUI`(Event.current 자체 처리, EventSystem/입력모듈 무관)는 **데스크톱 전용**이지만 studio의 "활성 EventSystem 2개 혼재"(아바타 InputSystemUIInputModule + `[QuickTest]` StandaloneInputModule)에 영향 0 — 데스크톱 빠른 확인용으로만.

---

## 6. XRI 인터랙터 / XR 입력 (에디터 시뮬 검증 범위)

- **아바타별 인터랙터(실측):** `Desktop`=XR 인터랙터 없음(마우스+InputSystemUIInputModule) / `UnityXR`·`XrealXR`=**`XRUIInputModule`+`NearFarInteractor`+`XRPokeInteractor`** (전부 **`XR Origin Hands (XR Rig)/Camera Offset/{Left,Right} Hand/` 아래** — **컨트롤러와 손이 같은 인터랙터 공유**).
- **XR 월드-클릭 브리지(`XRWorldClicker`):** 컨트롤러/손 select(트리거/핀치) 엣지(`NearFarInteractor.logicalSelectState.wasPerformedThisFrame`)에 — **UI 위가 아니면**(`TryGetCurrentUIRaycastResult`==false) — 인터랙터 레이(`((IXRRayProvider)nf).GetOrCreateRayOrigin()`)를 월드 레이캐스트해 **`SimpleClickProvider.SubmitExternalRay(ray)`** 호출(마우스 클릭과 동일 핸들러). RulerContent 무변경. `SubmitExternalRay`는 계약 §4.5 **mechanism 추가**(IInteraction 무변경). 인터랙터 종류 무관 순회 → **손도 동일 코드로 커버**(코드 0 추가).
- **DetectRuntimePlatform:** WindowsEditor는 **활성 XR 로더 이름에 openxr/oculus**가 있어야 `xr.meta`(UnityXR) 스폰, 아니면 `desktop.windows`. **로더 미활성 시 에디터 XR 테스트는 스포너 매핑을 임시로 UnityXR로 강제**(Windows 엔트리 prefab→UnityXR, 저장) 후 테스트, **끝나면 반드시 원복**.
- **XR Interaction Simulator(HMD 없이):** `SimulatedDeviceLifecycleManager.deviceMode`(setter 없음 → `m_DeviceMode` 필드 + `m_DeviceModeDirty=true`)로 **Hand↔Controller** 전환. **컨트롤러 모드 = 성립**(레이+트리거로 World Space UI 클릭 + 바닥 측정, 사람 판정 PASS). **손 모드 = "Hand Actions are currently not interactive. They only change the hand shape"** — 시뮬은 손 select를 발화 안 함 + poke는 근접이라 원거리 패널 도달 불가 → **손 라이브 시연 불가(=실기기 V2).**
- **패키지(정정):** `com.unity.xr.hands`·`com.unity.xr.openxr`·`xr.management`·`xr.core-utils`가 **PackageCache에 존재**(XRI 3.3.1 전이 의존; manifest 명시 핀은 xr.interaction.toolkit 3.3.1 + inputsystem뿐). → migration §3a "studio엔 openxr/xr.hands 없음"은 **manifest 명시 핀 기준**이었음(전이 resolve로는 존재). 단 **XR 로더 미활성**이라 자연 감지=desktop.

---

## 6.5 COMPOSITION 배선 (COMPOSITIONS 층 + 네트워크 권위 프리팹 + 집계 루프) — ✅ 2026-07-24 (migration §14)

FEATURE들을 게임 루프로 조율하는 **COMPOSITIONS 층**. FEATURE 이식(§2)과 다른 절차: 새 씬 층 + 네트워크 권위 프리팹 + 서버권위 집계. 실증 = TargetShootoutMatch(과녁 점수전).

- **COMPOSITION 스크립트 = plain MonoBehaviour(IRoomContent 아님)** → `Contents` 레지스트리 **미등록**. 씬에 상주하며 `Start`에서 버스 구독. (FEATURE=자기등록 / COMPOSITION=씬 상주·미등록 — 등록 모델이 다름.) 이벤트 **타입만** 참조(TargetHitEvent/ScoreChangedEvent), FEATURE **클래스** 참조 0(grep 확인) → FEATURE↔FEATURE 참조 0 불변.
- **네트워크 권위 프리팹 = ChatChannelView 동형(§Chat/§10 재사용).** `MatchView.prefab` = **NetworkObject + hot 뷰**(NetworkBehaviour). 상행 `[ServerRpc(RequireOwnership=false)]`(발신자=서버 주입 `NetworkConnection sender=null`, 위조 불가) + 하행 `[ObserversRpc]` 방송. **신규 플랫폼 API 0.** 씬측 COMPOSITION과는 **static 이벤트+Latest 스냅샷**으로 디커플. 렌더러 없는 불가시 오브젝트(ChatChannel 형). hot 뷰 직렬 필드는 **코드 기본값**(field initializer)이면 Prefab-로더 미채움 지뢰 무관.
- **C1:** MatchView 저장 시 FishNet PrefabGenerator 자동 편입 + `RunFishNetGenerateFull` 재확인(§3c). `DefaultPrefabObjects` count +1.
- **COMPOSITIONS 층 생성:** 씬 root에 `===== COMPOSITIONS =====`(빈 GameObject) — contract §1에 **정의된 층을 처음 채우는 것**(구조 변경 아님). 자식에 COMPOSITION MonoBehaviour + `matchPrefab`→프리팹 **씬 임베드 배선**(3b). COMPOSITION MonoBehaviour는 NetworkObject 아니라 SceneId 무관(§3 재부모 이슈 없음); MatchView는 런타임 스폰(_DYNAMIC).
- **스폰-또는-재사용:** COMPOSITION `EnsureMatch`가 IsClientStarted 뒤 MatchView **1개만** 스폰(2클라 각자 스폰 방지 = ChatContent 채널 패턴, 트랩 I 재시도 흡수).
- **집계 루프(서버권위):** 명중(FEATURE HitEvent 발행)→COMPOSITION 구독→`ReportHit`(ServerRpc)→**서버만 집계**→ObserversRpc 방송→ScoreChangedEvent 발행→ScoreHud 표시→선취 N점 승자→resetDelay 후 리셋. **집계·승패·리셋은 전부 서버.**
- **§5 QuickTest(단일 host) 판정:** COMPOSITION 상주(미등록)·MatchView spawn-once·**실제 점수 루프**(명중 3회→집계 1→2→3→승자 방송→리셋 빈 보드, `[MatchView] scoreboard ...` 전 전이 로그)·ScoreHud 실 수신(주입 아님)·Error 0. **주입 함정:** 가림(ENVIRONMENT Capsule 등) 없는 가시 과녁을 골라 `SubmitExternalRay`(레이가 엉뚱한 콜라이더에 먼저 맞으면 명중 안 됨).
- **정직:** 단일 host라 "서버권위"는 구조로 성립하나 **2클라 점수 동기 파리티는 2번째 프로세스 필요**(§7 큐). 실 마우스클릭→명중 원경로는 `SubmitExternalRay` 경계.

---

## 6.6 2클라 클론 절차 (ParrelSync — 겪은 것만, 2026-08-19)

> studio에서 **에디터 2개로 2인**을 돌리는 절차. SSOT 상세·트랩표 = [xumflow-migration.md](xumflow-migration.md) §17.
> 전제: QuickTest는 MST가 아니라 **FishNet 직결 `localhost:7770`** (A=server, B=client).

### 6.6.1 한 번만 하는 준비

1. **ParrelSync 1.5.3 도입 — `.unitypackage`를 풀어 `Assets/ThirdParty/ParrelSync/` 로 배치.**
   ⛔ UPM git URL로 넣지 말 것 = `Packages/manifest.json` 수정이 된다. `.unitypackage`는 tar.gz라 각 엔트리의 `pathname`/`asset`/`asset.meta`를 재조립하면 **GUID를 보존한 채 원하는 경로로** 옮길 수 있다.
   확인: 컴파일 0 · `ParrelSync` asmdef 타입 적재 · 메뉴 `ParrelSync/Clones Manager` 노출 · `ClonesManager.IsClone()==false`(원본).
2. **역할 브리지: `Assets/PromptScene/Harness/Editor/`** — 소스 미러 = [assets/two-client-harness/](assets/two-client-harness/)(studio는 gitignore 대상이라 레포엔 미러만 있다)
   - `QuickTestRoleBridge.cs` — 클론이면 런타임에 `startAsServer/hostMode`를 `false`로 덮는다.
   - `CloneHarness.cs` — 클론 전용 자동 Play · 파일 명령 채널 · 1초 프로브 로그.
   - ⚠ **여기는 `Editor` 폴더이고 asmdef가 없어 `Assembly-CSharp-Editor`에 들어간다.** 이게 필수다 — `QuickTestStarter`(`Assembly-CSharp`)와 `ParrelSync`(Editor asmdef)를 동시에 볼 수 있는 유일한 자리이고, `App.HotUpdate`(hot-update DLL)를 오염시키지 않는다.
3. **클론 생성:** `ParrelSync/Clones Manager` 창, 또는 코드로 `ClonesManager.CreateCloneFromCurrent()`.
   MCP에서 부를 땐 **`EditorApplication.delayCall` 로 감싸라** — 복사가 동기라 MCP 요청이 타임아웃난다. 완료 판정은 `<프로젝트>_clone_0/.clone` 파일 존재로.
   비용 실측: 약 **3 GB**, 1분 미만(Library·Packages 복사).

### 6.6.2 매 세션 절차 (순서가 중요)

1. **A 준비** — `QuickStart` 의 `Starter` 에 `startAsServer✅ + hostMode✅ + roomSceneKey=<룸>` 을 넣고 **씬을 저장**한다.
   ⚠ 저장해야 한다. `playModeStartScene` 이 걸려 있으면 Play는 **디스크의 씬 에셋**을 로드하므로 미저장 편집은 날아간다(기존 "roomSceneKey가 되돌아간다" 증상의 정체).
2. **B 실행** — `Unity.exe -projectPath <clone> -logFile <경로>`. **`-logFile` 필수** — B 판정은 전부 이 로그로 한다.
   ⚠ **무장하지 말고 먼저 띄운다.** 콜드 오픈+컴파일에 약 5분이 걸리고, 그동안 A가 host로 떠 있어야 한다(QuickTest 클라는 **재시도가 없다** — 한 번 실패하면 끝).
3. **A를 Play** — host로 띄우고 `IsServerStarted` 와 자기 아바타를 확인한다.
4. **B 무장** — 클론 **루트**에 `.promptscene-autoplay` 생성 → `CloneHarness`가 Play에 진입하고 브리지가 client로 뒤집는다.
   로그에서 이 두 줄을 확인: `role-detect role='client'` / `apply role=client applied=True startAsServer:True->false`.
5. **판정** — B의 1초 프로브 한 줄에 전부 들어 있다:
   `probe[tick] srv=… cli=… cid=… scenes=[…] nobs=N | id=<objId> <name> owner=<IsOwner> ownerCid=<n> pos=… | chatLog=…`
6. **B 조종** — 클론 **루트**에 `.promptscene-cmd` 파일로 한 줄 명령(소비 후 자동 삭제):
   `probe` / `chat <문구>` / `move <dx> <dy> <dz>` / `stop` / `quit`
   ⛔ **`Assets/` 안에 두면 안 된다** — 심링크라 원본까지 무장·조종된다.
7. **원상복구** — B `quit` → 무장 파일 삭제 → A Play 종료 → `QuickStart` 값 복원·저장.

### 6.6.3 이 절차가 우회하는 것 / 반드시 아는 함정

- **입력 포커스 함정을 아예 안 만난다.** 에디터 2개 중 활성창만 실입력을 받지만, 이 절차는 A를 MCP로, B를 파일 명령으로 몬다. 대신 **실 키보드 2인 조작은 증명되지 않는다.**
- **심링크 = 씬·설정·소스 공유.** "B만 다른 씬"은 불가능하고, 공유 Assets를 편집하면 **양쪽이 같이 재컴파일**된다(B가 Play 중이면 끊긴다).
- ⛔ **클론의 MCP가 시작 몇 분 뒤 NuGet 재복원 → AssetDatabase refresh → 도메인 리로드**로 B의 Play를 끊는다. 조인 실패를 넷코드 탓하기 전에 B 로그에서 `[Unity-MCP DependencyResolver] Restoring` 을 찾을 것. **클론에서 MCP를 끄는 게 근본 대응.**
- ⛔ **클론 에디터를 종료하면 원본의 `unity-mcp-server` 프로세스가 같이 죽는다**(에디터 자체는 멀쩡, 자동 재기동 없음). 복구:
  `Library/mcp-server/win-x64/unity-mcp-server.exe port=21017 plugin-timeout=10000 client-transport=streamableHttp authorization=none`
- **MCP 포트 충돌은 없다** — 포트는 `SHA256(프로젝트 경로)`이고 `UserSettings/`는 클론에 복사되지 않는다(원본 21017 / 클론 25821).

## 7. 검증 범위 / 정직 계약

- ✅ **증명(단일 에디터 host, MCP + 사람 GUI):** 룸 조립(길1)·RoomCore·Ruler(§5)·5층 구조·SceneId 재부모 보존·World Space UI **데스크톱 마우스**(사람) + **XR 컨트롤러 sim**(사람: UI 버튼 클릭 + 바닥 측정).
- ✅ **코드 커버(구조):** 손도 동일 인터랙터 → 실기기에서 컨트롤러와 동일 작동(코드 0 추가).
- ⬜ **개척 청구서(V2/미경험):**
  - **실기기 손 트래킹**(핀치/poke), XREAL, **태블릿/Vision**(전용 아바타 프리팹 없음 — Desktop/UnityXR/XrealXR 3종만), 시선.
  - **실제 마우스/포인터 이벤트→레이캐스트** 원경로(현재 주입은 OnClick/SubmitExternalRay 경계).
  - poke로 바닥 측정(현재 near-far 레이만).
  - 번들 한글 폰트(현재 OS 동적 폰트 = 데스크톱만).
  - **배포(Smart Deploy / Build & Package / Bundle Uploader) 전체 = 미경험 → `build-studio-deploy.md` 후속.**
  - 2인(QuickTest 에디터 2개) = ✅ **성립(2026-08-19, §6.6)** — ParrelSync 클론 + 역할 브리지로 **빈 룸 2인 스폰 4신호 + Chat 양방향 4신호 PASS**(migration §17). **남은 파리티:** Grab 핸드오버 · 과녁/점수 동기 = 해당 FEATURE가 현존 룸에 미배치라 **미실행**(인프라 아님). **실 키보드 입력 2인 조작 · 3인+ 는 여전히 미증명.**
