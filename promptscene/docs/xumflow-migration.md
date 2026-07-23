# XumFlow 이식 — 메커니즘 · 대조표 · 미결 타깃 결정

> **최신 상태(2026-07-23):** ✅ **첫 관통 성공 — RoomCore + Ruler를 studio 샘플룸(T_RoomB 복제) 위에 얹어 §5/§6.5 MCP 라이브 판정 PASS. → §9.** (아래 §1~§5는 studio 확보 전 소스분석 기록, §7~§8은 환경/MCP/port-prep 확정. §9가 최신.)
>
> **상태(2026-07-22):** §2 메커니즘 + §3 대조표 = **소스 확정 완료(read-only)**.
> §4(이식)·§5(라이브 검증) = 당시 **하드 스톱**(studio 부재 + MCP 미가동) → §7 이후 해소, §9에서 관통 완료.
> 이 문서는 원 브리프의 "XumFlow로 PromptScene 이식" 가정을 **확정하지 않고**, §2 경계에서 도출한
> 포트 타깃 결정(§4)을 열어 둔 채 근거를 정리한다.
>
> **클론:** `c:\J_0\XumFlow` (gitignore `/XumFlow/`), 브랜치 `runtime` @ `200a4a2`, Unity `6000.3.11f1`(XRCollabDemo와 동일).
> `git ls-remote`/`clone` 성공(자격증명 provisioned). → HANDOFF §9 "oxr-sdk clone 금지" 문구와 충돌 → §6 역기입 제안.

---

## 0. 한 줄 요약 — XumFlow는 "새 XRCollabDemo"가 아니다

XumFlow 자체 `CLAUDE.md`가 이 프로젝트를 **`runtime_t3`**로 규정한다: **콘텐츠를 직접 저작하지 않고**,
별도 프로젝트 **`studio_v6`**가 빌드한 **Addressables 번들 + HotUpdate DLL을 원격(HTTP/nginx)에서
다운로드해 실행하는 client.exe + 룸서버 빌드 프로젝트**(`XumFlow/CLAUDE.md` §1–2). 스택 = HybridCLR +
Addressables + FishNet 4.6.17 + MasterServerToolkit(+ XumNet / UnifiedXRMotion / XumBuildkit / XumLobby /
XumRuntimeKit; `Packages/manifest.json`). **`XumStudioKit`는 없음**(그건 studio 측).

```
studio_v6  ──build──▶ ServerData/<Platform>/ (Addressables + HotUpdate DLL) ──nginx──▶  runtime_t3 (=XumFlow)
(룸 씬·프리팹·게임로직 저작)                                                              (다운로드 후 실행)
```

---

## 1. 메커니즘 (§2 재프레임 — "왜 클라 룸만 빌드하면 되나"의 실제 답)

