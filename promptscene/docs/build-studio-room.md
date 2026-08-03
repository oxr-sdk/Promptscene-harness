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


### 4.1 부트 흐름 트러블슈팅 — `No cameras rendering` (2026-07-30/31 실측)

**전제: `QuickStart`도 룸 씬도 카메라를 갖고 있지 않다.** 커밋 버전 grep 결과 `Camera:` 0개, `MainCamera` 태그 0개.
카메라는 **스폰되는 아바타 프리팹**에서 온다. 따라서 아바타가 없으면 Game 뷰는 계속 `No cameras rendering`이고,
이건 UI·렌더 문제가 아니라 **부트 흐름 문제**다. 정상 상태에서도 다음 두 구간에는 반드시 뜬다:

- **에디트 모드 전체** (재생 전) — 정상. 고칠 것 없음.
- **▶ 직후 수십 초** (서버 시작 → Addressables 룸 로드 → 스폰) — 정상. 기다리면 뜬다.
  실측 범위: 방해 없는 부트는 **t≈27초**에 `룸 loaded + cams=1 + 아바타`였고, 부트 중에 MCP 프로브를 두 번 넣은
  부트는 **t=14초에 아직 `룸 loaded=False`**, t≈56초에 완료였다. 즉 **"10~17초"는 상한이 아니다.**

⚠️ **부트 시간을 늘리는 두 가지 (둘 다 Unity 메인 스레드를 잡는다):**
- MCP 플러그인이 **매 부트마다** `[Unity-MCP DependencyResolver] Restoring NuGet packages… → Refreshing AssetDatabase`를
  돌린다(부트 시작 ~1.3초 후). 진행 중인 Addressables 로드를 그만큼 멈춘다.
- **에이전트의 `script-execute` 자체.** Roslyn 컴파일이 메인 스레드에서 돈다 → **부트 중에 상태를 찍는 행위가 그 부트를
  느리게 만든다**(관측자 효과). → **부트가 끝날 때까지 프로브하지 말고, 한 번만 찍는다.**

✅ **"안 되는 것"과 "아직 로딩 중"을 한눈에 가르는 기준:**

| Hierarchy | 판정 |
|---|---|
| 룸 씬이 아직 없거나 `loaded=False` | **로딩 중.** 기다린다 (섣불리 ▶를 다시 누르면 처음부터) |
| 룸은 올라왔는데 카메라·아바타가 없다 | **고장.** 아래 표의 ①~④ — 특히 ④(`client is starting`이 2회) |

그보다 오래 지속되면 아래 넷 중 하나다. 전부 실측으로 확인했고, 1~3은 세 verify 드라이버가 Setup 진입 전에 단정한다.

| # | 원인 | 증상 구분법 | 가드 |
|---|---|---|---|
| 1 | `roomSceneKey`가 **없는 룸**을 가리킴 | 인스펙터 `Room Scene` 칸이 `None` | `RoomResolvable()` → 정지 |
| 2 | **룸 씬이 에디터에 additive 로 열린 채 ▶** | Hierarchy에 `QuickStart` 옆에 룸이 같이 보인다. 룸·RoomCore·HUD는 올라오는데 **스폰만 빠진다** | `OnlyBootSceneOpen()` → 정지 |
| 3 | 키가 **카탈로그 주소도, 어떤 씬의 파일명도 아님** (오타·삭제된 룸) | 룸이 Hierarchy에 **아예 안 올라온다**. 로그에 `Failed to load scene key` 또는 로드 요청 후 무반응 | `RoomKeyLikeHuman()` + 왕복 검사 → 정지 |
| 4 | **부트 중 Game 뷰 좌상단 클릭** = FishNet 데모 HUD의 `Start Client` 버튼을 누른 것 | 로그에 `Local client is starting`이 **`[QuickTest] Host: 클라이언트 접속...` 없이** 먼저 한 번 뜬다. 이어서 `Remote connection stopped for Id 0` → `started for Id 1` | 가드 없음(사람의 클릭) → **클릭 금지 구역**을 안내한다 |

