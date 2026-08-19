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

### 5.1 디자인 토큰과 대비 게이트 (glass v6.4 — 2026-08-12~13 실측)

값 자체의 SSOT는 **`HudTheme.cs` 헤더 주석**이다(산술과 근거가 거기 있다). 여기엔 그걸 다루는 **절차와 함정**만 적는다.

- **토큰은 성역이다.** 변경 절차: 사람이 `.claude/settings.local.json` 최상위 `env`에 `PROMPTSCENE_ALLOW_THEME_EDIT=1` → **세션 재시작**(훅은 시작 시 환경을 물려받으므로 에이전트가 켤 수 없다) → **플러그인 assets 쪽** `HudTheme.cs` 수정 → **Phase 1b**로 studio 사본 덮어쓰기 → 플래그 제거. ⚠ 파일에서 지워도 **그 세션 프로세스 env는 살아 있다**(다음 세션부터 닫힘).
- **⛔ 대비 산술은 반드시 선형 공간에서 섞는다.** 프로젝트가 **Linear**라 GPU가 선형에서 블렌딩하는데, 게이트가 감마 값을 그대로 섞으면 **반투명 스택의 숫자가 전부 틀린다.** 오차 방향이 균일하지 않아 더 위험하다 — 어두운 잉크엔 비관적(없는 FAIL을 만든다), 밝은 잉크엔 낙관적(진짜 FAIL을 놓친다). 실제로 이 오차가 "Film을 .28→.60으로 올린다", "글리프에 헤일로를 단다" 두 결정을 만들었다. **검산법:** U8 캡처 픽셀을 샘플해 게이트의 `Composite()` 예측과 대조한다(교정 후 예측 `#959595` vs 실측 `#949494` = 1/255 일치). 캡처는 sRGB RenderTexture라 **화면과 같은 값**이다.
- **보장은 판이 아니라 잉크가 든다.** 유리 방향(Film을 비운다)에서는 판으로 대비를 만들 수 없다 → 잉크는 **`GlyphInk` + `GlyphHalo` 짝**으로 다니고 둘은 **항상 반대 극**이어야 한다(같은 극이면 자기대비 1.00:1로 보강이 0 — 실제로 밟았다). 잉크를 코드에 색으로 박지 말고 **역할 토큰**을 참조할 것: 박아 두면 디자인이 바뀔 때 게이트만 옛 값을 단정하는 드리프트가 난다(이 세션에 2건 발생).
- **헤일로 두께는 절대 px이 아니라 상대 두께로 판단한다.** 뭉개짐은 `두께/글자크기`가 정한다 — 1/8은 눈이 버렸고(16px 한글 + 2px), 1/24(48px 글리프 + 2px)와 1/16(16px 라벨 + 1px)은 통과했다. 그래서 U7의 아웃라인 절은 **역할별 하한**(`OutlineW` / `LabelHaloW`)을 쓴다. 완화가 아니라 분리다(`Roles.SizeExempt`가 크기에서 하는 것과 같은 형태).
- **라벨은 판으로 구제되지 않는다.** 불투명 잉크 대 환경이라 밝은 방에서 1.65:1이고, Scrim 알약을 되살려도 선형 산술로 2.33:1이라 미달이다. 선택지는 **잉크에 헤일로를 주거나 / 라벨을 지우거나** 둘뿐.
- **흰 링은 흰 배경에서 원리적으로 소멸한다.** Film을 비우면 윤곽을 링이 드는데, 그때 흰 링은 밝은 환경에서 사라진다(실측: 원판·링·배경 전부 `#FFFFFF`). → 링 잉크는 **양쪽 끝에서 살아남는 중간 톤**. 어두운 환경은 원판이, 밝은 환경은 링이 윤곽을 든다.
- **아이콘 폰트(Material Symbols):** 선/채움은 색이 아니라 **`FILL` 축**이다. 굽는 조건은 **wght 400 · GRAD 0 · `opsz 48` · FILL 0|1** — ⚠ `opsz`를 모르고 기본값(24)으로 구우면 "채우기만" 하려던 변경에 **획 굵기 회귀**가 조용히 딸려온다. 검산: upem(960)·advance(1.000em)·bbox가 이전 폰트와 **완전 일치**해야 한다. 스크립트 = `skills/cross-platform-ui/assets/bake_icon_font.py`. 같은 파일명으로 덮으면 `.meta`/GUID가 유지돼 코드·씬 변경이 0이고, U11이 코드포인트 전수를 재단정한다.