> 원 브리프의 프레이밍("예전엔 룸 바뀌면 Master+Room까지 빌드, 이제는 클라 룸만 빌드")은 **부정확**하다.
> 실제 모델은 더 크다: **룸은 어떤 실행파일에도 빌드되지 않는다 — 룸은 원격 Addressables 콘텐츠로
> 다운로드된다.** 즉 콘텐츠 변경 시 runtime(클라/서버) 재빌드는 **아예 불요**이고, 변경은 studio_v6의
> Addressables 배포로 처리된다(`XumFlow/CLAUDE.md` §7 빌드 트리거 표: "룸 씬·프리팹 추가/수정 → studio
> Addressables 빌드만, runtime 재빌드 ❌").

### (1) 콘텐츠 hot-load 경로/포맷 (소스 확정)

| 콘텐츠 | 경로 | 포맷/주소 | 근거(파일:라인) |
|---|---|---|---|
| 게임로직 DLL | `HotUpdateBootstrap` → `Addressables.LoadAssetAsync<TextAsset>(addr)` → `Assembly.Load(bytes)` | `HotUpdate/Dlls/<asm>.dll.bytes` | `Runtime/Bootstrap/HotUpdateBootstrap.cs:173-216`, `BuildDllAddress` `:361-368` |
| AOT 메타데이터 | `RuntimeApi.LoadMetadataForAOTAssembly(bytes, SuperSet)` | `HotUpdate/AOT/<asm>.dll.bytes` | `HotUpdateBootstrap.cs:312-352` (특히 `:336`) |
| 코드 매니페스트(버전 게이트) | 원격 우선, baked 폴백 | `HotUpdate/CodeManifest` | `HotUpdateBootstrap.cs:46-62, 263-310` |
| **네트워크 스폰 프리팹 컬렉션** | `Addressables.LoadAssetAsync<PrefabObjects>(addr)` → **`networkManager.SpawnablePrefabs = loaded`** | `Network/DefaultPrefabObjects` | `Runtime/Networking/AddressablesSpawnablePrefabsBootstrap.cs:19, 211, 234` (폴백 `:257-277`) |
| 룸 씬 | FishNet `SceneLoadData` + **`Options.Addressables = true`** (씬 키로 로드, 빌드 씬리스트 아님) | 씬 키 `Scenes/<Room>` / 기본 `T_RoomA`, 라벨 `RoomScene` | `Runtime/Networking/ServerSceneFlow.cs:125-156` (특히 `:149`), `RoomSceneKeyOverride.cs:24,32` |
| 카탈로그 URL | 백엔드 API resolve(서버=`XumApiServerCatalogProvider`, 클라=`XumApiSceneProvider` 로그인 후) / 폴백 `-room.remote_url` | `http://host/ServerData/<Platform>/catalog_0.1.0.bin` | `Runtime/Networking/RemoteUrlRouter.cs:16-22, 27, 53-86, 129-133` |

- 부팅 순서: `RemoteUrlRouter`(BeforeSceneLoad) → `HotUpdateBootstrap`(-21000) → `AddressablesSpawnablePrefabsBootstrap`(-20000) → `ServerSceneFlow`/`ClientSceneGate`. 뒤 두 컴포넌트는 `RemoteUrlRouter.IsApplied`까지 대기(`HotUpdateBootstrap.cs:128-150`, `AddressablesSpawnablePrefabsBootstrap.cs:130-147`).
- 카탈로그 URL이 서버(예 Windows)와 클라(예 Android) 플랫폼이 다르면 `SwapPlatformSegment`로 `/StandaloneWindows64/`→`/Android/` 치환(`RemoteUrlRouter.cs:88-127`).

### (2) runtime ↔ studio_v6 경계 — **정확히 어디인가**

경계는 **Addressables 원격 카탈로그**다. 리터럴 마커는 `Assets/App/Scripts/HotUpdate/App.HotUpdate.asmdef`:
```
"includePlatforms": ["Editor"],  "defineConstraints": ["XUM_LOCAL_HOTUPDATE"]
```
→ hot-update 코드는 **runtime 프로젝트엔 "에디터 전용 스텁"으로만** 존재(플레이어 빌드에서 제외).
플레이어 빌드는 이 DLL을 **studio_v6가 배포한 원격 카탈로그에서 다운로드**한다. runtime 안의
`HotUpdate/*.cs`(`HotUpdateHello`, `PlayerListButton`, `LeaveRoomButton`)는 **스텁**이며 실제 것은 원격.

| runtime_t3(XumFlow)가 소유(baked/build) | studio_v6가 소유(원격 Addressables) |
|---|---|
| baked Assembly-CSharp: `Scripts/Runtime/{Bootstrap,Networking,Versioning}` | 룸 씬(`Scenes/<Room>`, 라벨 `RoomScene`) |
| `App.Bridges`(baked 공유 타입: `PlatformGroup`/`PlatformPlayerEntry`/`RoomEventBridge`) | 네트워크 스폰 프리팹 컬렉션(`Network/DefaultPrefabObjects`) |
| 부트 씬 `T_Client`(로비)/`T_Master`/`T_RoomBootstrap` | 실제 `App.HotUpdate.dll`(게임 UI/로직) + `HotUpdate/AOT/*` |
| 에디터 전용 스텁 `App.HotUpdate`(`includePlatforms:["Editor"]`) | `HotUpdate/CodeManifest`(codeVersion·어셈블리·AOT 목록) |

**바이트 단위 동기화 계약**(양쪽 동일 사본 필수, `XumFlow/CLAUDE.md` §6): `Bridges/` 폴더 전체 +
`Scripts/Runtime/Versioning/HotUpdateCodeManifest.cs`. 한쪽만 고치면 직렬화 미스매치.

### (3) 룸/기능은 경계의 **어느 쪽에 사는가**

**studio_v6 쪽.** 근거:
- 룸 씬은 원격 Addressables 씬으로 로드(`ServerSceneFlow.cs:149` `Options.Addressables=true`).
- 생성 가능한 룸 목록은 **원격 카탈로그의 `RoomScene` 라벨**을 조회해 create-room UI에 주입(`RoomSceneKeyOverride.cs:24,32,48-51,173` — `OXRCreateNewRoomView.availableRoomScenes`에 리플렉션 주입). 즉 "어떤 룸이 있는가"는 studio 배포가 결정.
- 플레이어 스포너(`XumPlayerSpawner`)는 runtime `Assets/App`에 **코드 참조 0**(주석 1건뿐, `Tools/QuickTestStarter.cs:21`) → 스포너는 다운로드된 룸 씬(studio 콘텐츠)에 배치.
- 게임 UI/로직은 hot-update DLL(studio) — runtime엔 스텁만.

→ **PromptScene의 FEATURE/COMPOSITION(룸 씬·프리팹·게임 로직)은 이 경계의 studio_v6 쪽에 대응된다.**

---

## 2. 대조표 (§3 — 얼려둔 규격의 생존/폐기/변경, hot-update 렌즈)

| 얼려둔 규격 | XumFlow(runtime_t3)에서 | 판정 | 근거 |
|---|---|---|---|
| **C1** 프리팹 컬렉션 일치(baked, 서버NM==클라NM; feature 프리팹 추가=baked 컬렉션 편집+Room.exe 재빌드) | NM.SpawnablePrefabs를 런타임에 원격 `Network/DefaultPrefabObjects`로 **교체**(baked=폴백). 프리팹 추가=studio Addressables 재빌드 | **변경(핵심)** — build-time baking invariant **폐기**; 프리팹 컬렉션은 studio 소유·원격 다운로드 | `AddressablesSpawnablePrefabsBootstrap.cs:19,211,234,257-277`; CLAUDE §6·§7 |
| **C2** 플레이어 스폰=XumPlayerSpawner | runtime엔 참조 0(주석뿐); 스포너는 다운로드된 룸 씬에 존재 | **변경** — 메커니즘(스포너가 스폰) 생존, **위치=studio 콘텐츠로 이동**. ⚠️소스 추가확인(실제 룸 씬 미보유) | `QuickTestStarter.cs:21` |
| **C3** `offlineScene=Client.unity` | FishNet `_offlineScene` 배선 아님 → **MST 이벤트(`showGamesListView`) 기반 로비 복귀** + 로비=`T_Client` + Addressables 씬플로우 | **변경** | `LobbyReturnRelay.cs:8,22`; `ClientSceneGate.cs:104` |
| **C4** 실행 토폴로지(Master.exe+Room.exe / 에디터=Client+Room) | **단일 client.exe**가 인자로 lobby/room-server 모드; 씬 `T_Master`/`T_RoomBootstrap`; 에디터 검증=`QuickTestStarter`(Addressables `Use Asset Database`) | **변경** | CLAUDE §1·§4B; `ServerSceneFlow.cs` |
| **build-xumlobby-server.md**: MasterAndSpawner.exe/Room.exe 빌드 절차 전체 | 단일 client.exe 두 모드 + **룸 씬/프리팹 변경 시 runtime 재빌드 불요**(studio Addressables 배포) | **대폭 변경 / 2바이너리 절차 폐기** | CLAUDE §1·§7 트리거표 |
| §7 함정 "씬오브젝트 재배치→SceneId→Room.exe 재빌드" | 룸 씬=원격 Addressables. runtime 재빌드 트리거 아님(단, FishNet 씬오브젝트 SceneId 규율 자체는 studio 씬 저작에서 여전히 성립할 개연) | **폐기(runtime 측)** / studio 측 재검증 대상 | CLAUDE §7 |
| §7 함정 "콜드 Room 빌드 10분+" | 룸은 빌드 아니라 다운로드. 대신 콜드 성격이 ①Addressables 카탈로그/번들 다운로드 대기 ②runtime **첫 빌드**의 HybridCLR generate(MethodBridge 28MB 등)로 이동 | **변경(대기의 성격 이동)** | `HotUpdateBootstrap` 대기 로직; CLAUDE §5·§10.5 |
| §7 함정 "서버 재빌드가 cfg를 LAN IP로" | `application.cfg`(마스터 IP)는 여전히 존재. 카탈로그 URL은 별도로 백엔드 API resolve/`-room.remote_url`로 주입 | **부분 생존/변경** | `RemoteUrlRouter.cs`; CLAUDE §10.5 |
| §7 트랩 J/K(멀티 게스트 동시조인 flakiness / 콜드스타트 액세스토큰 만료) | MST + XumLobby 스택 공유 → **잔존 개연**. 미검증(MCP·라이브 부재) | **미확정(재검증 대상)** | — |
| 스킬 4종의 "서버 재빌드/조인" 단계 | Master/Room.exe 재빌드 전제가 깨짐; 콘텐츠는 studio Addressables 배포 경로 | **변경(스킬 절차 재작성 필요)** | 상동 전반 |

**"변경/폐기"로 분류된 전 항목이 §5 재검증 대상**이며, 그 재검증은 studio_v6 확보 + MCP 가동이 선행 조건.

---

## 3. §4 미결 핵심 결정 — PromptScene 콘텐츠, 어디에 이식하는가 (지금 단정 금지)

**결정 질문:** PromptScene 저작물(Core 계약 + FEATURE/COMPOSITION 모듈 + 프리팹 + 룸 씬)을
**XumFlow(runtime_t3)** 에 이식하는가, **studio_v6** 에 이식하는가?

**§2 경계에서 도출한 가설(강함):** FEATURE/COMPOSITION = 룸 씬·프리팹·게임 로직 = **studio_v6 저작·Addressables
배포 대상**. runtime_t3는 다운로더 + 얇은 baked infra + 에디터 스텁일 뿐. → **studio_v6가 포트 대상일 가능성 높음.**
원 브리프 §4의 "XumFlow로 이식" 가정은 **이 결정 전까지 보류.**

**아직 확정 못 하는 부분(studio_v6 소스 필요):**
- `PromptScene.Core` 계약(`IRoomCore`/`IEventBus`/레지스트리 등)이 **baked 공유 infra**(양 프로젝트 공유 `Bridges`류)로 가야 하는지, **hot-update DLL(studio)** 안으로 가야 하는지 — 직렬화·계약 동기화 규칙(CLAUDE §6)에 걸린다.
- XumRuntimeKit(`#runtime-kit`) / XumStudioKit(`#studio-kit`)가 콘텐츠 저작 측에 어떤 스폰/씬/RoomCore류 계약을 이미 제공하는지 — 재구현 vs 편승 판단의 전제.
- studio_v6의 씬 계층/스폰 규약이 PromptScene 씬 계층(SYSTEMS/…/FEATURES/COMPOSITIONS)과 어떻게 겹치는지.

---

## 4. 미확정 / 후속 소스확인 (정직 계약)

- **studio_v6 부재** — 콘텐츠 저작 측 구조·계약 전부 미확인. `XumFlow/CLAUDE.md`의 경로(`D:/dev/Remote/test/studio_v6`)는 타 머신 좌표(이 머신에 `D:` 없음). clone/제공 필요.
- **XumFlow MCP 미가동** — `ai-game-developer`류 MCP가 이 세션에 미등록, `Library/` 없음(에디터에서 한 번도 안 열림) → 라이브 검증 전무.
- **C2 실제 룸 씬 미보유** — 스포너 배치는 studio 콘텐츠라 XumFlow 소스만으론 확인 불가(주석 근거만).
- **트랩 J/K 잔존 여부 미검증** — MST/XumLobby 공유이나 라이브 미확인.
- 미정독 파일(경계 판단엔 불요했음): `AddressablesRoomClientManager.cs`, `XumApiSceneProvider.cs`, `XumApiServerCatalogProvider.cs` 등 — 이식 착수 시 정독 대상.

---

## 5. 하드 스톱 상태

- **§4(이식)·§5(라이브 검증)** = 대기. 선행: (a) `studio_v6` 확보(위치 확인/clone), (b) 포트 타깃 결정(위 §3), (c) 대상 에디터 MCP 가동.
- 재개 시: 스킬 재검증(`assemble-room`/`scaffold-content`/`compose-room`/`deploy-client`)은 XumFlow 모델(Addressables/HotUpdate)에 맞춰 **절차 자체를 재작성**해야 함(§2 대조표의 "변경" 항목들).

---

## 6. 역기입 제안 (HANDOFF/기타 SSOT)

1. **HANDOFF §9 "oxr-sdk 레포 clone/fetch 금지"** — 이 문구는 *SDK 패키지*(PackageCache에 이미 있는 것) 스코프로 명시 필요. **XumFlow 같은 프로젝트 repo는 예외**(자격증명 provisioned 시 clone 정당). `XumFlow/CLAUDE.md` §8도 "SDK 패키지 수정 시 원격 clone·push"를 정상 절차로 규정 → §9 문구를 "SDK 패키지의 캐시만 고치지 말라(embed 후 작업)"로 정밀화 제안.
2. **HANDOFF §2 지형표에 좌표 추가** — `XumFlow = runtime_t3(다운로더)`, `studio_v6 = 콘텐츠 저작(원격 Addressables)` 이원 구조. XRCollabDemo(단일 프로젝트에 콘텐츠+런타임 동거)와 **모델이 다름**을 명시.
3. **contract C1~C4 / build-xumlobby-server** — XumFlow 타깃 시 위 §2 대조표의 "변경/폐기" 판정을 반영한 별도 절(또는 문서)이 필요(현행 문서는 XRCollabDemo 전제).

---

## 7. studio 브랜치 clone + 선행조건 점검 + 구조 매핑 (2026-07-22 후속 세션)

> 목표: studio(=`studio_v6`, 콘텐츠 저작 프로젝트)를 실제로 확보하고, 에디터 오픈 전 선행조건 점검 + 우리 코드 자리 확정.
> **상태: §3 선행조건 점검 + §6 구조 매핑 완료. 선행조건 2건(codebook/XREAL) 해소됨(2026-07-22, 사용자 승인 하). §4 에디터 오픈 진행 중.**

**클론:** `git clone -b studio` 성공 → `c:\J_0\XumFlow-studio` (gitignore `/XumFlow-studio/` 추가), 브랜치 `studio` @ `7ccd554`, Unity `6000.3.11f1`. runtime 클론(`c:\J_0\XumFlow` @ `runtime` 200a4a2)은 대조·인용용으로 **보존**(별도 디렉터리). studio는 top-level에 `SETUP.md`(26KB) 보유(runtime엔 CLAUDE.md). Library 없음(미오픈).

### §3a manifest 핀 (studio `Packages/manifest.json` = 유일 진위)

| 패키지 | studio 핀 | runtime 핀 |
|---|---|---|
| FishNet | `4.6.17` ✅ | `4.6.17` |
| HybridCLR | `4feac30` | `4feac30` |
| XumNet | `06584e0` | `06584e0` |
| UnifiedXRMotion | `40db6de` | `40db6de` |
| kit | `xumstudiokit` (#studio-kit) | `xumruntimekit` (#runtime-kit) |
| XRI Toolkit | `3.3.1` (studio 전용) | (없음) |
| MST | **(없음)** | `file:.../4.23.0.tgz` |
| XumBuildKit | **(없음)** | git #main |
| XumLobby | **(없음)** | git |
| backendAPI4MST(com.oxr.sdk) | **(없음)** | git |
| OpenXR / xr.hands | **(없음)** | 1.16.1 / 1.8.0 |

- FishNet **4.6.17** — Runtime 일치 + README 요구 충족(플래그 없음).
- studio manifest는 runtime보다 **얇다**: MST·XumBuildKit·XumLobby·backendAPI4MST·OpenXR·xr.hands **없음**; XRI Toolkit 3.3.1 + `xumstudiokit` **추가**. (studio는 Addressables/DLL 저작만, 플레이어 프리셋 빌드·로비·MST는 runtime 몫.)
- README 이원 핀 경고(브리프 §3a)는 studio에선 **무해** — manifest 단일 진위, FishNet 일치.

### §3b codebook ONNX 5종 — ❌ 부재이나 **로컬 회수 가능**
- `Assets/UXMModels/` 자체 없음 → SETUP.md §2.6대로 플레이어 스폰 시 `ModelLoader.Load` NRE로 아바타 깨짐 → **§5 아바타 스폰 신호를 구조적으로 막음**.
- **단, 동일 5개 .onnx가 이 머신의 XRCollabDemo UXM PackageCache에 존재:** `XRCollabDemo/Library/PackageCache/com.kisti.unifiedxrmotion@c03e35d8f24a/Assets/Prefabs/MotionModule/Codebook/*.onnx` (FutureBody/LowerBody/TrackedUpperBody/TrackerBody/UntrackedUpperBody 5종).
- ⚠️ 그 캐시는 UXM `@c03e35d8`, studio 핀은 `@40db6de` — **다운로드 아님(로컬 회수)** 이나 GUID 일치 확인 후 복사 판단(SETUP.md: zip meta보다 UXM 리포 meta 사용 권장, GUID 동일 주장). → 사용자 승인 대기.
- XREAL tgz는 이 머신 어디에도 없음(별개).

### §3c LocalPackages — ❌ 부재 (브리프 정정: studio는 XREAL tgz만 필요)
- `LocalPackages/` 없음. **브리프 §3c 정정: studio는 MST tgz 불요** — studio manifest에 MST 의존 없음, `Assets/MasterServerToolkit`도 없음(MST=runtime 전용, SETUP.md §3 vs §2).
- studio 요구는 `LocalPackages/com.xreal.xr.tar.gz` **단 하나**(manifest `file:` 참조). 없으면 첫 오픈에서 패키지 resolve 오류(SETUP.md §2.2). **이 머신 어디에도 없음** → 사람이 XREAL 개발자 사이트(developer.xreal.com) **3.1.0** tarball 배치 필요.
- 완화 근거: Standalone 그룹 define에 `USE_XREAL` **없음**(§3d) → 데스크톱 베이스라인은 XREAL 기능 불요, **resolve 오류만** 문제(컴파일 붕괴는 아닐 개연). manifest 수정은 가드 차단이라 불가.

### §3d ProjectSettings — ✅ PASS
Unity `6000.3.11f1` / `activeInputHandler=2`(Both) / `scriptingDefineSymbols`에 `FISHNET;FISHNET_V4` = Standalone·Android·Server 그룹 **모두** 존재. (managedStrippingLevel Standalone/Server=4(High)이나 studio는 플레이어 빌드 안 함 → 무관.)

### §3e XRI 샘플 — ✅ PASS
`Assets/Samples/XR Interaction Toolkit/3.3.1/{Starter Assets, Hands Interaction Demo, XR Interaction Simulator}` 3종 전부 임포트됨. + XR Hands 1.7.3 HandVisualizer, XumNet 0.5.4 Diagnostics Demo.

### §6 구조 매핑 (read-only, 에디터 불요 — 조기 완료)
- **Scan 경로 규약(SETUP.md §4-1):** 룸 씬 = `Assets/App/Scenes/`, 공유 네트워크 프리팹 = `Assets/App/Prefabs/`. 이 밖은 Content Manager 미표시.
- **기존 룸 씬:** `Assets/App/Scenes/{QuickRoom, QuickStart, T_RoomA, T_RoomB}.unity`. QuickStart=Quick Test 스타터, T_RoomA/B=플랫폼 분기 룸.
- **ContentLogic asmdef = baked 경계의 한 축:** `Assets/App/Scripts/ContentLogic/App.HotUpdate.asmdef` (name `App.HotUpdate`, `autoReferenced:false`, `includePlatforms:[]`). ⚠️ runtime에선 이 asmdef가 `includePlatforms:["Editor"]` **스텁**이었으나 **studio에선 실제 hot-update DLL**(Addressables 배포 대상) — §1 (2)의 경계가 여기서 실물로 확인됨. **references 집합** = `App.Bridges, Unity.TextMeshPro, MetaVoiceChat, FishNet.Runtime, XumNet.Runtime, Unity.InputSystem, Unity.XR.Interaction.Toolkit, UnifiedXRMotion, UnityEngine.UI, Unity.RenderPipelines.Core.Runtime`. → **PromptScene.Core/Content가 이 참조 집합 안이면**(FishNet/XumNet/XRI 접근 가능) 이 asmdef에 넣어도 baked 경계 内 = Smart Deploy로 끝. 별도 asmdef 시 이 참조 상속하도록 구성.
- **baked 경계(안 건드림) 확인:** `Runtime/Networking/Bridges/{PlatformGroup,PlatformPlayerEntry,RoomEventBridge}.cs` + `Runtime/Versioning/HotUpdateCodeManifest.cs` 존재.
- **플레이어 스폰 규약(C2 정정 확증):** `Assets/App/Prefabs/`에 `Desktop.prefab / UnityXR.prefab / XrealXR.prefab` + `Avatar/`, `Spawn/` → XumPlayerSpawner의 이유 = **"플랫폼별 다른 플레이어 프리팹 분기 스폰"**(멀티플랫폼 아님) 확증. 스폰 로직은 `ContentLogic/Spawn/Player`.
- ContentLogic 하위: `Spawn/Player`, `Tmp_Management`, `VoiceChat/Silero` (hot-update DLL 내용).

### 선행조건 해소 (2026-07-22, 사용자 승인)
- **§3c XREAL:** 사용자가 `C:\Users\master\Downloads\com.xreal.xr.tar.gz`(248,169,074 B) 받아둠 → `XumFlow-studio/LocalPackages/com.xreal.xr.tar.gz`로 배치(바이트 일치).
- **§3b codebook:** GUID 사전 검증 **PASS** — runtime 클론의 UXM `@40db6de` PackageCache 프리팹 `_modelAsset` GUID(LowerBodyRegressor=`2541af95`, UpperBodyRegressor=`27561c2b`, UpperBodyPredictor=`c0eec658`)가 XRCollabDemo UXM `@c03e35d8` 캐시의 `.onnx.meta` GUID와 **동일**(GUID는 커밋 무관 안정). → 5 `.onnx` + 5 `.onnx.meta`를 `Assets/UXMModels/Codebook/`로 복사(사용자가 PowerShell로 실행 — 가드가 PackageCache-소스 cp를 fail-closed 차단해 에이전트 직접 복사 불가, HANDOFF §7 함정에 기록). meta 5종 GUID 확인 완료.
- ⚠️ 가드 함정(신규): 가드는 PackageCache가 **소스**인 cp/mv도 차단(읽기 전용 의도라도) — 우회 금지 원칙상 사용자 셸로 실행.

### §4 에디터 오픈 — ✅ 성공 (2026-07-22)
- **콜드 오픈 PASS:** Unity `6000.3.11f1`로 studio 첫 오픈(직접 Unity.exe 실행 → Hub가 실제 에디터 PID 39924로 핸드오프). 콜드 임포트 완주(다수 private git-URL 패키지 해석 + HybridCLR + Addressables + 20+ ShaderCompiler). **CS 컴파일 에러 0 / compilation failed 없음 / Safe Mode 없음.** `App.Bridges.dll` + `App.HotUpdate.dll` 컴파일·ILPP·ScriptAssemblies 복사 성공(baked 경계 빌드됨).
- **codebook 임포트 검증(로그 실측):** `Assets/UXMModels/Codebook/*.onnx`가 Inference-Engine ScriptedImporter로 임포트되며 `Guid(2541af95…/c0eec658…/27561c2b…)` = UXM 프리팹 `_modelAsset` 참조 GUID와 일치 → 아바타 스폰 NRE 회피 확인(런타임 스폰 자체는 §5).
- **무해 로그(기록):** Unity Connect 403/400(클라우드 project-ID `c98596a7…` 조회 실패 — 라이선스 무관), URP `FallbackError` 셰이더 미발견 경고(마젠타 계열 — 미감 문제, 빌드 무관), `Assets/HybridCLRGenerate` 빈 폴더 meta 경고, FishNet Mirror 업그레이드 스크립트 미컴파일 안내.
- **MCP(ai-game-developer): 미부착 확정.** studio manifest·Assets·`.mcp.json` 어디에도 MCP 브리지 없음 → 이 세션에 Unity MCP 미등록. §5 Quick Test **자동 구동 불가**(MCP 선행) → §4 "MCP 안 붙으면 정지·보고" 발동.

### §5 베이스라인 — GUI Quick Test로 진행(옵션 2 선택), MCP는 그 뒤
에디터는 살아 있고 GUI 준비됨. MCP 미부착(+세션 재시작 선행)이라 §5는 **사람의 GUI Quick Test 구동**으로 즉시 증명하기로 결정(2026-07-22). MCP 셋업은 §5 결과 기록·커밋 후 재개(재시작 전 산출물 보존).

**⚠️ QuickTestStarter 소스 실측 정정 2건 (`Assets/App/Scripts/Tools/QuickTestStarter.cs` — 조립 스킬이 반드시 알아야 함):**
1. **단일 에디터에서 아바타 스폰을 보려면 `hostMode=true`도 필요.** `startAsServer=true`만이면 서버 전용 → 로컬 클라 없음 → 아바타 미스폰(룸 씬만 로드). host 모드(서버+클라, `:98`/`:118-123`)라야 로컬 클라가 붙어 XumPlayerSpawner가 아바타 스폰·렌더. 스크립트가 `ClearDefaultObserverConditions`(`:76` "Host Mode 가시성 문제 방지")로 host 가시성을 명시 처리 → **host가 의도된 단일 에디터 관측 경로.**
2. **Quick Test의 Room Scene Key = 씬의 "등록된 주소"와 매칭 — 단 leaf 폴백이 존재.** `QuickTestStarter`는 `SceneLoadData(new SceneLookupData(roomSceneKey))` + `Options.Addressables=true`(`:185-187`) + `AddressablesSceneProcessor`(`:227-259`)로 로드. **등록 주소(실측): `Scenes/T_RoomA`, `Scenes/T_RoomB` 둘 다 `Scenes/` 접두어 포함**(`Default Local Group.asset`). **그런데 shipped `QuickStart`의 `roomSceneKey` 기본값 = 바 leaf `T_RoomB`**(접두어 없음, QuickStart.unity `:155`) → shipped 기본이 leaf로 동작한다는 것은 **AddressablesSceneProcessor(또는 SETUP §4-1의 "파일명 폴백")가 leaf→`Scenes/<Room>`을 해석**함을 의미. → **실무 규칙: shipped leaf 키(`T_RoomB`)를 그대로 쓰면 되고, InvalidKeyException 시에만 전체 주소 `Scenes/<Room>`으로.** (앞선 "반드시 접두어" 서술은 과단 — 정정.)

> **대조표 편입(다음 세션 조립 스킬용):** 씬 주소 규약 — 등록 주소는 `Scenes/<Room>`(Content Manager `Apply` 기본)이지만, **로드 키는 leaf(`<Room>`)로도 폴백 해석된다**(런타임 room-server `-room.scene_key`와 Quick Test AddressablesSceneProcessor 양쪽 모두 파일명 폴백 보유). 즉 XRCollabDemo의 "leaf" 규약이 studio에서도 통하지만, **폴백이 꺼지거나 동명 씬이 여럿이면 전체 주소가 안전**. assemble/compose 스킬은 씬 등록 시 주소를 기록해두고 로드 키로 leaf 우선·전체주소 폴백을 쓰도록 짤 것. (InvalidKey 관측 시 전체주소 사용이 결정적 단서.)

**§5 실행 체크리스트(사용자 구동 중, 최소 변경판):** Content Manager > Quick Test ON(Addressables Play Mode=Use Asset Database) → `QuickStart.unity` → `Starter`(QuickTestStarter): shipped 기본(`startAsServer`✅, `roomSceneKey=T_RoomB` leaf) 유지 + **`hostMode`만 0→✅로 변경**(단일 에디터 아바타 관측 필수) → Play. 신호 ①룸(T_RoomB) 로드 ②아바타 스폰 ③모션(머리/몸 카메라 추적) + 콘솔 이상(ModelLoader NRE/InvalidKey/missing script) 관측. 옵션: XR Interaction Simulator로 NetCube 잡기/던지기(M2/V1 경로).

**§5 결과 (2026-07-23 — 사용자 GUI 라이브 관측 PASS):** `QuickStart`→`Starter`(`startAsServer`✅ + `hostMode`✅ + `roomSceneKey=T_RoomB`)→Play. **①T_RoomB 룸 씬 로드 PASS ②아바타 스폰 PASS ③아바타 모션(머리/몸 카메라 추적) PASS.** XREAL 관련 빨간 콘솔 에러 = 무해(무시). ⚠️ **관측 주체 = 사용자(GUI 육안), MCP/에이전트 자동판정 아님** — 정직 계약상 "사용자 라이브 관측"으로 기록(에이전트 하네스 판정 아님). → **studio "환경이 산다" 증명 완료: 컴파일 레벨(§4) + 런타임 스폰·모션(§5) 양쪽.** codebook GUID 회수(§3b)가 실제로 아바타 스폰·모션까지 이어짐이 라이브로 확인됨.

### MCP 설치 실패 사후분석 (2026-07-23) — studio는 0.76.3 조합 불가
studio에 `com.ivanmurzak.unity.mcp` 설치 시도 → 컴파일 실패 연쇄. **근본 원인 = UXM 1.8.5(@40db6de)가 신버전 MCP 전용 어댑터를 too-loose 게이트로 포함.**
1. 0.86.1 설치 → CS0246류(버전 churn).
2. 0.76.3 다운그레이드(XRCollab 핀 맞춤) → CS0115 `UnityMcpPlugin.Config.cs` (`UnityConnectionConfig.Token` override 대상 없음). 원인: `Assets/Plugins/NuGet/{McpPlugin,McpPlugin.Common,ReflectorNet}.dll`이 0.86.1 잔존(**UPM 다운그레이드는 Assets/ 일반 DLL을 안 되돌림**) → 0.76.3 소스가 0.86.1 base를 override 못 함.
3. DLL을 0.76.3(XRCollab 복사)으로 교체 → CS0115 해소, 그러나 **CS0234/CS0246** `UnifiedXRMotionMcpAdapter.cs`: `com.IvanMurzak.Unity.MCP.Runtime.Data.GameObjectRef` 요구 — 0.76.3엔 없음.
- **핵심:** UXM 1.8.5 `UnifiedXRMotion.Editor.asmdef` versionDefine = `com.ivanmurzak.unity.mcp` `0.66.0`(≥0.66.0이면 `UNITY_MCP_PRESENT` on). 어댑터는 0.86.x API 사용 → 게이트 하한이 실제 API 요구보다 낮아 0.76.x에서 오컴파일. **어댑터=신버전 전용인데 게이트가 느슨.**
- **XRCollab 대조(실측 확인):** XRCollab UXM = **1.8.1**(@c03e35d8) — `Editor/`에 `ReadOnlyAttribute.cs`·`InterfaceTypeDrawer.cs` **2개뿐, McpTools/어댑터/Editor asmdef 자체가 없음.** studio UXM = **1.8.5**(@40db6de) — `Editor/Scripts/McpTools/`(UnifiedXRMotionMcpAdapter + AvatarBinder/ScenePresetPlacer/SceneInspector/DevicePresetSelector + Models) 전체 보유. **즉 어댑터는 1.8.1→1.8.5 사이에 신설된 것 — XRCollab에서 "지운" 게 아니라 애초에 없음.** XRCollab이 0.76.3으로 컴파일되는 건 이 때문. **버전 조합은 프로젝트별 UXM 버전 종속(XRCollab 0.76.3 조합을 studio 1.8.5에 이식 불가).**
- **"어댑터만 무력화(옵션 B)" 기각:** 어댑터는 immutable PackageCache 소재 → 무력화하려면 UXM를 embed(Packages/ 복사·git핀 해제·편집)해야 하는데 침습적 + 코어 아바타 패키지가 핀에서 이탈. 게다가 McpTools는 어댑터 외 AvatarBinder/ScenePresetPlacer/SceneInspector 등 **UXM의 MCP 노출 도구** 포함 → 끄면 MCP+UXM 자동화 자체를 버림. **정답은 어댑터 무력화가 아니라 UXM 1.8.5 호환 MCP 버전(0.86.x) 설치.**
- **해소(임시):** MCP 패키지 제거 → `UNITY_MCP_PRESENT` off → 어댑터 `#if` 제외 → studio 클린 컴파일 복구(0 에러). §5 GUI 진행 가능. scopedRegistry는 존치.
- **정식 MCP 셋업 — 버전 `com.ivanmurzak.unity.mcp@0.66.0` 설치 ✅ 완료(2026-07-23).** 절차 실행: 패키지 제거 → `Assets/Plugins/NuGet/` 폴더 전체(85파일) + `.meta` 삭제(에디터 닫고) → manifest에 `@0.66.0` 추가(사용자) → 재오픈 → **0.66.0의 NuGet 자동복원기(`Editor/DependencyResolver/NuGetPackageInstaller.cs`)가 43개 NuGet 패키지를 버전폴더로 다운로드**(System.Text.Json 8.0.5, com.IvanMurzak.ReflectorNet 5.0.0, McpPlugin 6.1.0 등) → AssetDatabase refresh → 재컴파일 **`Loaded All Assemblies`, CS 에러 0, Safe Mode 아님. UXM 어댑터 컴파일 통과(Runtime.Data 충돌 해소).** 서버바이너리 `Library/mcp-server/win-x64/unity-mcp-server.exe` = 0.66.0 자동 배치(수동 복사 불요). UPM==server==DLL 모두 0.66.0.
  - **함정(신규):** 0.66.0 설치기는 **NuGet 네트워크 자동복원(버전폴더 레이아웃)** — 0.76.3/0.86.1의 flat-DLL과 다름. 폴더 전체 삭제 후 재설치 시 **다운로드 완료 전 수천 개 CS0234/0246(System.Text.Json/ReflectorNet 부재)가 과도기적으로 뜸** → 복원 완료(`[NuGet] Package restore complete`) 후 자동 해소. **행 아님 — 다운로드 대기.**
  - **STEP4 모델 차이(주의):** 0.66.0 `server.json` = **stdio 기본 + `MCP_PLUGIN_PORT`(기본 8080)** — XRCollab 0.76.3의 **HTTP+Bearer@27826**과 모델 다름. 정확한 등록 명령은 **AI Game Developer 창(0.66.0 authoritative)**에서 취득.
- **STEP4 등록 ✅ 완료(2026-07-23):** AI Game Developer 창이 **HTTP @ `http://localhost:21017` (토큰 없음)** 제시 → `claude mcp add --transport http ai-game-developer http://localhost:21017` (local scope, project c:\J_0). **`✔ Connected` 확인.** ⚠️ 실제 포트=**21017**(플러그인 자동 할당, 계획했던 27827 아님), 이름=**`ai-game-developer`**(별도 `-studio` 아님 — 죽은 XRCollab@27826 등록을 같은 이름으로 대체. XRCollab MCP 재사용 시 별도 이름 재등록 필요). Bearer 토큰 불요(0.66.0 창이 무토큰 http 제시).
- **STEP6 재시작 완료 + MCP 제어 검증 ✅(2026-07-23, 재시작 후 세션):** MCP 툴 로드됨(`mcp__ai-game-developer__*` + `uxrm-*`). **§5를 MCP로 재현** — `script-execute`로 play mode 진입 → 서버가 **T_RoomB 로드**(scene-list-opened에 T_RoomB + MovedObjectsHolder) → **`Desktop(Clone)` 아바타 스폰**(NetworkObjects=4) → **UXM 모션 rig**(LowerBodyRegressor/MotionSeries, motionComps=7 — §3b codebook 계승) → play mode exit + 테스트값(hostMode) 원복. `scene-list-opened`/`console-get-logs`/`script-execute`(읽기+쓰기) 동작 확인. **= 에이전트가 studio 실제 제어 가능 = MCP 셋업 진짜 완료.** ⚠️ `editor-application-set-state`는 이 세션 deferred 목록에 미로드 → play mode는 `script-execute`(EditorApplication.isPlaying)로 구동. console-get-logs는 "2 event systems" 경고(무해, QuickTest가 EventSystem 생성)로 폭주 가능 → 스폰 확인은 scene/gameobject 직접 조회가 더 확실.

## 8. 이식 착수 전 확정 (STEP 3, read-only — 2026-07-23)

첫 기능(Ruler 등) 클린 재저작 착수 전 3대 결정 확정. 근거 = studio 소스 + `DETAILS.md`.

**3a. PromptScene.Core/Content 자리 = `App.HotUpdate`(ContentLogic) 안, 별도 asmdef 불요.** ContentLogic은 단일 `App.HotUpdate.asmdef`(하위 asmdef 없음, subdir=Spawn/Player·Tmp_Management·VoiceChat/Silero). references가 이미 우리 수요 전부 커버(FishNet.Runtime, XumNet.Runtime, XR.Interaction.Toolkit, UnifiedXRMotion, InputSystem, UI, App.Bridges). → `Assets/App/Scripts/ContentLogic/PromptScene/`에 Core+Content 소스로 투입 = baked 경계 内 = **Smart Deploy로만 배포**(runtime 재빌드 불요). 별도 asmdef는 같은 refs 재선언만 되므로 불필요.

**3b. 직렬화 지뢰 회피 = DETAILS.md 명문 규칙(브리프 승인 패턴 일치).** ① 새 직렬화 스크립트 지양(기본 컴포넌트+Inspector), **ScriptableObject 금지**(HybridCLR 붕괴). ② 커스텀 직렬화 MonoBehaviour 필요 시 **씬에 직접 박기(별도 Prefab 자산 금지)** — "씬 로더는 hot-update MonoBehaviour SerializedField를 채우나 **Prefab 자산 로더는 안 채우는 케이스**"가 핵심 지뢰. ③ `[Serializable]` 데이터 컨테이너(List<Foo> 직렬화) → **App.Bridges(baked)**, hot-update 두면 미스매치. ④ NetworkBehaviour(FishNet RPC)=검증됨. → **승인 패턴 확정: 프리팹=기본 컴포넌트만(XR Grab Interactable/Rigidbody/NetworkObject), hot 뷰 직렬 필드=씬 임베드 or 런타임 코드 배선, 데이터 컨테이너=App.Bridges.** (연관: "Hot-update nested type 함정", `Quaternion? rotation` 스폰 뒤집힘 함정.)

**3c. assemble/compose 스킬 Smart-Deploy 재작성 스코프(중~대).** XRCollab의 "Master/Room.exe 재빌드→서버 실행→에디터 클라 조인" → studio 모델로: **등록**=Content Manager Scenes `Scan/Apply`(룸=`Assets/App/Scenes/`) + Network Prefabs `Apply & Generate`(=신 C1, DefaultPrefabObjects+Addressables 동시 재생성) / **검증(에디터)**=Quick Test Mode(startAsServer+**hostMode**, 이번에 MCP로 구동한 경로) / **배포(원격)**=Build & Package→Bundle Uploader(exe 재빌드 없음). **잔존**: FishNet SceneId 규율(씬 NetworkObject 추가/삭제→전 플랫폼 재배포, DETAILS §7)·leaf 씬키 폴백. 스킬 EXECUTE/VERIFY 단계 교체가 주 작업, 씬 조립·SceneId 할당·자기등록·§5 FEATURES 체크는 유지. **라인 단위 재작성은 4개 스킬 EXECUTE/VERIFY 정독이 선행(다음 단계).**
- **함정 기록:** ① MCP 실 DLL이 Assets/Plugins/NuGet(비-UPM, `.nuget-installed.json` 마커 기반) → 버전 변경 시 자동 재동기 안 됨, 수동 삭제 후 재설치 필요(안 하면 CS0115). ② UXM 1.8.5 versionDefine(≥0.66.0)이 실제 API 요구보다 느슨 → 0.68.0+ AIGD 리네임 버전에서도 어댑터가 켜져 CS0234/CS0246. UXM 측이 상한(<0.68.0) 미설정 = 버그 후보.

### MCP 버전 확정 (2026-07-23, 증거 기반 — "0.86.x 추측" 폐기)
UXM 1.8.5 어댑터가 `using com.IvanMurzak.Unity.MCP.Runtime.Data;`(+`GameObjectRef`)를 씀. Unity-MCP 레포(github IvanMurzak/Unity-MCP) 태그별 `Runtime/Data/GameObjectRef.cs` 네임스페이스 실측:

| MCP 버전 | 네임스페이스 | 판정 |
|---|---|---|
| 0.66.0(2026-04-17) ~ **0.67.3**(2026-04-30) | `com.IvanMurzak.Unity.MCP.Runtime.Data` | ✅ UXM 1.8.5 호환 |
| 0.68.0(2026-05-02) → 0.86.1(latest) | `AIGD` (리네임) | ❌ CS0234/CS0246 |

- **호환 창 = 0.66.0–0.67.3**(네임스페이스 기준). **직접 검증된 정확 버전 = `0.66.0`.**
- **직접 증거(추론 아님):** UXM 리포 `docs/superpowers/plans/2026-04-23-unifiedxrmotion-mcp-v1-verification.md`(@40db6de) 명시 — `Unity: 6000.3.11f1` + `com.ivanmurzak.unity.mcp: 0.66.0`에서 4개 `uxrm-*` 툴 **end-to-end 동작 검증**. 어댑터는 2026-04-23 저작 후 무변경(git log) → **0.66.0 대상 저작이지 "최신 기대 상류버그" 아님.** 검증 Unity(6000.3.11f1)=studio Unity와 동일.
- **0.66.0 호환성:** `unity` 하한 `2022.3`(6000.3 상회), `GameObjectRef.pre-Unity.6.5.cs` 폴백 동봉(→ `#if UNITY_6000_5_OR_NEWER` 게이트에도 6000.3에서 컴파일), dep `extensions.unity.playerprefsex 2.1.3`(MCP 전용 transitive, studio 타 패키지 무관·FishNet 4.6.17 독립).
- **확정 vs 시도(STEP1):** 로컬 UXM 1.8.5 패키지 자체엔 MCP 명시 dep 없음(package.json), CHANGELOG 비어있음, asmdef versionDefine=하한 `0.66.0`뿐. **명시 검증 버전은 UXM *리포* 검증문서(0.66.0)에서 옴 → `0.66.0`=확정, `0.67.x`(동일 네임스페이스 창)=폴백.**
- **STEP2 도구 인벤토리(0.66.0, read-only 확정):** 우리 워크플로 도구 전부 존재 — `Script.Execute`(script-execute/Roslyn), `Scene.Open/Create/Save`, `GameObject.*`+`Component.*`, `Reflection.MethodCall/Find`, `Console.GetLogs`, `Editor.Application.Get/SetState`, `Screenshot.GameView`. **길 A로 워크플로 도구 손실 0.**
- **모든 실패 설명됨:** 0.76.3(XRCollab 핀, AIGD era)→CS0234 / 0.86.1(사용자 첫 시도, AIGD era)→CS0246. **0.86.1 실패는 DLL-sync 아니라 네임스페이스 리네임.** XRCollab이 0.76.3으로 되는 건 그쪽 UXM 1.8.1에 어댑터가 아예 없어서(무관).
- **정합성 규칙(기록):** **MCP 버전은 프로젝트의 UXM 버전에 종속.** studio(UXM 1.8.5)=`0.66.0`(검증), XRCollabDemo(현재 UXM 1.8.1, 어댑터 없음)=`0.76.3` → **두 프로젝트 MCP 버전 분리, 서버 바이너리·DLL 재사용 불가, 포트도 분리(studio 27827 / XRCollab 27826).** 각 프로젝트가 자기 UXM에 맞는 MCP를 쓰는 게 정상.

### XRCollabDemo 기억 충돌 해소 (2026-07-23, 소스 확정) — 둘 다 사실
- **현재 상태(소스):** XRCollab MCP=`0.76.3`(manifest 핀+lock registry+PackageCache `@8d8d7aff4358` 단일), UXM=`1.8.1`(`@c03e35d8`) — **McpTools/어댑터 부재 확인.** 단일 해시(churn 잔재 없음). → **현재는 어댑터 없어 무충돌**(에이전트 주장은 *현재 잠금 상태* 한정 참).
- **어댑터 최초 탑재 = UXM 1.8.2**(2026-06-01). 1.8.0/1.8.1엔 없음(gh contents 404 확인).
- **사용자 기억도 사실:** XRCollab UXM는 **핀 없는 git URL**(`…/UnifiedXRMotion.git`, `#hash` 없음) → resolve 시점 latest로 해석, 현재 lock이 1.8.1로 고정. **2026-06-01 이후 재resolve하면 UXM 1.8.2+(어댑터)가 핀된 MCP 0.76.3(AIGD era)과 충돌 → 동일 CS0234/CS0246.** 이후 1.8.1로 재잠금되어 마스킹됨.
- **함정(신규):** XRCollab은 **잠재 취약** — unpinned UXM URL이라 packages-lock 리셋 시 UXM 1.8.5를 끌어와 재파손. 현재 컴파일되는 건 pre-adapter 1.8.1 고정 덕. XRCollab을 현행 UXM으로 올리면 그쪽도 MCP `0.66.0` 필요(0.76.3은 어댑터-없는 구 UXM 전용).

### HANDOFF §7 함정 패치 제안 (적용은 승인 후)
1. **codebook 부재 NRE(신규 지뢰):** studio 첫 오픈 전 `Assets/UXMModels/Codebook/` .onnx 5종 필요. 로컬 회수처 = XRCollabDemo UXM 캐시(위 §3b). 없으면 아바타 스폰 NRE(입장·씬로드는 정상 → 오진 유발).
2. **LocalPackages 이원성:** runtime=MST tgz + XREAL tgz **둘**, studio=XREAL tgz **하나**. 브리프 §3c가 둘을 혼동(정정).
3. **manifest 핀 이원화 경고:** studio에선 무해(manifest 단일 진위, FishNet 4.6.17 일치).
4. **XumFlow 이중 클론 좌표:** runtime `c:\J_0\XumFlow`(@200a4a2) / studio `c:\J_0\XumFlow-studio`(@7ccd554) — 같은 repo, 다른 브랜치, 다른 프로젝트(다운로더 vs 콘텐츠 저작).
5. **씬 주소 규약(신규 지뢰 — 조립 스킬 필수):** 등록 주소는 `Scenes/<Room>`이나 **로드 키는 leaf(`<Room>`)로도 폴백 해석**(shipped QuickStart 기본이 `T_RoomB` leaf로 동작 — QuickStart.unity `:155`). leaf 우선 사용, `InvalidKeyException` 시 전체주소 `Scenes/<Room>`. + Quick Test 단일 에디터 아바타 관측은 **`hostMode=true` 필수**(서버 전용은 미스폰). 근거: `Assets/App/Scripts/Tools/QuickTestStarter.cs:185-187,227-259`(Addressables 로드+프로세서)·`:98,118`(host).

---

## 9. 첫 관통 (길 1 — 샘플룸 위에 RoomCore + Ruler 얹기) — ✅ 성공 (2026-07-23, MCP 라이브 판정)

> 목표: studio 샘플룸(T_RoomB) 재사용 SYSTEMS 위에 **RoomCore + 첫 FEATURE(Ruler)** 를 얹어, contract §5(FEATURES)·§6.5(런타임) 신호가 studio에서 라이브로 성립함을 증명. 절차 = §2 대조 → §3 베이스라인 → §4 RoomCore → §5 Ruler, 각 단계 QuickTest(startAsServer+hostMode+roomSceneKey, **MCP 구동**) 얹기 전/후 비교. **판정 주체 = 에이전트 MCP 자동판정**(scene/gameobject/reflection 조회 — §5 사용자 GUI 관측과 대비).

### §2 선행 대조표 — 샘플룸 SYSTEMS(studio 실측) vs contract §1 (얹기 전, read-only)

| contract §1 SYSTEMS | studio 실측 | 위치 | 판정 |
|---|---|---|---|
| Network (R-RoomServer/NetworkManager…) | `NetworkManager`(FishNet NM + NetworkHudCanvas) | **QuickStart 부트 씬** (룸 씬 아님 — Addressables 모델) | 이미 있음. RoomCore는 `InstanceFinder`(전역) 접근 → 동거 불요 |
| Player (--PLAYER_SPAWNER + XumPlayerSpawner) | `--PLAYER_SPAWNER` = `XumPlayerSpawner`+`FishNet.Object.NetworkObject`+`XumNet.XumNetwork`+`XumSimpleSpawnClientExample` | **T_RoomB 룸 씬** | 이미 있음(C2). 씬 NetworkObject → 복제 시 SceneId 규율. `XumNetwork` 컴포넌트가 `XumNetwork.Instance` 싱글턴 공급(FishNetSpawn 스폰의 전제) |
| RoomCore (IRoomCore + Registry) | **없음** | — | **우리가 채움(§4)** |
| ENVIRONMENT | Directional Light + `Plane`(MeshCollider) + `Capsule`(CapsuleCollider) | T_RoomB | 이미 있음. 클릭 레이캐스트 타깃 확보 |
| UI | `Canvas/MessageWindow` + `R-MasterCanvas/RoomHudView`(PlayerList/Leave) | T_RoomB | 이미 있음 |
| FEATURES | 없음 | — | **우리가 채움(§5 Ruler)** |

**IRoomCore 4서비스 성립(studio 소스 실측 — oxr-source-scout):**
- **INetSpawn(FishNetSpawn)** ✅ `XumNet.XumNetwork.Instantiate(NetworkObject,Vector3,Quaternion,NetworkConnection)` = **static**, 반환 `GameObject`, **클라 호출 시 ServerRpc 왕복→null 반환**(FishNetSpawn·RulerMeasurementView가 이미 이 동작 전제). `InstanceFinder`(NetworkManager/IsClient·ServerStarted/ClientManager.Connection[**field**]/ServerManager.Despawn), `NetworkObject.IsSpawned`, `NetworkBehaviour.Despawn/IsOwner/IsServerStarted`, `[ServerRpc(RequireOwnership=false)]`·`[ObserversRpc(BufferLast=true)]` 전부 FishNet **4.6.17** 시그니처 일치. asmdef `XumNet.Runtime`+`FishNet.Runtime`는 `App.HotUpdate`가 이미 참조. 경로: `XumNetwork.cs:1041`, `InstanceFinder.cs:27/59/70/161/169`.
- **IInteraction(SimpleClickProvider)** ✅ 레거시 `UnityEngine.Input`(studio Active Input=Both) + 레이캐스트 타깃(Plane/Capsule) + 카메라(아바타).
- **IRoomUserState(LocalUserState)** ✅ 로컬 스텁(`MultiScaleName=>string.Empty`), studio 소스 의존 0 — "MultiScaleName 소스 부재" 우려 무의미(Ruler 미사용).
- **IEventBus(EventBus)** ✅ 순수 C#.
- **판정: 막는 불일치 0.** §2 경계의 "API 시그니처 다를 수 있음"이 실측으로 전부 해소.

### 얹기 전/후 QuickTest 비교 (MCP 자동판정, 룸=PromptSceneRoom_1, startAsServer+hostMode)

| 단계 | 신호 | 결과 |
|---|---|---|
| **§3 베이스라인**(복제만) | 룸 로드 / 아바타 `Desktop(Clone)` 스폰 / UXM 모션 rig(LowerBodyRegressor 등) / 콘솔 에러 | **PASS** — 룸 로드(Addressables leaf), 아바타 스폰(=복제 스포너 SceneId 유효 증명), 모션 rig, Error 0 |
| **§4 RoomCore 얹은 후** | (a)아바타 여전히 스폰(SYSTEMS 무손상) (b)RoomCore.Instance 초기화 + 빈 레지스트리 + 내장 4서비스 등록 + SimpleClickProvider 자동추가 | **PASS** — 아바타 스폰 유지, `Instance` 초기화, `Contents.All=0`, 서비스=[IEventBus,IInteraction,INetSpawn,IRoomUserState], Error 0 |
| **§5 Ruler 얹은 후** | (a)SYSTEMS 정상 (b)ruler 자기등록(`Contents.All=[ruler]`, Meta 룰러/측정) (c)SetEnabled(true/false) 무예외 (d)측정 실동작 | **PASS** — 아바타 스폰 유지. ruler 자기등록. SetEnabled 양방향 무예외. **바닥 Plane 실 raycast 2점→OnClick×2→`RulerMeasurement(Clone)` 네트워크 스폰(pos=(1,0,1)=중점), LineRenderer(끝점 전파)+DistanceLabel '1.41 m'(=√2) 빌드, NetworkObject.IsSpawned=True**, Error 0 |

### 산출물 (studio, `Assets/App/`)
- **룸:** `Scenes/PromptSceneRoom_1.unity`(T_RoomB 복제) + Addressables 등록(leaf 주소 `PromptSceneRoom_1`, 라벨 `RoomScene`, `Default Local Group`).
- **Core:** `Scripts/ContentLogic/PromptScene/Core/{Contracts,RoomContentRegistry,SimpleClickProvider,RoomCore}.cs`(namespace `PromptScene.Core`, App.HotUpdate 어셈블리 内 — Smart-Deploy 경계). **소스 무개조 이식**(§2에서 API 차이 0 확인 → verbatim).
- **FEATURE:** `Scripts/ContentLogic/PromptScene/Content/Ruler/{RulerContent,RulerMeasurementView}.cs`.
- **네트워크 프리팹:** `Prefabs/RulerMeasurement.prefab`(NetworkObject + RulerMeasurementView) → DefaultPrefabObjects 편입(6→7) + Addressables(`Network/Prefabs/RulerMeasurement`, `Network/DefaultPrefabObjects`).
- **씬 계층:** `===== SYSTEMS =====`/RoomCore, `===== FEATURES =====`/Ruler(measurementPrefab 배선). 기존 네트워크 씬 오브젝트(--PLAYER_SPAWNER) **재배치 안 함**(SceneId churn 회피 — 헤더 폴더는 신규 추가만).

### 스킬 Smart-Deploy 재작성용 실측 절차 (다음 단계 = 4스킬 EXECUTE/VERIFY 정독 후)
- **씬 등록:** `ContentManagerWindow.RegisterScenes` = `settings.AddLabel("RoomScene")` → `CreateOrMoveEntry(guid,"Default Local Group")` → `entry.address=leaf` → `entry.SetLabel("RoomScene")`. ⚠️ **GUI `Apply`는 백엔드 씬이름 중복검사(로그인 게이트, 401)를 먼저 탄다**(`ContentManagerWindow.cs:1068-1076`) — 로컬 베이스라인은 이 검사 불요이므로 Addressables 쓰기만 직접 재현하면 됨(타인 번들 충돌 가드는 원격 배포 때만 의미).
- **네트워크 프리팹 등록(신 C1):** `FishNet.Editing.PrefabCollectionGenerator.Generator.GenerateFull(null,false,true)`(리플렉션) → `Assets/DefaultPrefabObjects.asset` 프로젝트 스캔 재생성 → `RegisterDefaultPrefabObjectsInAddressables`(addr `Network/DefaultPrefabObjects`). 프리팹은 `Assets/App/Prefabs/`에 둬야 스캔됨.
- **QuickTest MCP 구동:** `QuickTestStarter`(startAsServer=true+**hostMode=true**+roomSceneKey=leaf) SerializedObject 세팅 → `EditorApplication.isPlaying=true` → scene-list-opened/scene-get-data/gameobject-find/reflection으로 판정 → `isPlaying=false` + 테스트값 원복(디스크 미저장). `console-get-logs`는 "2 event systems" 경고 폭주 가능 → Error 필터 + 씬/오브젝트 직접 조회가 확실.
- **측정 주입(하네스):** 단일 에디터 MCP는 실제 마우스 이동 불가 → `Physics.Raycast`(바닥 Plane)로 실 RaycastHit 획득 → private `RulerContent.OnClick(hit)` 리플렉션 2회. **정직 캐비엇: "실제 마우스 클릭 이벤트→레이캐스트" 자체는 미검증**(D2/M4와 동일 경계) — 검증된 것은 OnClick 이후 측정·스폰·전파 경로.

### 정직 계약 (증명 범위)
- **증명됨:** RoomCore(4서비스) + Ruler(자기등록·토글·네트워크 측정 스폰·RPC 끝점 전파)가 **studio 샘플룸 SYSTEMS 위에서 §5/§6.5 성립**. 단일 에디터 host QuickTest, MCP 자동판정, Error 0.
- **밖(후속 세션):** 2클라 파리티(별도 데스크톱 프로세스), 실제 마우스-클릭 레이캐스트, TargetProps/ScoreHud/COMPOSITION(D2) 이식, 원격 Addressables 배포(Build & Package), 실기기/XR 입력. **스킬(assemble/compose/scaffold) Smart-Deploy 재작성**은 이 관통으로 절차가 실측되었으니 다음 단계(스킬 정독 후).

### 구조 정리 — 5층 폴더 안으로 (✅ 2026-07-23, SceneId 안전 검증 동반)

관통 직후 `PromptSceneRoom_1`을 contract §1 씬 계층으로 완전 정리. **각 이동마다 QuickTest**, 특히 FishNet 씬 네트워크 오브젝트는 SceneId 검증. **studio판 실측 하이어라키(최종, 디스크 저장 확인):**

```
PromptSceneRoom_1.unity
├── ===== SYSTEMS =====
│   ├── RoomCore                     RoomCore(+SimpleClickProvider 런타임 자동추가)
│   └── Player
│       └── --PLAYER_SPAWNER {sp}    XumPlayerSpawner + NetworkObject + XumNetwork + XumSimpleSpawnClientExample
├── ===== FEATURES =====
│   └── Ruler                        RulerContent (measurementPrefab 배선)
├── ===== ENVIRONMENT =====
│   ├── Directional Light
│   ├── Plane {pt}                   MeshCollider (클릭 레이캐스트 바닥)
│   └── Capsule                      샘플룸 장식 primitive(스크립트 0) — 보존
└── ===== UI =====
    ├── Canvas {MessageWindow}
    └── R-MasterCanvas {RoomHudView} 룸 HUD(PlayerList/Leave)
```

**contract §1 정본과의 studio 편차(정당):**
- **Network 하위폴더 없음** — `NetworkManager`는 **부트 씬(QuickStart/T_Master)** 소재(Addressables 모델). 룸 씬에 Network 층 불요. RoomCore는 `InstanceFinder` 전역 접근.
- **COMPOSITIONS 층 없음** — 컴포지션 부재 시 미생성(contract §1 "있을 때만 존재"·"수요 없는 층 금지" launchpad/D2 교훈).
- **_DYNAMIC 없음** — 런타임 생성물(아바타 Clone, RulerMeasurement(Clone))은 play 중에만 등장(에디터 정적 씬엔 없음).

**⚠ SceneId 안전 검증 결과 (신규 실측 — 조립 스킬 필수):**
- **FishNet 씬 네트워크 오브젝트(--PLAYER_SPAWNER)를 `SYSTEMS/Player`로 재부모해도 SceneId 유지.** 이동 전/후 모두 `SceneId=4290510823`(불변) `IsSceneObject=True`. 이동 → `EditorSceneManager.SaveScene`(FishNet `sceneSaving` 훅 발화) → 재읽기로 확인 → QuickTest 아바타 스폰 **유지 PASS**.
- **contract §1 "SceneId=0 함정"은 여기서 미발생.** 그 함정은 *한 script-execute 안 씬 생성→배치→저장*(훅이 비결정적으로 스킵)에 국한. **지속 오픈 씬에서 재부모+SaveScene은 훅이 정상 발화**해 SceneId를 보존·검증. → `CreateSceneId(force)` 폴백 **불요**(있으면 안전망). **규칙: studio에서 씬 네트워크 오브젝트 재배치는 (a)persistent 오픈 씬에서 (b)SaveScene(MCP scene-save 또는 EditorSceneManager.SaveScene)로 훅 발화 (c)SceneId!=0 && IsSceneObject 재확인 (d)QuickTest 아바타 스폰 — 4단계면 안전.**
- 비-네트워크 오브젝트(Light/Plane/Capsule/Canvas) 재부모는 SceneId 무관, 일괄 이동 후 1회 QuickTest로 충분.

**정리 판정:** 5층(SYSTEMS/FEATURES/ENVIRONMENT/UI) 완성 + §5/§6.5 전부 **여전히 PASS**(아바타 스폰·RoomCore 4서비스·ruler 자기등록·SetEnabled·측정 스폰 pos=중점+LineRenderer 전파+라벨 거리, Error 0). 작동 유지하며 구조 완성 — 타협(스폰 루트 잔류) 불필요.

### Ruler 최소 HUD — 사람 조작 UI (✅ 2026-07-23, IMGUI 채택, 사람 라이브 판정)

목표 = 사람이 화면 UI로 Ruler ON/OFF + 측정 지우기. XRCollab `RulerHudUI`(HANDOFF §5c) 패턴 이식.

**UI 방식 결정: IMGUI(OnGUI) — uGUI 대신 (사용자 선택, 근거 제시 후).** studio UI 실측(read-only + 런타임):
- **기존 UI(2a):** `===== UI =====`의 `Canvas`=**WorldSpace** 메시지 보드(MessageWindow=TextMeshProUGUI, worldCam=null, 비대화형) / `R-MasterCanvas`(Transform만) → 자식 `RoomHudView`=**ScreenSpaceOverlay** Canvas+GraphicRaycaster+CanvasGroup(PlayerList/Leave 버튼 = hot-update 구동, interactable+리스너 wired).
- **⚠ 입력 경로(2b) — 신규 실측:** 룸 씬 런타임에 **활성 EventSystem이 2개** — 아바타 `EventSystem`(**InputSystemUIInputModule**) + QuickTest 생성 `[QuickTest] EventSystem`(**StandaloneInputModule**, = `EventSystem.current`). 이것이 migration이 언급한 "2 event systems" 경고의 실체. 정적 씬엔 EventSystem 0(런타임 아바타가 공급).
- **판정:** uGUI도 원리상 동작(기존 HUD 버튼 wired + StandaloneInputModule이 Both와 호환)하나 **2-EventSystem 혼재로 클릭 라우팅 불안정 소지 + 런타임 버튼 구성 verbose + 클릭이 사람만 판정 가능**. **IMGUI는 `Event.current` 자체 처리라 EventSystem/입력모듈 완전 무관** → 2-EventSystem 영향 0, 직렬화 지뢰 0, XRCollab 검증됨. → 마찰 최소 IMGUI 채택.
- **SuppressWorldClick studio 적용:** `RulerHudUI.Update`가 매 프레임 `SimpleClickProvider.SetWorldClickSuppressed(this, 패널Rect.Contains(마우스))` — 커서가 패널 위면 클레임(클레임 기반, M3 동형). `Input.mousePosition`(하좌 원점) → GUI Rect(상좌 원점) **Y 뒤집기** 필수. 레거시 `Input`(Active Input=Both)로 studio에서 동작. OnDisable에서 클레임 해제.

**산출물:** `Assets/App/Scripts/ContentLogic/PromptScene/Content/Ruler/RulerHudUI.cs`(hot, 계약만 의존, 직렬 필드 0) + 씬 `===== UI =====/RulerHudUI` GameObject(3b 안전 — 직렬 필드 없음).

**검증 (§4, 사람 라이브 판정 PASS):** QuickTest(host, PromptSceneRoom_1) 진입 → 좌상단 IMGUI 패널 렌더(`_registry` 바인딩 확인) → **사용자가 화면에서 직접**: 룰러 ON/OFF 토글 / 바닥 2점 클릭→측정선+라벨 / 지우기→소멸 / HUD 버튼 클릭이 바닥 측정으로 안 샘(SuppressWorldClick) / 기존 UI(월드 메시지·PlayerList/Leave) 무손상 — **전부 "잘됨" 확인.** 에이전트 뒷받침: **예외/에러 0**(6분 구간), 기존 HUD 버튼 활성 유지, 측정 카운트가 지우기 반영. ⚠ 관측 주체 = 사용자 GUI(§5 베이스라인과 동형의 정직 표기).

**정직:** 증명 = **단일 에디터 host에서 사람이 UI로 Ruler 조작**(토글·측정·지우기·클릭 억제). 밖 = 2클라 UI 동기, VR 입력(가상 키보드), Phase 3 런치패드(아이콘 그리드) — **이건 "최소 HUD"지 런치패드 아님.**

### Ruler 크로스플랫폼 UI (World Space uGUI + XRI) + XR world-click (✅ 2026-07-23)

IMGUI(데스크톱 전용)를 **크로스플랫폼 World Space uGUI**로 교체(입력소스 독립). 절차·함정 SSOT = **[build-studio-room.md](build-studio-room.md) §5~§6**. 요지:
- **저작 객체 방식**(런타임 생성 아님, 사용자 지시): World Space Canvas + 버튼을 실제 씬 GameObject로 저작(`===== UI =====/RoomHud`), `RoomHudBinder`(hot)가 런타임 배선(LeaveButton 패턴 — 직렬 onClick→hot 메서드는 target=null). 캔버스=`GraphicRaycaster`(마우스)+**`TrackedDeviceGraphicRaycaster`**(XR). **빌보드 필수**(GraphicRaycaster ignoreReversedGraphics → 뒷면=mirror+클릭불가). 한글=**레거시 Text + 동적 OS 폰트**(studio 한글 TMP 자산 부재).
- **XR world-click(`XRWorldClicker` + `SimpleClickProvider.SubmitExternalRay`):** 컨트롤러/손 select 엣지 → UI 아니면 인터랙터 레이 월드 레이캐스트 → 마우스와 동일 핸들러 → Ruler 측정. 계약 §4.5 mechanism 추가(IInteraction 무변경). 인터랙터가 `{Left,Right} Hand/` 아래 공유 → **손도 동일 코드 커버**.
- **라이브 판정:** 데스크톱 마우스(사람 PASS) + **XR 컨트롤러 sim**(사람: UI 버튼 ON + 바닥 측정 PASS — deviceMode=Controller 전환). **손 sim = 불가**("Hand Actions not interactive" + poke 사거리 밖) → 실기기 V2. UnityXR 아바타는 studio 자연감지로 안 떠서(로더 미활성) **스포너 임시 강제 후 원복.**
- **패키지 정정(§3a 보강):** `com.unity.xr.hands`·`com.unity.xr.openxr`·`xr.management`·`xr.core-utils`가 **PackageCache에 전이 의존으로 존재**(manifest 명시 핀은 xr.interaction.toolkit 3.3.1+inputsystem뿐). §3a "openxr/xr.hands 없음"은 *manifest 명시 핀 기준* — 전이 resolve로는 존재하나 **XR 로더 미활성**이라 감지=desktop.
- **신규 문서:** [build-studio-room.md](build-studio-room.md) — studio 룸 조립·검증 재사용 절차(겪은 것만). ⚠ 배포(Smart Deploy)는 미경험이라 미포함(`build-studio-deploy.md` 후속).