**4번 상세 (2026-08-03 스택트레이스로 확정).** `QuickStart`의 `NetworkManager/NetworkHudCanvas`는 FishNet 데모
스크립트 `NetworkHudCanvases`이고, 이것이 **`OnGUI`로 Game 뷰 좌상단에 `Start Server` / `Start Client` 버튼을 그린다**
(`GUILayout.BeginArea(new Rect(4, 110, 256, 9000))`, 버튼 165×42, `GUI.matrix`가 1920×1080 기준으로 스케일 →
1280×720 뷰에서는 대략 **x 0~115, y 70~140 px**). 그 영역을 클릭하면 `OnClick_Client()`가 호출되고, 여기가 **토글**이라
연결 상태에 따라 Start/Stop이 갈린다.

부트 중에 눌리면 이렇게 무너진다:

| 시각 | 성공 부트 (09:35) | 실패 부트 (09:55) |
|---|---|---|
| +2.6s | — | `Local client is starting` ← `NetworkHudCanvases:OnGUI → OnClick_Client` (사람 클릭) |
| +2.9s | — | `[NetworkEnabler] enabling embedded rig under 'OnlyClient'` (Id 0 기준) |
| +4.4s | `[QuickTest] Host: 클라이언트 접속...` (유일한 클라 시작) | `[QuickTest] Host: 클라이언트 접속...` → `QuickTestStarter.cs:122`가 **이미 붙은 연결을 끊고 재접속** |
| +4.4s | `[NetworkEnabler] enabling embedded rig` ✅ | `Remote connection stopped Id 0` → `started Id 1`, `NetworkEnabler` **재발화 없음** → `cams=0` ❌ |

`QuickTestStarter`는 호스트 모드에서 `WaitForSeconds(2f)` 뒤 `ClientManager.StartConnection()`을 한 번 부른다
(`QuickTestStarter.cs:118~123`). 즉 **클라 시작은 그 한 번뿐이어야 한다.** 사람이 먼저 눌러 만든 연결은
그 호출에 의해 재시작되고, 리그를 켜준 `NetworkEnabler`는 새 연결에 다시 붙지 않는다.

→ **사람 안내 규칙: 부트가 끝날 때까지(~17초) Game 뷰를 클릭하지 않는다. 클릭이 필요하면 좌상단을 피해
화면 중앙~우하단을 클릭한다.** (`Stop Client`도 같은 버튼이라, 부트 후에 눌러도 세션이 끊긴다.)

**④를 확정한 대조 실험 (2026-08-03, `T_RoomA` / 키 `Scenes/T_RoomA` 고정).** 키·룸·씬 구성을 **하나도 바꾸지 않고**
클릭만 뺐다:

| 부트 | 클라 시작 호출자 | 결과 |
|---|---|---|
| 10:02 / 10:03 (사람이 부트 중 클릭) | `NetworkHudCanvases:OnGUI` **+** `QuickTestStarter.cs:122` = **2회** | `cams=0`, `avatar=MISSING` ❌ |
| 10:08 (클릭 없이 `EnterPlaymode`) | `QuickTestStarter.cs:122` **1회뿐** | `cams=1`, `avatar=(0,0,0)` ✅ |

→ 판별 한 줄: **로그에서 `Local client is starting`이 몇 번 뜨는지 센다. 2번이면 ④다.**
"드래그앤드롭이 원래 됐는데 안 된다"의 정답이 대부분 여기다 — 키는 멀쩡하다.

**2번 A/B 실측** (`roomSceneKey`는 양쪽 동일하게 정상값):

| ▶ 직전 상태 | t+14~17s |
|---|---|
| 룸이 additive 로 함께 열림 | `cams=0, avatar=False` ❌ (룸 로드 ✅, RoomCore ✅, HUD 버튼 생성 ✅ — **스폰만** 실패) |
| `QuickStart` **단독** | `cams=1, avatar=True` ✅ |

⚠️ **에이전트 의무:** 룸 씬을 열어 편집했으면 넘기기 전에 **반드시 `scene-open QuickStart Single`로 되돌린다.**
사람은 Project 창의 씬 에셋을 인스펙터 칸에 **끌어다 놓기만** 하므로 룸을 여는 일이 없다 — 즉 2번은
**에이전트 워크플로가 만드는 상태**다. 룸을 열어놓고 넘기면 사람이 ▶를 눌러 밟는다(2026-07-31 실제 발생).

### 4.2 `roomSceneKey`를 스크립트로 쓸 때의 함정 (실측)

