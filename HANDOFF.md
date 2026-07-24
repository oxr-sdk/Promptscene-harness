# PromptScene — 세션 핸드오프 (다음 Claude 대화용 진입점)

> **이 문서의 목적.** 새 Claude 세션이 이 프로젝트의 맥락을 빠르게 복원하도록,
> "지금까지 뭘 했고 / 지금 상태가 어떻고 / 다음에 뭘 할지"를 한곳에 모은 **상태 스냅샷 + 로드맵**이다.
> 깊은 내용은 여기 **복사하지 않는다** — `promptscene/docs/`의 원본을 가리킨다(SSOT). 낡지 않게 하려면
> 이 문서는 "포인터 + 상태"만 유지하고, 절차·규격 같은 지식은 항상 `promptscene/docs/`에 기입한다.
>
> **읽는 순서:** 이 문서(전체 지도) → [README.md](README.md)(설치/사용) →
> [promptscene/docs/ARCHITECTURE.md](promptscene/docs/ARCHITECTURE.md)(설계) → [promptscene/docs/promptscene-content-contract.md](promptscene/docs/promptscene-content-contract.md)(규격 SSOT).
> 작업 중 막히면 `oxr-docs-routing` 스킬의 라우팅표를 따른다(아래 §6).
>
> **최종 갱신:** 2026-07-24.

---

## 1. 프로젝트 한 줄 정의

**XRCollabDemo(멀티유저 XR Unity 앱)에서 자연어 프롬프트로 룸(작동 토대 + 선택 기능)을 합성하고, 구조를
자동 검증한다.** 산출물은 두 갈래다: ① Unity 런타임 코드(얇은 코어 + 옵트인 콘텐츠 모듈), ② 그걸 조립·검증하는
Claude Code 플러그인(이 레포 = `promptscene-harness`).

> **신규 병행 트랙(2026-07):** 위 ①런타임 코드를 **XumFlow studio**(별도 콘텐츠 저작 프로젝트 — hot-update/Addressables 모델로 XRCollabDemo와 상이)로 이식 착수. Core/Content는 `App.HotUpdate`(`ContentLogic/PromptScene/`)에 들어감. 지형 §2 · 현황 §4 · 다음 §8 · 상세 SSOT [promptscene/docs/xumflow-migration.md](promptscene/docs/xumflow-migration.md).

## 2. 레포·프로젝트 지형 (헷갈리기 쉬움)

| 것 | 위치 | 정체 | 비고 |
|---|---|---|---|
| **이 레포 = 로컬 마켓플레이스** | `c:\J_0` | `.claude-plugin/marketplace.json` 루트 + 타깃 Unity 앱(XRCollabDemo) 동거 | git `main`. **여기서 세션을 열어야** 플러그인이 로드됨(→ §7 함정). |
| **promptscene 플러그인** | `c:\J_0\promptscene` | 플러그인 본체(plugin.json/skills 4종/hooks+회귀 러너). install EBUSY 회피 위해 레포 루트에서 분리 | marketplace.json은 루트 `.claude-plugin/`에 잔류. 로드 방식은 §9 |
| **XRCollabDemo** | `c:\J_0\XRCollabDemo` | 타깃 Unity 6 앱(`6000.3.11f1`), MCP 포트 **27826** | 런타임 코드가 사는 곳: `Assets/PromptScene/`. `Library/PackageCache`는 **읽기 전용**. |
| **DeepChairProject** | `C:\Unity\DeepChairProject` | 기능 **레퍼런스** 소스(ruler/laser/memo 등), Unity `6000.1.7f1`, MCP **22863** | 앱레이어가 XRCollabDemo에 없어 **lift-and-shift 불가 → 계약 위에 클린 재구현**이 정석. |
| **XumFlow (studio)** | `c:\J_0\XumFlow-studio` | 포트 **타깃** 후보(콘텐츠 저작 프로젝트), studio 브랜치 `@7ccd554`, Unity `6000.3.11f1`, MCP `ai-game-developer`@**21017** | gitignore `/XumFlow-studio/`. 환경 살아있음 실증(오픈·컴파일·§5 스폰). SSOT: [promptscene/docs/xumflow-migration.md](promptscene/docs/xumflow-migration.md) §7 |
| **XumFlow (runtime)** | `c:\J_0\XumFlow` | 다운로더/플레이어 빌드(runtime 브랜치 `@200a4a2`) | 대조·인용용 보존. gitignore `/XumFlow/` |

> **XumFlow studio 트랙 상태(2026-07-23):** studio 클론 + 선행조건(codebook/XREAL 회수) + 에디터 오픈·컴파일 클린 + **§5 베이스라인 스폰 PASS(T_RoomB, 사용자 GUI)** + 우리 코드 자리 확정(`ContentLogic/App.HotUpdate.asmdef` = baked 경계) + **MCP 0.66.0 배선(Connected)**. **MCP 0.66.0 제어 증명 완료(2026-07-23 재시작 후 세션):** §5를 MCP로 재현 — `script-execute`로 play mode 진입→T_RoomB 로드→`Desktop(Clone)` 스폰+모션rig→exit. + **port-prep 3결정 확정**(Core=`ContentLogic/PromptScene`·별도 asmdef 불요 / 직렬화 지뢰 회피=프리팹 기본컴포넌트+씬임베드·런타임배선·데이터컨테이너 App.Bridges / 스킬 Smart-Deploy 재작성 스코프). **다음=Ruler 클린 재저작.** **핵심 규칙/함정(SSOT=xumflow-migration.md §7):** ①MCP 버전 ∝ 프로젝트 UXM 버전(studio UXM 1.8.5→MCP 0.66.0 / XRCollab UXM 1.8.1→0.76.3, 서버·포트 분리) ②MCP NuGet DLL은 UPM 비관리 → 버전 변경 시 클린 재설치(폴더 삭제 후 0.66.0 자동복원, 다운로드 중 과도기 CS 에러=행 아님) ③씬 로드 키=leaf 폴백 가능·`hostMode=true`라야 단일 에디터 아바타 스폰 ④XRCollab UXM URL 언핀 = 잠재 취약(재resolve 시 어댑터-MCP 충돌 재발). 길 A(구버전 MCP, UXM 무수정) 채택 — UXM 담당자에 versionDefine 완화 요청됨, 상류 수정 오면 최신 MCP 이전 재검토.