### 5.2 ⛔ 검증기 자체의 조용한 실패 4종 (전부 2026-08-12~13 실측·수정)

판정에 안 들어가는 부분(증거·복원)은 **실패해도 아무도 알려주지 않는다.** 이 스킬에서 실제로 터진 것들:

| 증상 | 원인 | 수정 |
|---|---|---|
| U8 캡처가 **백지 PNG** | `HudSummon`이 HUD를 **캔버스 OFF로 시작**(F1 소환)하는데 `Capture()`가 소환하지 않음 | 촬영 동안만 Canvas 켜고 원복 |
| 캡처 속 원 크기가 **실행마다 달라짐**(240px → 90px) | 프레이밍을 `px/PxPerMeter`로 유도 = HUD가 설계 스케일에 있다고 가정. `HudPlacement`가 런타임에 스케일을 바꾼다 | 패널의 **월드 크기**(`GetWorldCorners`)로 프레이밍 |
| 액센트 positive case가 VIOLATION | 액센트가 원판→**테두리**로 옮겨졌는데 게이트만 `__disc`를 계속 기대 | 기대를 `__ring`으로. `SequenceEqual`이라 "원판은 안 물들었다"까지 함께 단정 |
| **Teardown이 복원을 안 하고도 조용함** | 복원 스냅샷을 `<project>/Temp`에만 둠 → Unity가 리로드·재임포트 때 그 폴더를 비운다(폰트 재임포트 실행에서 실제로 사라졌고, 같은 절차의 앞선 두 실행은 멀쩡 = **불규칙 재현**) | 정본을 **EditorPrefs**로(파일은 사람이 읽을 사본), 스냅샷 없는 Teardown은 **현재 값을 찍는 LogWarning**으로 승격 |

**교훈(§7 정직 계약의 연장):** 마지막 항목은 실패 시 `roomSceneKey`가 **테스트 값 그대로** 남아 다음 사람이 Play를 눌렀을 때 엉뚱한 룸이 뜬다(메모리 `quicktest-handoff-roomkey-reverts`가 경고한 오인). 그래서 고친 뒤 **차단 재현으로 검증했다** — Temp 사본을 일부러 지우고 Teardown → 복원 성공. *통과 테스트가 아니라 실패 재현이 검증이다.*

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
5. **판정** — 프로브 한 줄에 전부 들어 있다:
   `probe[tick] srv=… cli=… cid=… scenes=[…] nobs=N | id=<objId> <name> owner=<IsOwner> ownerCid=<n> pos=… | chatLog=…`
   **현재 상태는 클론 루트의 `.promptscene-status` 를 읽는다**(1초마다 덮어쓰기, 항상 한 줄) — 커지는 로그를 훑을 필요가 없다. 상태 1회 확인 = 파일 1개 읽기.
   ⚠ **로그를 상태 조회에 쓰지 말 것.** `-logFile` 은 append-only라 세션이 길어질수록 읽는 비용이 비례해 커진다(초기 구현은 1초마다 스택트레이스까지 찍어 **13 MB / 175k줄**까지 갔다). 지금 로그는 ① Log 레벨 스택트레이스 off ② **상태가 바뀔 때만** 기록(+`cmd probe` 는 항상) 이라 사건 기록에 가깝다 — 시계열·사후 추적용으로 쓴다.
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