- **`Room Scene`은 별도 필드가 아니다.** 직렬 필드는 `roomSceneKey`(문자열) 하나뿐이고, 인스펙터 위쪽
  `Scene (드래그&드롭)` 칸은 `QuickTestStarterEditor`가 그 문자열을 SceneAsset으로 **역해석해 보여주는 뷰**다.
  키만 올바르면 두 줄이 같이 맞고, 해석 실패면 그 칸이 `None`이 된다.
- **키의 정답 형태는 Addressables 주소가 아니라 Unity 씬 이름(leaf)이다 (2026-08-03 정정).**
  두 형태 모두 룸을 **로드**하지만, FishNet의 글로벌 씬 등록은 **씬 이름으로만** 맞는다:

  | 키 | Addressables 로드 | `UnitySceneManager.GetSceneByName(key)` | FishNet `Connection.Scenes` | 아바타 |
  |---|---|---|---|---|
  | `T_RoomA` (leaf = 씬 이름) | ✅ 파일명 폴백으로 `Scenes/T_RoomA` 매핑 (로그 남음) | **True** | 룸 등록 ✅ | 스폰 ✅ |
  | `Scenes/T_RoomA` (등록 주소) | ✅ 주소가 그대로 맞아 즉시 로드(로그 없음) | **False** | **`[]` 빈 채로 남음** + `The following global scenes were specified but could not be found: Scenes/T_RoomA` 경고 | 스폰 ✅ (실측) |

  근거: `AddressablesSceneProcessor.ResolveAddressableSceneKey()`는 키가 카탈로그에 있으면 그대로 쓰고, 없으면
  **확장자 없는 파일명**이 같은 카탈로그 키를 찾아준다 → leaf가 항상 통한다. 반면 FishNet
  `SceneManager.OnClientAuthenticated()`는 `GetSceneByName(globalSceneName)`으로 찾으므로 **주소 형태는 실패**하고,
  `sceneLookupData.Count == 0` → `SendEmptyBroadcast()`로 빠진다.
- ⚠️ **그래서 `ResolveAddress()`를 그대로 키에 쓰면 안 된다** — 이 메서드는 **등록 주소**를 돌려준다(실측:
  `T_RoomA`→`Scenes/T_RoomA`, `T_RoomB`→`Scenes/T_RoomB`, `AssembleRoom`→`Assets/App/Scenes/AssembleRoom.unity`).
  사람의 드래그앤드롭도 이 값을 쓰므로, **사람이 넣어도 주소 형태가 들어간다.** 아바타는 그래도 뜨기 때문에
  겉으로는 정상처럼 보이지만 `Connection.Scenes`가 비어 있다(측정됨). 네트워크 콘텐츠가 있는 룸에서 이게
  복제에 어떤 영향을 주는지는 **아직 측정하지 않았다** — 확인 전까지는 **씬 이름(leaf)을 쓴다.**
  인스펙터 표시는 두 형태 모두 정상이다(`ResolveSceneAsset('T_RoomA')`와 `('Scenes/T_RoomA')` 둘 다 T_RoomA를 돌려준다).
- ✅ **정답은 손으로 키를 고치는 게 아니라 등록을 규약대로 되돌리는 것이다** — 그러면 드래그앤드롭이 알아서 맞는 값을 넣는다.
  규약의 출처는 업스트림이다: `Docs/phase2-scene-authoring.md` L37 "이 **파일 이름(leaf)이 그대로 Addressables 주소가 되고**",
  L77 "**파일 이름 그대로가 권장값** … `RoomScene` 라벨은 자동으로 붙습니다" → `Apply`.
  Content Manager의 `ScanScenes()`도 미등록 씬에 `addr = name`(leaf)을 제안한다.
- **2026-08-03에 실제로 어긋나 있었고 고쳤다.** `AssembleRoom`이 주소=`Assets/App/Scenes/AssembleRoom.unity`(Addressables의
  **기본값 = 에셋 경로**) + **라벨 없음**으로 등록돼 있었다 — `CreateOrMoveEntry`만 돌고 address/label 지정이 안 된 형태다.
  `/assemble-room` Phase 1이 **쓰고 나서 다시 읽지 않았기 때문에** 몇 주간 드러나지 않았다.
  조치: (a) 엔트리를 `AssembleRoom` + `[RoomScene]`으로 복구, (b) Phase 1에 `SetLabel(..., force:true)` +
  **읽기-되돌려-단정(read-back) 게이트**를 추가해 어긋나면 STOP하게 했다.
  복구 후 실측: `ResolveAddress(AssembleRoom)` = `'AssembleRoom'`(= 씬 이름), 부트 시
  `_globalScenes=[AssembleRoom]` · `GetSceneByName('AssembleRoom').IsValid=True` · `cams=1` · 아바타 스폰 ✅.