- **런타임 코드 루트:** `XRCollabDemo\Assets\PromptScene\` — `Core\`(namespace `PromptScene.Core`: Contracts / RoomContentRegistry / SimpleClickProvider / RoomCore) + `Content\`(FEATURE 모듈: `Ruler\RulerContent.cs`, `ClickSpawner\ClickSpawnerContent.cs`).
- **규격 SSOT:** [promptscene/docs/promptscene-content-contract.md](promptscene/docs/promptscene-content-contract.md) — 계약 인터페이스·씬 계층·불변식 C1~C4·검증 하네스·로드맵.

## 3. 아키텍처 핵심 (한 문단)

씬을 **SYSTEMS(작동 토대: 네트워크·세션·입장·스폰·코어 레지스트리)** 와 **FEATURES(그 위에 얹는 옵트인 콘텐츠·연출)** 로 분리한다.
의존성은 **FEATURE → SYSTEMS 한 방향**뿐 — 코어는 어떤 기능이 얹히는지 컴파일타임에 모르고, 각 FEATURE가 런타임에
`RoomCore.Instance` 레지스트리에 **자기등록**한다(UE5 Modular Game Features와 동일 원칙). 그래서 자연어 입력이 바뀌면
FEATURES 층만 바뀌고, 토대는 검증된 절차로 얼려 스킬화할 수 있다. 씬 계층 표준:
`SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC`. — 자세히: [promptscene/docs/ARCHITECTURE.md](promptscene/docs/ARCHITECTURE.md).

## 4. 현재 상태 (로드맵)

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 룸 베이스(구조+아바타+UI 전환, 불변식 C1~C4) | ✅ |
| 1 | 계약 규격 | ✅ |
| 2 | Ruler를 계약 위에 클린 재구현(파일럿) | ✅ |
| 2.5 | 씬 계층 표준화 | ✅ |
| 3 | 런치패드 UI(registry→아이콘 그리드→SetEnabled) | ⬜ **보류/롤백** — 회고 [promptscene/docs/promptscene-launchpad-attempt.md](promptscene/docs/promptscene-launchpad-attempt.md). 단, 최소 플레이 HUD(`RulerHudUI`, registry→토글+클리어, IMGUI)는 2026-07-15 추가·검증(아이콘 그리드 런치패드는 아님) — §5c·build-desktop-client §9 |
| 4 | `/scaffold-content` 스킬화 + LLM 신규 기능 생성 템플릿 | ✅ **라이브 검증(2026-07-09)**, 샘플 `ClickSpawnerContent`로 §5+§6.5 PASS |
| 5 | `/compose-room` 합성 스킬 + 합성 하네스 | ✅ **라이브 검증(2026-07-13)**, `ComposedRoom_1`(Ruler)로 §5+§6.5 PASS. **N=3 실증(2026-07-20 M4)**: `ComposedRoom_2`(Chat+Ruler+Grabbable, 슬래시 발동) §5×3+§6.5 PASS |
| D2 | COMPOSITIONS 층(게임모드) — 이벤트 버스 + 파일럿 FEATURE 2종 + 첫 COMPOSITION | ✅ **라이브 검증(2026-07-21)**: `IEventBus` 추가(IRoomCore 무변경) / TargetProps·ScoreHud(상호참조 0) / TargetShootoutMatch+MatchView. `ShootoutRoom_1`에서 단일 클라 서버권위 루프(집계→승자→리셋) + **2클라 점수 동기 파리티** + 버스 스모크 전부 PASS(§5). 예측 견적 [prediction-survey.md](promptscene/docs/prediction-survey.md) |

| V1 | 던지기(다트) — 비경합 투사체(오너 유지 비행·NT 전파·명중→서버권위 점수), 던지기=재조합 | ✅ **라이브 검증(2026-07-22)**: `ShootoutRoom_1`에서 2클라 데스크톱 — A 3/3 명중, B가 비행·오너·점수 동기 관측. 마젠타 교정 + 셰이더 검사. XR 입력경로는 소스검증(라이브 XR/실기기 = V2). [capability-map.md](promptscene/docs/capability-map.md), build-desktop-client §13 |
| XumFlow studio 이식 | studio(콘텐츠 저작 프로젝트)로 런타임 코드 포트 — 환경 확보 + MCP + **FEATURE 이식** | ✅ **관통(§9) + Chat(§10) + GrabbableProps XRI 그랩(§11) — 2026-07-23**: 환경/MCP 0.66.0 확보 후 studio 샘플룸(=PromptSceneRoom_1) FEATURES = **Ruler·Chat·GrabbableProps 3종 공존**, 전부 §5 단일 host MCP 라이브 PASS(자기등록·Meta·SetEnabled 멱등·네트워크 스폰+RPC·아바타 무손상·Error/Exception 0). Core/Content=`ContentLogic/PromptScene/` verbatim 이식(§2 API 차이 0). **§11에서 ⭐XRI 3b 경계 규칙 확정**(base 어셈블리 XR Grab Interactable/Rigidbody=프리팹 직접 직렬화 OK[NetCube 선례+실측] / hot 뷰=직렬 필드 0 런타임 배선) = 이후 모든 XRI FEATURE SSOT. 그랩=XRCollab 클릭 아닌 **XRI selectEntered→XumView.RequestOwnership**. **⭐+D2 점수게임 Loop 1~3 완주 = 5층 전체(COMPOSITIONS 포함) studio 성립(2026-07-24, §12~§14)** — TargetProps(4과녁 네트워크 스폰·명중→TargetHitEvent)+ScoreHud(구독→표시, 상호참조 0)+**TargetShootoutMatch COMPOSITION**(studio 첫 COMPOSITIONS 층+MatchView C1). **§5 서버권위 루프 실증**(클릭 명중→집계 1→2→3→승자→리셋, 실 흐름) + ScoreHud 실 수신 + 6콘텐츠 공존 + Error 0. IEventBus 상속(계약 무수정), 신규 플랫폼 API 0, FEATURE↔FEATURE 참조 0. **PromptScene 아키텍처 완전 증명.** **⭐ `/assemble-room` studio판 재작성 완료 + 자기검증 PASS(2026-07-24, §15):** 골격 전담 스킬(샘플룸 복제→Content Manager 등록→**5층 골격**[SYSTEMS+RoomCore+스포너/ENVIRONMENT/UI/빈 FEATURES/빈 COMPOSITIONS]→SceneId 안전 재부모→QuickTest §6.5)로 새 룸 `AssembleRoom`(기본 베이스 `T_RoomA`=Capsule 없음) 생성 → §6.5 PASS(5헤더·Capsule 0·아바타 스폰 IsOwner=True·RoomCore 4서비스·빈 레지스트리·SceneId 보존·Error 0). FEATURES+COMPOSITIONS는 빈 층 폴더까지만=경계 준수(콘텐츠는 add-component 몫). **⭐ `/add-component` 스킬+에이전트 완성 + 자기검증 PASS(2026-07-24, §16):** 골격 채움 전담(상담/견적→룸→컴포넌트 확보→층 배치+§3b 배선→§5+§6.5 QuickTest)·회고A(6루프 공통/차이)로 EXECUTE 동결·회고B(scaffold-content studio 역할 **흡수** 결정). 자기검증=`/assemble-room AddCompProof_1`→`/add-component 룰러`→**§5/§6.5 FEATURE PASS**(자기등록 ruler·SetEnabled 무예외·Meta 룰러/측정·아바타 스폰·Error 0), 손작업 Ruler와 동형. **다음=①`/compose-room` studio판 재작성(assemble-room+add-component 조율)·②2클라 파리티(MPPM §11.6)·③DartProps→다트 연결.** SSOT [xumflow-migration.md](promptscene/docs/xumflow-migration.md) **§9~§16**·§7·§8 |

**스킬 5종(현재 동작):**
- `/promptscene:assemble-room <RoomName>` — **studio판(§15, 골격 전담):** 샘플룸 복제(기본 `T_RoomA`, Capsule 없음) + Content Manager 등록 + 5층 골격(SYSTEMS+RoomCore+스포너/ENV/UI/빈 FEATURES/빈 COMPOSITIONS) + SceneId 안전 재부모 + QuickTest §6.5 라이브 증명. 콘텐츠(FEATURE/COMPOSITION)는 안 넣음(add-component 몫). 기본 룸명=AssembleRoom. (구 XRCollab판=서버 재빌드/조인은 git 이력.)
- `/promptscene:add-component <컴포넌트 요청> [on <Room>]` — **studio판(§16, 골격 채움 + 상담):** 의도의 FEATURE/COMPOSITION을 룸에 얹고 §5+§6.5 QuickTest 자기증명. Phase 0 상담/견적(FEATURE↔COMPOSITION 분류·capability-map 재조합✅/개척⛔·oxr-docs-routing) → 룸(없으면 `/assemble-room` 참조) → 컴포넌트 확보(재사용/AI생성/사람작성) → 층 배치+§3b 배선(+새 네트워크 프리팹이면 C1) → §5 검증 → (옵션) `/cross-platform-ui`. **scaffold-content의 studio 역할을 흡수**(회고 B). 부품(assemble-room/cross-platform-ui/oxr-docs-routing) 참조 호출, 절차 복제 금지. 에이전트 `.claude/agents/add-component.md`(⚠ 세션 레지스트리 트랩 = 다음 세션부터 subagent 호출 가능).
- `/promptscene:cross-platform-ui <PC|PCSS|PCXR|Cross> [on <Room>]` — **studio판:** 레지스트리 순회 World Space uGUI HUD(토글형 FEATURE마다 버튼) + XR world-click(`XRWorldClicker`+`SubmitExternalRay`) + QuickTest 증명. add-component §6이 부품으로 호출.
- `/promptscene:scaffold-content <설명>` — (XRCollab 트랙) 프롬프트→FEATURE 모듈 생성(동결된 Ruler 템플릿) + Master/Room.exe로 §5 라이브 검증. **studio는 add-component가 흡수**(§16 회고 B) — studio판 미제작.
- `/promptscene:compose-room <자연어 룸 요청>` — 오케스트레이터: 자연어→기능 선택(카탈로그 매칭 + `composition-plan.json` 박제)→assemble-room·scaffold-content **참조 호출**로 N기능 룸 조립→§6.5 SYSTEMS + §5 FEATURES(기능마다) 자동 판정. 조립 절차는 하위 스킬을 참조(복제 금지).
- `/promptscene:deploy-client [Meta|XReal|Tablet|Vision]` — 디바이스 프리셋 적용 + 룸 씬 번들 + 마스터 IP 베이크 + 빌드/adb 배포·검증.

**정직 계약:** 하네스는 **§5 구조/계약 적합성만** 증명한다. 기능의 실제 동작·미감은 **비검증**(사람/비전 루프 필요).

## 5. 직전 세션(2026-07-22 — V1 던지기(다트); ✅ 2클라 라이브 실증 — 비행 전파·오너 유지·명중→점수 동기, 던지기=재조합)

- **동기·경계:** D2 점수전 위에 **던지기(비경합 투사체)**를 얹음. 판정: **검증 기계 재조합** — 던진 사람이 비행 내내 오너(Takeover, 비반납) + client-auth NT 궤적 전파 + 명중→이벤트→서버권위 점수. **뺏기 경합 전까지 D4-2(예측) 불요**([capability-map.md](promptscene/docs/capability-map.md) 첫 조각, 경계선 명시).
- **V1-1 DartProps FEATURE:** `Content/DartProps/`(`DartProps.cs` Core-only 스폰 FEATURE + `DartView.cs` 프리팹 내부 NetworkBehaviour + `DartHitEvent.cs`) + `Dart.prefab`(GrabbableProp 골격 + Rigidbody + XRGrabInteractable(throwOnDetach) + DartView, **C1** DPO index 17). **입력경로=XRGrabInteractable 단일**(마우스 임시코드 0), 데스크톱 검증은 `ThrowLocal(velocity)` 주입점. **FEATURE↔FEATURE 참조 0 유지** — 다트는 자체 `DartHitEvent`(TargetProps 무참조) 발행, "과녁인가?" 판정은 COMPOSITION이(`TargetShootoutMatch`에 `Subscribe<DartHitEvent>` 1줄 + TargetMarker 체크 → 클릭과 동일 `ReportHit`). **버스 배당금: 한 점수 루프, 두 소스(클릭/다트)**. ShootoutRoom_1 편입 + Room.exe/Client.exe 재빌드.
- **V1a 라이브 판정(2클라 데스크톱, PASS):** A(myId=0) 다트3 스폰(대기 중 안 떨어짐)→**3/3 명중**→서버권위 P0:1→2→3→**승자 P0**→리셋. B(myId=1, 한 발도 안 쏨) **①비행 궤적 에폭 관측(공중 포착)** ②**Owner=A 유지**(owner=0/mine=False 내내) ③**점수 양측 동기**(MatchView 방송 수신, P0:1→3→승자→리셋) ④위치 교차일치. ⚠️ 2데스크톱 게스트 동시조인은 트랩 J로 불안정(1회는 signedIn 고착 실패, 재기동 후 통과) — 결함 아닌 인프라.
- **V1c 마젠타 교정:** Target/Dart URP/Lit(빨강/노랑) + **"에러 셰이더 0" 검사 스크립트**(`Harness/Editor/ShaderSanityCheck.cs`, 메뉴 노출) PASS — 실기기 전 필수 게이트.
- **V1b XR 입력경로(소스 검증):** `throwOnDetach` velocity는 attach/targetPose 프레임델타 평활 → **실기기/시뮬 동일 코드경로**. 발견·수정: `Detach()`가 **kinematic RB를 안 던짐** → 다트 정지시 kinematic이라 XR 발사 실패 가능 → 그랩/릴리즈에서 non-kinematic 강제(데스크톱 경로 무관). **라이브 XR 스윙은 미실행 = V2.**
- **정직 계약:** V1 증명 범위 = 데스크톱 2클라 **물리·비행 전파·소유권·명중→점수 동기**(구조/동작). **밖(=V2, 판정자는 사용자):** 손맛·실기기 함정(build-meta-client §2.4-D)·크로스플랫폼(HMD+데스크톱 동시)·경합 뺏기(D4-2). 절차·판정·V2 준비물 SSOT: [build-desktop-client.md](promptscene/docs/build-desktop-client.md) §13.

## 5a. 그 직전 세션(2026-07-21 — D2 COMPOSITIONS 층 증축 + 예측 정찰; ✅ 라이브 게임 루프 실증 완료 — 단일 클라 서버권위 루프 + 2클라 점수 동기)

- **동기:** 게임 루프 요청(과녁 맞추기 점수전)으로 **"서로 통신해야 하는 기능 2개"** 수요 발생 → design-directions **D2(COMPOSITIONS 층)** 착수 조건 충족. 전제(직교성)를 깨지 않고 조율자 층을 위에 얹는 원안 그대로 구현.
- **D2-1 계약 추가 (딱 하나):** 인프로세스 타입드 이벤트 버스 `IEventBus`(Publish/Subscribe/Unsubscribe)를 `Contracts.cs`에 additive로 추가. **`IRoomCore` 인터페이스 무변경** — 내장 서비스로 등록(`RoomCore.Awake`)하고 `TryGet<IEventBus>`로 조회(v0.2 "서비스 추가해도 코어 무변경" 원칙 준수). 메커니즘-비정책(§4.5): **인프로세스 전용, 네트워크 전송 아님** — 복제는 여전히 프리팹 RPC. 구현은 예외 격리 + (T,handler) 멱등.
- **D2-2 파일럿 FEATURE 2종 (서로 모름이 핵심):** `TargetProps`(과녁 스폰→클릭 명중 시 `TargetHitEvent` 발행까지만, 점수 모름) + `ScoreHud`(`ScoreChangedEvent` 구독·IMGUI 표시만, 과녁 모름). **두 소스 상호 타입 참조 0 (grep 검증)** + 각자 `PromptScene.Core`만 의존 — 이 층의 §5급 신규 검증 신호. 위치: `Assets/PromptScene/Content/TargetProps/`, `.../ScoreHud/`.
- **D2-3 첫 COMPOSITION:** `TargetShootoutMatch`(+ 네트워크 권위 프리팹 `MatchView`, **ChatChannelView 동형** — `ServerRpc(RequireOwnership=false)` 상행 + `ObserversRpc` 하행, 발신자=서버 주입 ClientId, 신규 플랫폼 API 0). TargetHit 구독 → **서버 권위** 집계 → 선취 N점(기본 3) 승자 공지(자체 IMGUI 배너 + ScoreChangedEvent MatchOver; Chat 부재 시에도 무해 — `Contents.GetById("chat")` 런타임 조회로만) → resetDelay 후 리셋·재판. 위치: `Assets/PromptScene/Compositions/TargetShootoutMatch/`.
- **P-G0 예측 정찰 (완료, 읽기 전용):** [prediction-survey.md](promptscene/docs/prediction-survey.md). scout가 소스에서 수집 + 메인이 하중 주장 재검증. 판정 — FishNet 4.6.12 예측은 **XumNet 통과해 직접 도달 가능**(XumNet Runtime .cs에 예측 코드 0건, XumView는 NetworkBehaviour 직상속·예측 파이프라인 무개입). 단 **NetworkManager에 PredictionManager + 프리팹별 `_enablePrediction`**을 요구해 M-시리즈의 "SYSTEMS 무수정"을 깬다 → D4-2 "대공사, 보류"의 이유가 *SYSTEMS 해동+검증 인프라 비용*임이 확증. 지연/손실 시뮬 = FishNet 내장 `LatencySimulator` 존재.
- **D2-4 라이브 실증 (✅ 완료, 후속 세션에서 MCP 연결 후 수행):** 프리팹 2종(Target/MatchView) FishNet auto-populate로 DefaultPrefabObjects C1 등록(16→18) → `ShootoutRoom_1` 조립(C1/C3=True, spawner SceneId 할당, `===== COMPOSITIONS =====` 층 포함, TargetProps.targetPrefab·TargetShootoutMatch.matchPrefab 직렬 배선) → Room.exe 재빌드(room.log `Online Scene: ShootoutRoom_1`) → 라이브 판정:
  - **§6.5 (에디터 클라):** 로비 소멸(Client 언로드)·`ShootoutRoom_1` 로드·아바타 Desktop(Clone) IsOwner=True·RoomCore 4서비스(IEventBus 포함) 등록·TargetProps/ScoreHud 자기등록+SetEnabled 무예외·MatchView 스폰(1개)·Target 4개 스폰 — 전부 PASS.
  - **단일 클라 서버권위 루프:** 명중(TargetHitEvent 버스)→COMPOSITION→MatchView.ReportHit(ServerRpc, 발신자=서버주입 ClientId)→**서버 집계 1→2→3**→ObserversRpc 방송→ScoreChanged 버스→ScoreHud 표시. 선취 3점 **승자 P0 공지**(over=True)→resetDelay 후 **리셋(빈 보드)→재판**. 방송 레코더 로그로 전 전이 캡처.
  - **2클라 점수 동기 파리티:** 에디터 A(clientId 1) 명중 → **별도 데스크톱 프로세스 B**(clientId 0, 한 발도 안 쏨, Player 서브타깃 창모드=Renderer≠Null)가 `clientB.log`에 **동일 서버권위 스코어보드** 수신(P1 1→2→3, over=True **winner=P1** 일치, 리셋). B의 COMPOSITION이 Chat 부재를 `Contents.GetById("chat")` 런타임 조회로 감지해 자체표시 폴백(무해). 상호가시(avatars=2, own1/remote1), 공유 MatchView 1개(재사용, 중복 없음).
  - **버스 런타임 스모크:** IEventBus 전달·(T,handler)멱등·예외격리·해지·빈발행 안전 전부 PASS.
  - 증거: [screenshots/d2-shootout-scoreboard.png](promptscene/docs/screenshots/d2-shootout-scoreboard.png)(단일 P0 2/3), [d2-shootout-2client.png](promptscene/docs/screenshots/d2-shootout-2client.png)(2클라 A뷰 P1 2/3), [d2-shootout-clientB-parity.txt](promptscene/docs/screenshots/d2-shootout-clientB-parity.txt)(B 방송 로그), [d2-shootout-broadcasts.txt](promptscene/docs/screenshots/d2-shootout-broadcasts.txt). 절차 SSOT: [build-desktop-client.md](promptscene/docs/build-desktop-client.md) §12.
- **정직 범위 (중요):** 증명된 것 = **구조 불변식(FEATURE↔FEATURE 참조 0) + 버스 런타임 + §6.5 + 서버권위 집계 + 2클라 점수 동기·승자·리셋.** **밖(비검증):** 게임의 "재미"/밸런스, 실제 마우스 클릭 레이캐스트→명중 판정(주입은 버스 경계 TargetHitEvent에서 — 클릭 감지는 TargetProps 내부 로직이라 별도), 3인+, VR 입력, Target 프리팹 머티리얼(URP 셰이더 미해결로 마젠타 — 콜라이더/NetworkObject는 정상, count=4). compose-room은 아직 COMPOSITION을 모름 — 편입은 후속(§8).

## 5b. 그 이전 세션(2026-07-20 — M4 다기능 합성 피날레, 한 줄 → 3기능 룸)

- **M4 관통 (§8-3 "다기능 합성 실증" + "슬래시 표면 발동" 닫힘):** `/promptscene:compose-room 채팅으로 다른 참가자와 소통하고, 측정한 내용을 서로 볼 수 있고, 물건을 잡아서 옮길 수 있는 룸 만들어줘` — **슬래시 표면으로 발동**(절차 직접 수행 아님). RESOLVE가 소스 카탈로그 4종에서 **Chat(소통)+Ruler(측정)+GrabbableProps(물체) 3종 선택, ClickSpawner 비선택**(요청에 대응 능력 없음 — 카탈로그 매칭이 실제로 변별한다는 음성 증거, plan의 `notSelected`에 박제) → `ComposedRoom_2` 조립(`C1/C3/RoomCore/기능3종=True, sceneIdsGenerated=1`) → Room.exe 재빌드 → **§6.5 SYSTEMS 4신호 + §5 FEATURES 3종 전부 PASS**(`=== §5 COMPOSITION VERDICT: PASS (3 features) ===`, room.log `Client 0 has become a player`). plan: [composition-plan.json](promptscene/skills/compose-room/composition-plan.json).
- **빌더 갭 1건 발견·오케스트레이션으로 해소(역기입):** `build_composed_room.cs`는 기능 컴포넌트를 AddComponent까지만 하고 **직렬화 프리팹 필드는 배선하지 않는다**(ComposedRoom_1의 룰러도 M1 때 수동 배선했던 것). §5는 프리팹 없이도 통과하지만(우아한 경고) 실동작엔 필요 → EXECUTE에 후처리(채팅 채널/측정/잡기 프리팹 3종 배선+씬 재저장)를 넣고 compose-room SKILL.md Stage 4에 절차로 기입.
- **2클라 데모(증명 아님 — 시연):** 신뢰 토폴로지(에디터 A=clientId 1 + 데스크톱 exe B=clientId 2, Client.exe를 ComposedRoom_2 번들로 재빌드, cfg localhost). 한 세션에서 ①채팅 한 마디(B `RECV from=1`) ②측정 공유(3.28 m 선) ③프롭 핸드오버 왕복(B 로그 ownerId 1→2→1, 위치 전파 동반) 전부 관측. **피날레 스크린샷**(한 화면에 두 아바타+채팅 패널+측정선+프롭): [m4-composed-room-finale.png](promptscene/docs/screenshots/m4-composed-room-finale.png).
- **F0 회귀:** SuppressWorldClick 클레임화 — 기계 확인(OR-의미론 5케이스) + 라이브 확인(제3 claimant가 RulerHUD·채팅 패널의 매 프레임 쓰기에도 생존, 해제 후 잔류 0 — 구 bool이면 소멸했을 시나리오) 모두 PASS.
- **정직 계약:** 하네스 증명 범위는 **구조/계약 적합성**(§6.5+§5×3)까지. 데모의 기능 동작(채팅/측정/핸드오버)은 **시연이지 하네스 증명 아님**. `params` 실전달, `mode:"extend"`, MutuallyExclusive 충돌 케이스는 **여전히 미실증**(§8-3 잔존).

## 5c. 그 이전 세션(2026-07-20 — M3 채팅 닫힘, 원래 요청 3종 완주)

- **M3 실증 (룰러 공유·그랩·채팅 시리즈의 마지막 조각):** 새 FEATURE `ChatContent`(런타임 `Assets/PromptScene/Content/Chat/`)로 2클라 텍스트 채팅 양방향을 라이브 판정. **4신호 전부 PASS**(①A 발신 5건→B 수신, 내용+발신자 일치 ②B 회신 2건→A 수신 ③연속 5건 순서 보존 ④발신자 표시 양측 교차 일치 — 양측 모두 A건=P0/B건=P2). 검증된 두 기계의 조합만 사용(`ServerRpc(RequireOwnership=false)` 상행=M2 동형 + `ObserversRpc` 하행=M1 동형) — 신규 플랫폼 API 0. **계약(Contracts.cs) 무수정**, 프리팹 `ChatChannel`(NetworkObject+`ChatChannelView`) C1 자동 등록 + Room.exe/Client.exe 재빌드. 발신자는 **서버가 주입한 ClientId**(`NetworkConnection sender = null` 패턴, 위조 불가) — 닉네임 시스템 발명 안 함. 절차·판정 SSOT: [build-desktop-client.md](promptscene/docs/build-desktop-client.md) §11, 스크린샷 [m3-chat-two-clients.png](promptscene/docs/screenshots/m3-chat-two-clients.png).
- **신규 설계 지점 1개 — 채널 스폰-또는-재사용:** SetEnabled 시 씬에 채널이 없으면 `core.Net.Spawn`, 있으면 재사용(2클라가 각자 스폰해 2채널 되는 것 방지). 재시도 루프가 트랩 I를 흡수, 동시-인에이블 레이스는 수신 전-채널 집계(static Log)로 무해화. 실측: B(objId 38402 재사용, 양측 내내 1채널). SetEnabled(false)=패널 숨김만(채널은 타 클라용 존치).
- **Core 메커니즘 일반화 1건(정직 기록):** `SimpleClickProvider.SuppressWorldClick`을 단일 writable bool→**클레임 기반**으로 교체(패널 2개가 되면 마지막-쓰기-승리로 억제가 조용히 깨지는 잠재 버그 수정). SYSTEMS 파일 1개 수정이지만 메커니즘-비정책(M1이 이 훅을 추가했던 성격과 동일), 계약 무수정.
- **승격 검토서 작성(승격은 안 함):** FEATURE 내부 네트워크 사용 3건(M1 상태전파/M2 소유권/M3 방송 버스)의 모양 비교 → **`INetMessaging` 승격 보류** 권고(순수 버스 수요는 채팅 1건, 전달 의미론 상충 — BufferLast가 Ruler엔 필수/채팅엔 유해). 근거·트리거: [net-messaging-promotion-review.md](promptscene/docs/net-messaging-promotion-review.md).
- **함정(신규, 트랩 K):** 콜드 스타트 exe의 방 조인이 액세스 토큰 만료(AccessTimeoutPeriod 10s < 첫 실행 씬 로드)로 실패(`Room could not validate you`) → 재기동(웜 스타트)으로 통과. build-desktop-client §8 표 K.
- **정직 범위:** 텍스트 채팅 양방향, 2인, 데스크톱, 백필 없음(늦게 조인하면 과거 메시지 안 보임 — 패널에 명시)까지 증명. **밖:** 이력 동기화, 3인+, VR 입력(가상 키보드), 귓속말/채널 분리, 입력 중 WASD 억제.

## 5d. 그 이전 세션(2026-07-16 — M2 잡기 기반 소유권, D4-1 닫힘)

- **M2 실증 (D4 1단계 "잡기 기반 소유권" 닫힘):** 새 FEATURE `GrabbableProps`(런타임 `Assets/PromptScene/Content/GrabbableProps/`)로 2클라 잡기→이동→놓기→**핸드오버(Owner A→B→A)**를 라이브 판정. **5신호 전부 PASS**(①A잡기 Owner=A 양측 ②A놓기 위치 전파+Owner 유지=비반납 ③B탈취 Owner=B ④B놓기 위치 전파 ⑤A재탈취 Owner=A, 공유 objId 양측 교차 일치). **SYSTEMS·계약 무수정** — 프리팹 `GrabbableProp`(NetworkObject+`XumView` Takeover+client-auth `NetworkTransform`+`GrabbableView`)의 뷰가 SDK `XumView.RequestOwnership`를 직접 사용(M1 `RulerMeasurementView`와 동형). C1: `DefaultPrefabObjects` 자동 등록 + Room.exe 재빌드. 검증 SSOT: [grab-ownership-survey.md](promptscene/docs/grab-ownership-survey.md) §실증.
- **하네스 확장:** `AutoJoinClient`에 그랩 안무 추가(`-psGrabTest`/`-psGrabRole A|B`/`-psGrabEpoch` — 공유 에폭 타임라인). 런처 `Builds/App/play-grabtest.ps1`(서버+2클라, 직렬화 조인).
- **핵심 함정(신규, 아래 §7):** ①클라 런타임 스폰은 `IsClientStarted` 뒤에(ClientId 유효만으로 부족 → 스폰 RPC 드롭) ②2 데스크톱 게스트 동시 조인 MST flakiness(2번째 게스트 sign-in/validation 미완 + 서버 2nd-connect NRE) — 그랩 결함 아님, 인프라. 신뢰 토폴로지는 에디터+1데스크톱.
- **정직 범위:** 클릭 잡기·놓기·Takeover 핸드오버, 2인, 데스크톱, 비반납 모델까지 증명. **밖(알려진 한계):** VR 컨트롤러 그랩, 오너 이탈 중 잡힘 상태(NT 송신자 공백), 3인 동시 경합, 잡는 동안 원격 보간 품질, 예측(D4-2). 2클라 핸드오버는 **1회 클린 통과로 5신호 증명**했고 재현은 MST 게스트 flakiness로 불안정(1클라 스모크는 안정 재현).

## 5e. 그 이전 세션(2026-07-14~15, 멀티플레이 실증 + Ruler 결과값 공유)

- **M0 멀티플레이 실증 (프론티어 "멀티플레이 실증" 닫힘):** 에디터 클라 + **빌드된 Windows 데스크톱 클라**(2번째 클라, ParrelSync 없이) 2인을 localhost로 붙여 §6.5 확장 신호(①자기 IsOwner=True ②원격 Clone IsOwner=False ③위치 전파)를 **양측 시점**(에디터=리플렉션, exe=`-logFile`)에서 라이브 판정. 두 아바타 ObjId 교차 일치. 실체: arg 게이트 자동조인 하네스 `Assets/PromptScene/Harness/AutoJoinClient.cs`. 절차·함정 신규 문서 [build-desktop-client.md](promptscene/docs/build-desktop-client.md).
- **M1 Ruler 결과값 공유 (D4 왼쪽 열 실증):** RoomCore의 `INetSpawn`을 **FishNet 백엔드 `FishNetSpawn`**으로 실체화(계약 §4.5 메커니즘 승격 — SYSTEMS 해동 아님) + Ruler 측정을 `RulerMeasurement` 프리팹(NetworkObject + FEATURE-내부 `RulerMeasurementView`)으로 네트워크 스폰. 끝점은 `[ObserversRpc(BufferLast)]`로 전파(INetSpawn.Spawn이 못 나르는 per-object 데이터). **생성·제거 양방향 전파** 확인. RulerContent는 여전히 Core만 참조.
- **핵심 함정(문서 역기입됨, build-desktop-client §8):** ①서버 빌드가 `standaloneBuildSubtarget=Server`를 남겨 클라가 헤드리스(창 없음)로 나옴 → 클라 빌드 전 **Player 복구** ②하네스가 방 입장 시 파괴(C3 언로드) → `DontDestroyOnLoad` ③에디터 클라 활성 씬이 룸 씬이면 포트 7777 충돌 → **Client.unity 활성** ④실행 중 서버 exe 재빌드 불가(파일 락) → 종료 후 재빌드 ⑤빌드 완료 판정은 exe mtime 아님(`_Data/level0`·cfg) ⑥긴 블로킹 빌드/타깃 전환이 MCP 브리지 끊음 → 에디터 재시작 ⑦서버 재빌드는 cfg를 LAN IP로 되돌림 → localhost 테스트면 매번 교정.
- (설계 결정: M1은 사용자 선택으로 **RoomCore INetSpawn 실체화** 경로. 계약 주석의 "Despawn 매핑은 RoomCore 구현 참조"와 부합.)
- **수동 플레이 HUD(사람이 직접 몰기용):** 인게임 IMGUI 패널 `RulerHudUI`(룸 UI에 심음) — 레지스트리 순회로 콘텐츠별 ON/OFF 토글 + 측정 지우기 + 공유 카운트. `SimpleClickProvider`에 `SuppressWorldClick` 훅(HUD 클릭이 바닥 측정으로 새는 것 방지). `activeInputHandler=2(Both)`라 레거시 Input 클릭 동작. **클릭→네트워크 측정 스폰→공유** 라이브 검증. 런처 `Builds/App/play-2clients.ps1`. ⚠️ 이건 **Phase 3 런치패드(아이콘 그리드)가 아니라** "플레이 가능하게 하는 최소 UI" — 런치패드는 여전히 보류(§4). 절차·검증: build-desktop-client §9.

## 5f. 그 이전 세션(2026-07-13, Phase 5 관통)

- **`/compose-room` 스킬 신설**(`promptscene/skills/compose-room/`): SKILL.md(PARSE→RESOLVE→PLAN→EXECUTE→VERIFY) + 자산 2종(`build_composed_room.cs` N기능 조립, `verify_composition.cs` N기능 §5 판정). 설계 확정: compose-room은 **오케스트레이터** — 신규 담당은 ①자연어→기능 선택 ②부품 조율+최종 판정뿐, 조립은 assemble-room·scaffold-content **참조 호출**(절차 복제 금지, SSOT). PLAN은 `composition-plan.json`으로 박제(계획 vs 실행 문제 분리), unresolved/conflict 시 정지·질문.
- **관통 검증 PASS**: "측정 도구 있는 룸 만들어줘" → Ruler(Category 측정) 선택 → `ComposedRoom_1` 조립 → Room.exe 재빌드 → 서버+에디터 클라 조인 → **§6.5 4신호 전부**(become-a-player / 로비 소멸+MovedObjectsHolder / Desktop(Clone) IsOwner=True / DummyController+헤드팔로워+NetworkTransform) + **§5 COMPOSITION PASS**(ruler 자기등록·SetEnabled 무예외·Meta 룰러/측정).
- **핵심 발견·수정 — 스크립트 단발 빌드의 FishNet SceneId 미할당 함정**: 한 `script-execute`로 씬 생성→저장하면 FishNet 자동 SceneId 훅이 비결정적으로 누락돼 스포너 `SceneId=0`으로 저장 → **아바타만 조용히 안 뜸**(입장·로비 소멸·스포너 복제는 정상). 격리 검증(FeatureLab_1은 오늘 정상 스폰 → 환경 아님, ComposedRoom_1 3회 실패 → 씬 결함)으로 원인 확정, FishNet `NetworkObject.CreateSceneId(force)` 명시 호출로 수정. compose-room·scaffold-content 두 빌더 + contract §1에 반영. 상세는 §7·contract §1.
- (이전 세션 2026-07-10 가드레일 실전화 요약은 git 이력 참조.)

## 6. 막혔을 때 어디를 읽나 (라우팅 요약)

`oxr-docs-routing` 스킬이 정본이다. 요지:

| 증상/작업 | 소스 |
|---|---|
| 계약 인터페이스 컴파일 에러 | contract §2 |
| 아바타 안 뜸 / 로비 안 사라짐 / WASD 불가 | [promptscene/docs/build-working-room.md](promptscene/docs/build-working-room.md) + C1~C4 |
| 네트워크 스폰·RPC·소유권 | GitBook Object Management(패턴) → **PackageCache XumNet 소스로 시그니처 재검증** → `Documentation~/ai` 보충 |
| 서버(.exe) 빌드/실행 | [promptscene/docs/build-xumlobby-server.md](promptscene/docs/build-xumlobby-server.md) |
| Quest 클라 빌드 / 화면 깜빡임 | [promptscene/docs/build-meta-client.md](promptscene/docs/build-meta-client.md) (§2.4-D 즉답) |
| 새 씬 조립 | [promptscene/docs/build-working-room.md](promptscene/docs/build-working-room.md) 우선 |

**가드레일:** PackageCache와 패키지 매니페스트는 **수정 금지**(훅이 차단). 막히면 우회 말고 **읽고→보고→지시 대기**. 문제를 풀면 그 해법을 해당 `promptscene/docs/`에 추가하는 **패치를 제안**(역기입 루프).

## 7. 반드시 알아야 할 함정 (요약 — 상세는 링크 문서에)

> **교훈(가드 검증):** 가드는 *통과* 테스트가 아니라 **차단 재현**으로 검증한다. fail-open이면 조용히 무력화되므로 "안 걸렸다=안전"이 아니다 — 실제 차단이 재현되는지를 봐야 한다.

**플러그인/가드 관련 (이번 세션 신규):**
- **하위 폴더에서 세션 시작 → 플러그인 미로드:** 레포 루트(`c:\J_0`)가 아닌 하위 폴더(예: `XRCollabDemo/`)에서 Claude 세션을 열면 플러그인이 로드되지 않아 **가드 훅이 아예 발동하지 않는다**(PackageCache 보호가 조용히 사라짐). 반드시 마켓플레이스/플러그인 루트에서 시작.
- **플러그인 루트에 Unity 프로젝트 두면 install EBUSY:** Unity 프로젝트가 플러그인 루트에 있으면 `Install locally`가 Library 파일 락으로 **EBUSY** 실패. → 플러그인을 `promptscene/` 하위로 **분리**해 설치기가 Unity 프로젝트를 건드리지 않게 했다.
- **Windows 백슬래시 경로 → 정규식 fail-open:** 가드 정규식이 슬래시(`/`) 경로만 매칭해서, 백슬래시(`\`) 경로가 **무검사 통과**(fail-open)됐다. → 입구에서 `\`→`/` **정규화** + 두 구분자 모두 커버하는 **회귀 러너** 추가로 봉합.
- **가드는 보수적으로 차단(cp 백업 포함):** 명령 문자열에 보호 경로가 들어가면 읽기성 의도라도 막힌다 — `manifest.json`을 언급하는 `cp` 백업조차 차단. 읽기는 `cat`/`grep`/`ls`만, 보호 파일을 셸로 스냅샷하려 하지 말 것.
- **플러그인 스킬/훅은 세션을 `c:\J_0`에서 시작해야 로드됨:** XRCollabDemo 등 하위 폴더에서 시작하면 가드가 **조용히 무력화(fail-open)**. 시작 로그의 스킬 출처로 확인.
- **플러그인 install은 플러그인 루트 전체를 `~/.claude/plugins/cache`로 복사** — 루트에 Unity 프로젝트가 있으면 EBUSY. 플러그인은 `promptscene/` 하위로 분리 완료.
- **Windows의 Edit/Write는 백슬래시 경로를 넘김** — 슬래시 전용 정규식은 fail-open. 가드는 입구 정규화로 수정됨. 스크립트 수정 시 반드시 **회귀 러너 실행**.
- **가드는 보수적:** 보호 파일을 source로 하는 `cp`(백업)도 차단됨. 훅 스크립트 수정은 **소스 수정만으론 반영 안 됨** — 플러그인 재설치 + 세션 재시작 필요.

**Unity/네트워크:**
- **Room.exe 재빌드 필요 조건:** FishNet 네트워크 씬 오브젝트를 배치/재배치하면 SceneId 재직렬화 + scene-save + **Room.exe 재빌드** 없이는 서버가 스폰 안 함(아바타/오브젝트 안 뜸). — launchpad 회고 §기술2.
- **스크립트 단발 빌드는 FishNet SceneId 자동 할당을 놓친다(2026-07-13 발견):** `NewScene→배치→SaveScene`을 한 `script-execute`로 끝내면 스포너 NetworkObject가 `SceneId=0/IsSceneObject=false`로 저장돼 **아바타만 조용히 스폰 실패**(입장·로비 소멸·스포너 복제는 정상, "become a player"도 뜸 → 오진 유발). 빌더 저장 직전 `NetworkObject.CreateSceneId(scene,force:true,out changed)`+`ReserializeEditorSetValues`(둘 다 internal→리플렉션) 명시 호출로 고정. 빌더 로그의 `sceneIdsGenerated≥1` 확인. 상세 contract §1.
- **PlayerSpawner는 프리팹으로:** `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab`. 스크립트로 NetworkObject 만들면 scene id 무효 → "Failed to confirm access"로 입장 거부. — architecture 메모리 / build-working-room.
- **콜드 Room 빌드는 10분+**, 그동안 MCP 전부 블로킹("Response data is null"). **행 아님** — 파일시스템(level0/cfg LastWriteTime) 폴링으로 완료 판정, MCP 응답성으로 판단 금지.
- **새 `.cs` 추가 직후 빌드 실패** 가능(`scripts are compiling`) — `EditorApplication.isCompiling==false` 확인 후 빌드.
- **입력 핸들링은 현재 `activeInputHandler=2`(Both)** (2026-07-15 실측, §5): 레거시 `UnityEngine.Input`과 신형 Input System이 **둘 다 동작** → `SimpleClickProvider`의 레거시 `Input.GetMouseButtonDown`·룰러 HUD 클릭이 정상. ⚠️ 단 **New 전용(=1)로 바꾸면**(build-meta-client §2.4-E: Android 빌드 경고 회피용) 레거시 `UnityEngine.Input`이 예외를 던짐 → `Mouse.current...`로 포팅 필요(`DummyController`는 이미 포팅됨), uGUI 클릭엔 `InputSystemUIInputModule` 필요.
- **전용 서버 실행순서:** `MasterAndSpawner.exe` → (6s) `Room.exe` → 그다음 에디터 Play. 서버 안 켜고 Play만 = 로비에서 안 넘어감.
- **이 머신 `python3`은 Store 스텁** — 스크립트는 `python`/`py`를 써야 실제로 돈다.
- **클라 런타임 네트워크 스폰은 `InstanceFinder.IsClientStarted` 뒤에(2026-07-16 M2):** `core.Net.Spawn`(→`XumNetwork.Instantiate`)을 룸 FishNet 클라가 완전히 active 되기 전에 부르면 스폰 ServerRpc가 "client is not active"로 **조용히 드롭**(오브젝트 안 뜸). `ClientId` 유효(마스터 링크)만으론 부족 — `IsClientStarted` 게이트 + 프롭 등장까지 재시도. `XumNetwork.Instantiate`는 클라에서 정상적으로 `HandleInstantiationServerRpc`로 서버 왕복하니 API는 문제 아님(스카우트 소스 확인). grab-ownership-survey §실증.
- **콜드 스타트 exe의 방 조인이 액세스 토큰 만료로 실패(2026-07-20 M3, 트랩 K):** 방 액세스 토큰 유효기간(`AccessTimeoutPeriod`)이 **10초**인데 exe 첫 실행은 StartMatch→룸 씬 로드(셰이더 컴파일)가 이를 초과 → room.log `Failed to confirm the access`, 클라 `Room could not validate you` 후 Client.unity 재로드(하네스 인스턴스 2중화로 로그 2줄씩 = 조인 실패의 시그니처). **재기동(웜 스타트)이 정답** — 씬 로드가 빨라져 통과. 트랩 J와 별개의 실패 모드. build-desktop-client §8 K.
- **2 데스크톱 게스트 동시 조인 = MST 인프라 flakiness(2026-07-16 M2):** 같은 머신 2번째 게스트가 `SignInAsGuest` 콜백 미완(signedIn 고착) 또는 룸 접근 검증 미완("just joined"인데 validated 안 됨)으로 조인 실패. 서버측 FishNet `ServerManager.Transport_OnServerConnectionState` NRE(2nd connection, 기존 데모 이슈)와 동반. **네트워크 기능 결함 아님.** 완화: 조인 직렬화(A player 확정 후 B 기동)+재시도. 신뢰 토폴로지는 M0/M1의 **에디터 클라 + 1 데스크톱 클라**.

**교훈:** **가드는 "통과 테스트"가 아니라 "차단 재현"으로 검증한다** — python 스텁·세션 스코프·백슬래시 3건 모두 통과 테스트는 성공했으나 실전에서 fail-open이었음.

## 8. 다음에 할 일 / 열린 질문

> **설계 방향 기록:** 2026-07 아키텍처 토론의 결정 사항(사거리 재정의, COMPOSITIONS 층, 에셋 전략 등)은 [design-directions-2026-07.md](promptscene/docs/design-directions-2026-07.md)에 정리됨.

**바로 이어서:**
0zc. ✅ **⭐ `/add-component` 스킬+에이전트 완성 + 자기검증 PASS(2026-07-24, migration §16).** 골격 채움 전담(assemble-room의 짝): 상담/견적(FEATURE↔COMPOSITION 분류·capability-map 재조합✅/개척⛔·oxr-docs-routing) → 룸(없으면 `/assemble-room` 참조) → 컴포넌트 확보(재사용/AI생성/사람작성) → 층 배치+§3b 씬임베드 배선(+새 네트워크 프리팹이면 C1) → §5+§6.5 QuickTest → (옵션) `/cross-platform-ui`. **회고 A**(6루프 §9~§14 공통[App.HotUpdate·3b·층배치·§5신호·컴파일0]/차이[네트워크프리팹·XRI·FEATURE↔COMPOSITION])로 EXECUTE 동결, **회고 B: scaffold-content의 studio 역할을 add-component가 흡수**(scaffold-content studio판 미제작, Ruler 템플릿 이관, XRCollab판 존치). 자산 3종(`add_component.cs` 배치+배선 / `verify_component.cs` §5+§6.5 KIND분기 / `FeatureContent.cs.template` studio변형) + `.claude/agents/add-component.md`(⚠세션 레지스트리 트랩=다음 세션부터 subagent 호출, 이번 자기검증은 메인 루프 직접 실행). **자기검증:** `/assemble-room AddCompProof_1`→`/add-component 룰러`→**§5/§6.5 FEATURE PASS**(자기등록 `ruler→RulerContent`·SetEnabled 무예외/멱등·Meta 룰러/측정·아바타 스폰·Error 0·Exception 0), 손작업 Ruler와 동형. main 커밋(브랜치 미분리, 지시 §3). **다음=①`/compose-room` studio판 재작성(assemble-room+add-component 조율, 큐 다음) ②MPPM 2클라 파리티 ③DartProps→다트 연결.** SSOT [xumflow-migration.md](promptscene/docs/xumflow-migration.md) **§16**.
0za. ✅ **⭐ studio D2 점수게임 여정 Loop 1~3 완주 — 5층 전체(COMPOSITIONS 포함) studio 성립(2026-07-24). PromptScene 아키텍처 완전 증명.** 선행 IEventBus는 §9 verbatim Core 복사로 **이미 상속**(Contracts/RoomCore diff=IDENTICAL, 계약 무수정) → 건너뜀. TargetProps/TargetMarker/TargetHitEvent verbatim 이식(App.HotUpdate, CS 0) + `Target.prefab`(NetworkObject+SphereCollider+TargetMarker, **URP/Lit 빨강=마젠타 함정 선제 회피**) C1 편입(DefaultPrefabObjects count=10) + `PromptSceneRoom_1` FEATURES 배치·씬임베드 배선(**4기능 공존**). **§5 host PASS:** 자기등록(target-props)·Meta(과녁/게임)·SetEnabled 멱등·**4과녁 네트워크 스폰(IsSpawned=True)**·명중→`TargetHitEvent` 버스 발행(`SubmitExternalRay` 실 핸들러 경로, bus=True)·버스 스모크(전달/멱등/예외격리/해지)·**Error 0**(Exception 2건=XREAL env DllNotFound + 의도적 격리테스트 throw). **Loop 2 ScoreHud(점수판/게임):** verbatim 이식(ScoreChangedEvent 소유, 순수 IMGUI, 프리팹 불요) + **TargetProps와 상호참조 0(grep 양방향)** + 5기능 공존. §5 PASS — 자기등록(score-hud)·Meta·SetEnabled 멱등·버스 구독→표시상태 채움(Scores=[2,3])·해지 실증(disable 후 발행 무수신)·Error 0. **버스 실사용자 2종(발행 TargetProps / 구독 ScoreHud) → 스택 rule-of-two studio 성립.** **Loop 3 TargetShootoutMatch COMPOSITION(+MatchView 네트워크 권위 프리팹):** studio **첫 `===== COMPOSITIONS =====` 층** 생성(contract §1 정의 층 첫 충전) + MatchView C1(→11). MatchView=ChatChannelView 동형(ServerRpc 상행+ObserversRpc 하행, 신규 API 0). COMPOSITION=plain MonoBehaviour(Contents 미등록, 씬 상주). **⭐ §5 서버권위 루프 실증(주입 아닌 실 흐름):** 클릭 명중 3회→TargetHitEvent→COMPOSITION→MatchView.ReportHit(ServerRpc)→**서버 집계 1→2→3**→ObserversRpc 방송→ScoreChanged→ScoreHud 실 수신 + `[shootout] WINNER P0`(chatPresent=레지스트리 조회)→resetDelay 후 **리셋(빈 보드)**. 6콘텐츠 오브젝트 공존(5기능+COMPOSITION)·MatchView spawn-once·Error 0. **FEATURE↔FEATURE 참조 0 유지 + COMPOSITION만 이벤트 타입으로 조율(클래스 참조 0, grep).** **⭐ 5층 전체(SYSTEMS/ENVIRONMENT/UI/FEATURES/COMPOSITIONS[+_DYNAMIC]) studio 성립 = 아키텍처 완전 증명.** 함정: ENVIRONMENT Capsule이 최좌측 과녁 가림→가시 과녁 골라 주입. **다음=①2클라 파리티(MPPM: Chat 양방향+Grab 핸드오버+과녁/점수 동기 §11.6) ②DartProps 이식 후 COMPOSITION에 다트 연결(Subscribe<DartHitEvent> 2줄, V1 배당금) ③스킬 Smart-Deploy 재작성.** SSOT [xumflow-migration.md](promptscene/docs/xumflow-migration.md) **§12~§14**.
0z. ✅ **studio Chat FEATURE 이식 + §5 단일 클라 PASS(2026-07-23).** XRCollab ChatContent/ChatChannelView **verbatim 이식**(Ruler 선례, API 차이 0) → `Content/Chat/`, `ChatChannel.prefab`(NetworkObject+View) C1 등록(DefaultPrefabObjects 6→8 + Addressables), `PromptSceneRoom_1` FEATURES 배치·씬임베드 배선. **§5 MCP PASS:** 자기등록(Contents=[chat,ruler])·Meta(채팅/소통)·SetEnabled 무예외·채널 1개 spawn-or-reuse·host 루프백 RPC(서버주입 P0·순서보존)·Error 0. **⛔ studio 첫 2인 = 인프라 블록(사용자: "여기서 멈춤"):** QuickTest=MST 아닌 **FishNet 직결 localhost:7770**, 2번째 프로세스 수단 부재(ParrelSync·MPPM 미설치, 경량 빌드 없음, Smart-Deploy 미경험). **다음 세션 착수 전 결정: MPPM 추가(권장)/클론/빌드.** 트랩 J/K는 MST 소산이라 studio 구조적 무존재 개연(→ /multiplayer-check SSOT 씨앗). SSOT [xumflow-migration.md](promptscene/docs/xumflow-migration.md) **§10**.
0x. ✅ **XumFlow studio 첫 관통 완료(2026-07-23).** 환경/MCP 0.66.0 확보 + port-prep 3결정 후 **길 1(샘플룸 위에 얹기)로 RoomCore + Ruler 관통**: §2 대조(API 차이 0)→§3 베이스라인(T_RoomB 복제=PromptSceneRoom_1, leaf 주소 등록)→§4 RoomCore(4서비스·SYSTEMS 무손상)→§5 Ruler(자기등록·SetEnabled·바닥 raycast→OnClick→네트워크 측정 스폰 pos=중점+LineRenderer+라벨 '1.41m'+IsSpawned) — 전부 **MCP 자동판정 PASS, Error 0.** Core=`ContentLogic/PromptScene/` verbatim(별도 asmdef 불요). **+구조 5층 정리 완료**(SYSTEMS/FEATURES/ENVIRONMENT/UI, --PLAYER_SPAWNER→SYSTEMS/Player 재부모 후 SceneId 보존 실측 — §9 "구조 정리"). **+Ruler 최소 HUD**(IMGUI/OnGUI — studio 2-EventSystem 혼재 회피; 사람 라이브 PASS — §9 "Ruler 최소 HUD"). **+크로스플랫폼 UI**(World Space uGUI 저작객체 + `TrackedDeviceGraphicRaycaster` + `RoomHudBinder` 런타임배선 + 빌보드 + 동적 OS 한글폰트; **XR world-click** `XRWorldClicker`+`SimpleClickProvider.SubmitExternalRay` — 데스크톱 마우스 + **XR 컨트롤러 sim**으로 UI 클릭+바닥 측정 사람 PASS, 손은 동일 인터랙터라 코드 커버·실기기 V2). **재사용 절차 SSOT 신규 = [build-studio-room.md](promptscene/docs/build-studio-room.md)**(겪은 것만; 배포=미경험 제외). 다음 순서: 채팅 이식(studio 2인)→그랩/다트→(과녁)→상담/견적, 각 관통 후 /Room editor 오케스트레이터 조립. **다음 = ①2클라 파리티(별도 데스크톱) ②TargetProps/ScoreHud/COMPOSITION(D2) 이식 ③스킬(assemble/compose/scaffold) Smart-Deploy 재작성(절차 §9에 실측됨 — 스킬 EXECUTE/VERIFY 정독 후).** SSOT [xumflow-migration.md](promptscene/docs/xumflow-migration.md) **§9**·§7·§8.
0v. ✅ **완료 — V1 던지기(다트) 라이브 실증(2026-07-22, §5).** 2클라 데스크톱에서 비행 전파·오너 유지·명중→점수 동기 PASS. 던지기=재조합(D4-2 불요), capability-map 첫 조각 작성. **다음 = V2 실기기 던지기**: Quest 3 충전+adb 페어링 → `/deploy-client Meta`([build-meta-client.md](promptscene/docs/build-meta-client.md)) → V1b에서 넘어온 재검증(XR 시뮬/실기기 스윙→throwOnDetach velocity 계승, non-kinematic 수정 확인). 손맛·크로스플랫폼·경합 뺏기(D4-2)는 V2 몫, 판정자=사용자. 준비물 SSOT: build-desktop-client §13 "V2 준비물".
0. ✅ **완료 — D2 COMPOSITIONS 라이브 실증(2026-07-21, §5a·§12).** 프리팹 2종 C1 등록 → `ShootoutRoom_1` 조립 → Room.exe 재빌드 → **단일 클라 서버권위 루프 + 2클라 점수 동기 파리티 + 버스 런타임** 전부 라이브 PASS. 절차 SSOT는 [build-desktop-client.md](promptscene/docs/build-desktop-client.md) §12. **남은 후속(선택):** 실제 마우스-클릭 레이캐스트 경로 실증(현재 주입은 버스 경계 TargetHitEvent), Target 머티리얼 URP 셰이더 교정(현재 마젠타 — 기능 무관), 3인+·B도 득점하는 대칭 매치, compose-room의 COMPOSITION 편입(아래 §8-3).
1. ✅ **완료** — Phase 5 `/compose-room` 관통(§5e). 스킬 신설 커밋과 검증·문서 갱신 커밋을 분리해 기록.
2. ✅ **완료** — **D4-1 잡기 기반 소유권(§5b, 2026-07-16 M2).** `GrabbableProps` FEATURE로 2클라 핸드오버 5신호 PASS, SYSTEMS/계약 무수정.
2b. ✅ **완료** — **M3 채팅(§5a, 2026-07-20).** `ChatContent` FEATURE로 2클라 채팅 양방향 4신호 PASS — **원래 요청 3종(룰러 공유 M1 / 그랩 M2 / 채팅 M3) 완주.** 메시징 계약 승격은 검토서로 **보류** 판정([net-messaging-promotion-review.md](promptscene/docs/net-messaging-promotion-review.md)) — 두 번째 순수 버스 소비자가 생기면 재개.
2c. ✅ **완료** — **M4 다기능 합성 피날레(§5, 2026-07-20).** 자연어 한 줄 → `/compose-room` **슬래시 표면 발동** → 3기능(Chat+Ruler+Grabbable) 선택·ClickSpawner 비선택(음성 증거) → `ComposedRoom_2` §6.5+§5×3 PASS + 2클라 데모(시연). **§8-3의 "다기능 합성(2개+) 실증"과 "슬래시 표면 발동 검증"은 이것으로 닫힘.**
3. **로드맵 다음 순서(design-directions D5):** D4-1 완료 → **D2(COMPOSITIONS 층) ✅ 라이브 실증 완료(2026-07-21, §5·§12)** → 남은 갈래 D3(생성 에셋 파이프라인) / D4-2(예측, 견적 완료·대공사 보류). ⚠️ 소유권 계약 승격(IRoomCore 헬퍼)은 **두 번째 잡기-소유권 소비자**가 실제로 생길 때 재검토(grab-ownership-survey 판정). ⚠️ **이벤트 버스 두 번째 순수 소비자** 관련: net-messaging 승격 검토서의 "두 번째 순수 버스 소비자" 트리거와 별개 — IEventBus는 인프로세스 조율용, INetMessaging은 네트워크 방송용(혼동 금지).
4. **잡기 소유권 확장 여지(M2는 최소 관통):** VR 컨트롤러 그랩, 오너 이탈 중 잡힘 상태(NT 송신자 공백) 처리, 반납형 정책, 다중 소품, 3인 경합.
3. **compose-room 확장 여지:** ~~다기능 합성(2개+) 실증~~ ✅ **닫힘(M4, §5a — N=3, 슬래시 표면 발동 포함)**. 잔존: `mode:"extend"`(기존 룸에 기능 추가), 파라미터(`params`) 실제 전달, MutuallyExclusive 충돌 케이스 실증. **신규(D2): COMPOSITION 편입** — compose-room은 아직 FEATURES만 조립하고 COMPOSITIONS 층을 모른다. 자연어→게임모드 선택+COMPOSITION 배치+프리팹 배선까지 오케스트레이션하려면 `build_composed_room.cs`에 COMPOSITIONS 배치 로직 확장 필요(§8-0 절차의 씬 배치 부분).

**Phase 5 이후에도 남은 프론티어(launchpad 회고에서 이월):**
- ✅ **멀티플레이 실증 — 닫힘(2026-07-14~15).** 에디터 클라 + 빌드된 데스크톱 클라 2인이 서로의 아바타를 보고, Ruler 결과값(측정)이 양방향 전파됨을 라이브 증명. 실체: `AutoJoinClient.cs` 하네스 + [build-desktop-client.md](promptscene/docs/build-desktop-client.md).
- ✅ **잡기 소유권(D4-1) — 닫힘(2026-07-16, M2).** 2클라 핸드오버(Owner A→B→A) 5신호 PASS. `GrabbableProps` FEATURE, SYSTEMS/계약 무수정. (D4-2 예측은 여전히 밖.) SSOT: [grab-ownership-survey.md](promptscene/docs/grab-ownership-survey.md) §실증.
- **플레이테스트 하네스:** 게임 로직 "정확성"은 구조 하네스로 안 됨 → **시뮬 플레이어 N명** 플레이테스트 하네스가 필요(프론티어). (2클라 하네스가 그 첫 벽돌 — N명·시뮬 입력·판정으로 확장 여지.)
  - **(M3 메모①) 에폭 동기 로그 판정이 3연속 재사용됨(M1 측정→M2 그랩 5신호→M3 채팅 4신호)** — 공유 에폭 + 역할별 안무 + `-logFile` 교차 대조라는 같은 골격의 3번째 복제. N명 하네스를 만들 때 이 골격(에폭 타임라인·역할 안무·신호 표 판정)을 **판정 코어로 승격**하는 게 자연스러운 출발점.
  - **(M3 메모②) N명 하네스의 선행 과제 = 조인 인프라 안정화.** 트랩 J(멀티 게스트 동시 조인 flakiness)에 트랩 K(콜드 스타트 토큰 만료)까지 — 2인도 직렬화+재기동으로 겨우 안정인데 N인은 조인 자체가 병목. N명 하네스 착수 전에 조인 재시도/워밍업의 하네스 내장(현재는 수동 재기동)이 먼저다.
- **시각화 우선:** 진행도/역할/결과를 화면에 띄우기(로그 말고).
- **미감 루프:** 스크린샷 → 비전/사람 승인 없이는 "이쁘게" 자동 달성 불가.
- **런치패드 은유 재고:** 방마다 UI 컨셉이 다를 텐데 아이콘 그리드가 맞는가?

## 9. 환경/툴 메모

- **Unity/MCP:** XRCollabDemo `6000.3.11f1`(MCP 27826) / DeepChairProject `6000.1.7f1`(MCP 22863). 스킬 실행 전 대상 에디터의 `ai-game-developer` MCP가 **살아 있어야** 함.
- **script-execute:** Roslyn full-code 모드(className/methodName). 프로젝트 타입은 리플렉션(AppDomain 순회)으로 접근. `using UnityEditor.SceneManagement` 등 누락 주의, `Object`는 `UnityEngine.Object`로 명시.
- **셸:** 기본 PowerShell(5.1) + Bash 툴(POSIX). 훅 스크립트는 bash.
- **플러그인 설치:** `c:\J_0`를 **로컬 마켓플레이스**로 등록 후 `Install locally`로 `promptscene` 설치. **훅/스킬 변경은 재설치 + 세션 재시작해야 반영**된다(`/reload-plugins`만으로 훅이 안 잡힐 수 있음). 세션은 반드시 마켓플레이스 루트에서 시작(→ §7).
- **플러그인 로드 방식:** local marketplace(`c:\J_0`) + `/plugin install promptscene@promptscene-harness`(Install locally). **훅 반영은 재설치 + 재시작 필요.**
- **원격/private:** `oxr-sdk` 레포는 private — clone/fetch 시도 금지, 필요한 건 로컬 PackageCache에 있음.
- **에이전트 레지스트리 함정:** 세션 도중 새로 만든 `.claude/agents/*.md`는 그 세션에서 `subagent_type`으로 **등록되지 않는다**(레지스트리는 세션 시작 시 로드). 새 서브에이전트를 쓰려면 **세션 재시작** 필요 — 훅/스킬 반영 규칙(재설치+재시작)과 동일. 임시 위임은 기존 등록 에이전트(`general-purpose` 등)에 규칙을 인라인해 처리. (2026-07-16 guard-probe 탐침에서 확인. 참고: PreToolUse 가드 훅은 서브에이전트 내부 도구 호출에도 fail-closed로 적용됨을 라이브 검증.)

---

*관련 메모리: PromptScene Architecture, XRCollabDemo MCP Setup, PromptScene Room Anatomy, prefer-rename-over-uninstall, deploy-client-loadpreset-loop.*