- ⚠️ **GUI `Apply`는 이미 등록된 엔트리를 고쳐주지 못한다.** `ContentManagerWindow`는 기존 엔트리에 대해 `address`만
  갱신하고 `SetLabel`은 **신규 추가 분기에만** 있다. 복구는 체크 해제 → `Apply`(엔트리 제거) → 다시 체크 → `Apply`,
  또는 Phase 1 스크립트로 한다.
- ℹ️ `T_RoomA`/`T_RoomB`는 여전히 `Scenes/T_Room*`으로 등록돼 있다(라벨은 있음). 규약의 leaf 형태가 아니라 같은 증상을
  낸다 — 업스트림 템플릿 소유라 여기서 고치지 않았다. **oxr-sdk에 보고할 항목.**
- **스크립트 쓰기는 씬을 dirty로 만들지 않는다.** 실측: `SerializedObject.ApplyModifiedProperties()` → `isDirty=False`,
  `EditorUtility.SetDirty()` / `EditorSceneManager.MarkSceneDirty()` → `isDirty=True`.
  즉 스크립트로 넣은 값은 Unity의 저장 추적 **밖**에 있어서, 씬을 다시 열면 **아무 경고 없이** 디스크 값으로 덮인다
  (사람이 인스펙터에서 만지면 `*`가 뜨고 저장 프롬프트가 나온다 — 그쪽은 정상 유지된다).
  → 한 Setup→Play→Check→Teardown 사이클 안에서는 안전하지만, 도중에 씬을 열고 닫으면 값이 사라진다.
- **스냅샷 복원의 수명:** Setup은 원본을 `Temp/ps_*_orig.txt`에 저장한다. Teardown 없이 Setup을 다시 돌리면
  스냅샷이 **자기가 방금 쓴 값으로 덮여** 사람이 넣어둔 원본이 사라진다(2026-07-30 실제 사고).
  → Setup은 스냅샷이 있으면 덮어쓰지 않고 WARN, Teardown은 복원 후 스냅샷을 삭제한다.

### 4.3 아바타가 안 움직인다 = 대개 Game 뷰 포커스다 (2026-08-03 실측 해소)

`Desktop(Clone)` **스폰**은 §4가 판정하지만 **이동**은 판정하지 않는다. 그런데 "아바타가 안 움직인다"로 두 세션을
태웠으므로, 원인과 판별 절차를 여기 남긴다. **결론: 결함이 아니라 포커스였다.**

- `DummyController`는 레거시 `Input.GetAxis("Horizontal"/"Vertical")`로 읽고 `localPosition`에 직접 가산한다.
  `moveSpeed = 1f` → **1 m/s** (체감이 느리다). `activeInputHandler=2`(Both)라 레거시 입력은 유효하다.
- 레거시 입력은 **Game 뷰가 키보드 포커스를 쥐어야** 들어온다. 창이 보이는 것만으로는 안 되고,
  **Game 뷰 안을 한 번 클릭**해야 한다. MCP로 에디터를 구동하면 `Application.isFocused=False`가 되기 쉽고,
  특히 `Selection.activeGameObject=…`를 실행하면 Inspector가 앞으로 나와 키를 가로챈다.
  → **에이전트는 넘기기 전에 `Selection.activeObject=null`로 선택을 해제한다.**
- 사람 절차: 부트가 끝난 뒤 **Game 뷰 중앙~우하단을 클릭** → WASD를 **길게**. ⛔ **좌상단은 금지** — FishNet 데모 HUD의
  `Start/Stop Client` 버튼이 그려져 있어 세션이 끊긴다(§4.1 ④). HUD 패널을 클릭하면 콘텐츠가 토글되므로 그것도 피한다.
  1 m/s이므로 3초 ≈ 1.4 m. 짧게 톡 누르면 `GetAxis` 램프까지 겹쳐 2~3 cm만 가고, 바닥이 텅 빈 평면이라 안 보인다.

**판별 절차(실측 A/B).** 같은 씬·같은 빌드에서 Game 뷰 클릭 전/후만 다르다:

| | 클릭 전 (W 3초) | 클릭 후 (W 3초) |
|---|---|---|
| `Input.GetAxis("Vertical")` 최대 | **0** (13,226 프레임 내내) | **1** |
| `Input.GetKey(KeyCode.W)` 관측 | **False** | **True** (A/S/D도) |
| `Dummy.localPosition` 이동 | 0 m | **1.4 m** (카메라 동반) |

- **먼저 배제할 것 — 코드 경로는 무죄임을 1회로 증명한다.** `Dummy.transform.localPosition += (0,0,-1)`을 직접 써보고
  1초 뒤 다시 읽는다. 값이 유지되고 카메라가 따라오면 이동 경로·소유권·`NetworkTransform`(아바타에 68개 붙어 있다)
  전부 무죄다. 실측: 유지됨 + 카메라 `z=-1.025`.
- **⚠ `Input.anyKey`는 마우스 버튼도 True로 만든다.** "anyKey=True인데 W=False"를 "키보드는 오는데 W만 막힌다"로
  읽으면 틀린다 — 그 True는 Game 뷰 클릭이었다(2026-08-03 실제 오독). 키보드 도달 여부는 **키코드별로** 찍어야 갈린다.
- 축 설정과 레거시 활성은 정적으로 먼저 확인한다: `ProjectSettings/InputManager.asset`의 `Vertical`은
  `altPositiveButton: w`, `type: 0`. 그리고 Active Input Handling이 New 전용이면 `Input.GetAxis`가
  **`InvalidOperationException`을 던진다** — 예외 없이 0을 반환하면 레거시는 살아 있다는 뜻이므로 그 방향은 접는다.
- 한글 IME는 무죄였다(W/A/S/D 모두 도달). 의심되면 `Vertical`에 `up` 방향키도 걸려 있으니 방향키로 갈라본다.
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

## 7. 검증 범위 / 정직 계약

- ✅ **증명(단일 에디터 host, MCP + 사람 GUI):** 룸 조립(길1)·RoomCore·Ruler(§5)·5층 구조·SceneId 재부모 보존·World Space UI **데스크톱 마우스**(사람) + **XR 컨트롤러 sim**(사람: UI 버튼 클릭 + 바닥 측정).
- ✅ **코드 커버(구조):** 손도 동일 인터랙터 → 실기기에서 컨트롤러와 동일 작동(코드 0 추가).
- ⬜ **개척 청구서(V2/미경험):**
  - **실기기 손 트래킹**(핀치/poke), XREAL, **태블릿/Vision**(전용 아바타 프리팹 없음 — Desktop/UnityXR/XrealXR 3종만), 시선.
  - **실제 마우스/포인터 이벤트→레이캐스트** 원경로(현재 주입은 OnClick/SubmitExternalRay 경계).
  - poke로 바닥 측정(현재 near-far 레이만).
  - 번들 한글 폰트(현재 OS 동적 폰트 = 데스크톱만).
  - **배포(Smart Deploy / Build & Package / Bundle Uploader) 전체 = 미경험 → `build-studio-deploy.md` 후속.**
  - 2인(QuickTest 에디터 2개) / 2클라 파리티 = studio 미경험(다음 단계). **토폴로지 정찰(xumflow-migration §10.3):** QuickTest = MST 아닌 **FishNet 직접연결 `localhost:7770`**(서버=startAsServer✅, 클라=startAsServer❌). 2클라 = host 에디터 A + 별도 프로세스 B 1개면 성립하나 **B 생성 수단 부재**(ParrelSync·MPPM 미설치, 경량 스탠드얼론 빌드 없음 → Smart-Deploy 미경험). 착수 전 MPPM 추가 vs 클론 vs 빌드 결정 필요. **일괄 대기 큐:** Chat 양방향 · Grab 핸드오버 · **과녁/점수 동기 파리티(§6.5 COMPOSITION — 별도 클라 B가 같은 서버권위 스코어보드 수신)**.
