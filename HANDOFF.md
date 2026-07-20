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
> **최종 갱신:** 2026-07-20.

---

## 1. 프로젝트 한 줄 정의

**XRCollabDemo(멀티유저 XR Unity 앱)에서 자연어 프롬프트로 룸(작동 토대 + 선택 기능)을 합성하고, 구조를
자동 검증한다.** 산출물은 두 갈래다: ① Unity 런타임 코드(얇은 코어 + 옵트인 콘텐츠 모듈), ② 그걸 조립·검증하는
Claude Code 플러그인(이 레포 = `promptscene-harness`).

## 2. 레포·프로젝트 지형 (헷갈리기 쉬움)

| 것 | 위치 | 정체 | 비고 |
|---|---|---|---|
| **이 레포 = 로컬 마켓플레이스** | `c:\J_0` | `.claude-plugin/marketplace.json` 루트 + 타깃 Unity 앱(XRCollabDemo) 동거 | git `main`. **여기서 세션을 열어야** 플러그인이 로드됨(→ §7 함정). |
| **promptscene 플러그인** | `c:\J_0\promptscene` | 플러그인 본체(plugin.json/skills 4종/hooks+회귀 러너). install EBUSY 회피 위해 레포 루트에서 분리 | marketplace.json은 루트 `.claude-plugin/`에 잔류. 로드 방식은 §9 |
| **XRCollabDemo** | `c:\J_0\XRCollabDemo` | 타깃 Unity 6 앱(`6000.3.11f1`), MCP 포트 **27826** | 런타임 코드가 사는 곳: `Assets/PromptScene/`. `Library/PackageCache`는 **읽기 전용**. |
| **DeepChairProject** | `C:\Unity\DeepChairProject` | 기능 **레퍼런스** 소스(ruler/laser/memo 등), Unity `6000.1.7f1`, MCP **22863** | 앱레이어가 XRCollabDemo에 없어 **lift-and-shift 불가 → 계약 위에 클린 재구현**이 정석. |

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

**스킬 4종(현재 동작):**
- `/promptscene:assemble-room <RoomName>` — ROOM 조립 + C1~C4 + 서버 재빌드/조인 + §6.5 런타임 신호 라이브 증명.
- `/promptscene:scaffold-content <설명>` — 프롬프트→FEATURE 모듈 생성(동결된 Ruler 템플릿) + RoomCore 테스트룸에 얹어 §5 라이브 검증.
- `/promptscene:compose-room <자연어 룸 요청>` — 오케스트레이터: 자연어→기능 선택(카탈로그 매칭 + `composition-plan.json` 박제)→assemble-room·scaffold-content **참조 호출**로 N기능 룸 조립→§6.5 SYSTEMS + §5 FEATURES(기능마다) 자동 판정. 조립 절차는 하위 스킬을 참조(복제 금지).
- `/promptscene:deploy-client [Meta|XReal|Tablet|Vision]` — 디바이스 프리셋 적용 + 룸 씬 번들 + 마스터 IP 베이크 + 빌드/adb 배포·검증.

**정직 계약:** 하네스는 **§5 구조/계약 적합성만** 증명한다. 기능의 실제 동작·미감은 **비검증**(사람/비전 루프 필요).

## 5. 직전 세션(2026-07-20 — M4 다기능 합성 피날레, 한 줄 → 3기능 룸)

- **M4 관통 (§8-3 "다기능 합성 실증" + "슬래시 표면 발동" 닫힘):** `/promptscene:compose-room 채팅으로 다른 참가자와 소통하고, 측정한 내용을 서로 볼 수 있고, 물건을 잡아서 옮길 수 있는 룸 만들어줘` — **슬래시 표면으로 발동**(절차 직접 수행 아님). RESOLVE가 소스 카탈로그 4종에서 **Chat(소통)+Ruler(측정)+GrabbableProps(물체) 3종 선택, ClickSpawner 비선택**(요청에 대응 능력 없음 — 카탈로그 매칭이 실제로 변별한다는 음성 증거, plan의 `notSelected`에 박제) → `ComposedRoom_2` 조립(`C1/C3/RoomCore/기능3종=True, sceneIdsGenerated=1`) → Room.exe 재빌드 → **§6.5 SYSTEMS 4신호 + §5 FEATURES 3종 전부 PASS**(`=== §5 COMPOSITION VERDICT: PASS (3 features) ===`, room.log `Client 0 has become a player`). plan: [composition-plan.json](promptscene/skills/compose-room/composition-plan.json).
- **빌더 갭 1건 발견·오케스트레이션으로 해소(역기입):** `build_composed_room.cs`는 기능 컴포넌트를 AddComponent까지만 하고 **직렬화 프리팹 필드는 배선하지 않는다**(ComposedRoom_1의 룰러도 M1 때 수동 배선했던 것). §5는 프리팹 없이도 통과하지만(우아한 경고) 실동작엔 필요 → EXECUTE에 후처리(채팅 채널/측정/잡기 프리팹 3종 배선+씬 재저장)를 넣고 compose-room SKILL.md Stage 4에 절차로 기입.
- **2클라 데모(증명 아님 — 시연):** 신뢰 토폴로지(에디터 A=clientId 1 + 데스크톱 exe B=clientId 2, Client.exe를 ComposedRoom_2 번들로 재빌드, cfg localhost). 한 세션에서 ①채팅 한 마디(B `RECV from=1`) ②측정 공유(3.28 m 선) ③프롭 핸드오버 왕복(B 로그 ownerId 1→2→1, 위치 전파 동반) 전부 관측. **피날레 스크린샷**(한 화면에 두 아바타+채팅 패널+측정선+프롭): [m4-composed-room-finale.png](promptscene/docs/screenshots/m4-composed-room-finale.png).
- **F0 회귀:** SuppressWorldClick 클레임화 — 기계 확인(OR-의미론 5케이스) + 라이브 확인(제3 claimant가 RulerHUD·채팅 패널의 매 프레임 쓰기에도 생존, 해제 후 잔류 0 — 구 bool이면 소멸했을 시나리오) 모두 PASS.
- **정직 계약:** 하네스 증명 범위는 **구조/계약 적합성**(§6.5+§5×3)까지. 데모의 기능 동작(채팅/측정/핸드오버)은 **시연이지 하네스 증명 아님**. `params` 실전달, `mode:"extend"`, MutuallyExclusive 충돌 케이스는 **여전히 미실증**(§8-3 잔존).

## 5a. 그 직전 세션(2026-07-20 — M3 채팅 닫힘, 원래 요청 3종 완주)

- **M3 실증 (룰러 공유·그랩·채팅 시리즈의 마지막 조각):** 새 FEATURE `ChatContent`(런타임 `Assets/PromptScene/Content/Chat/`)로 2클라 텍스트 채팅 양방향을 라이브 판정. **4신호 전부 PASS**(①A 발신 5건→B 수신, 내용+발신자 일치 ②B 회신 2건→A 수신 ③연속 5건 순서 보존 ④발신자 표시 양측 교차 일치 — 양측 모두 A건=P0/B건=P2). 검증된 두 기계의 조합만 사용(`ServerRpc(RequireOwnership=false)` 상행=M2 동형 + `ObserversRpc` 하행=M1 동형) — 신규 플랫폼 API 0. **계약(Contracts.cs) 무수정**, 프리팹 `ChatChannel`(NetworkObject+`ChatChannelView`) C1 자동 등록 + Room.exe/Client.exe 재빌드. 발신자는 **서버가 주입한 ClientId**(`NetworkConnection sender = null` 패턴, 위조 불가) — 닉네임 시스템 발명 안 함. 절차·판정 SSOT: [build-desktop-client.md](promptscene/docs/build-desktop-client.md) §11, 스크린샷 [m3-chat-two-clients.png](promptscene/docs/screenshots/m3-chat-two-clients.png).
- **신규 설계 지점 1개 — 채널 스폰-또는-재사용:** SetEnabled 시 씬에 채널이 없으면 `core.Net.Spawn`, 있으면 재사용(2클라가 각자 스폰해 2채널 되는 것 방지). 재시도 루프가 트랩 I를 흡수, 동시-인에이블 레이스는 수신 전-채널 집계(static Log)로 무해화. 실측: B(objId 38402 재사용, 양측 내내 1채널). SetEnabled(false)=패널 숨김만(채널은 타 클라용 존치).
- **Core 메커니즘 일반화 1건(정직 기록):** `SimpleClickProvider.SuppressWorldClick`을 단일 writable bool→**클레임 기반**으로 교체(패널 2개가 되면 마지막-쓰기-승리로 억제가 조용히 깨지는 잠재 버그 수정). SYSTEMS 파일 1개 수정이지만 메커니즘-비정책(M1이 이 훅을 추가했던 성격과 동일), 계약 무수정.
- **승격 검토서 작성(승격은 안 함):** FEATURE 내부 네트워크 사용 3건(M1 상태전파/M2 소유권/M3 방송 버스)의 모양 비교 → **`INetMessaging` 승격 보류** 권고(순수 버스 수요는 채팅 1건, 전달 의미론 상충 — BufferLast가 Ruler엔 필수/채팅엔 유해). 근거·트리거: [net-messaging-promotion-review.md](promptscene/docs/net-messaging-promotion-review.md).
- **함정(신규, 트랩 K):** 콜드 스타트 exe의 방 조인이 액세스 토큰 만료(AccessTimeoutPeriod 10s < 첫 실행 씬 로드)로 실패(`Room could not validate you`) → 재기동(웜 스타트)으로 통과. build-desktop-client §8 표 K.
- **정직 범위:** 텍스트 채팅 양방향, 2인, 데스크톱, 백필 없음(늦게 조인하면 과거 메시지 안 보임 — 패널에 명시)까지 증명. **밖:** 이력 동기화, 3인+, VR 입력(가상 키보드), 귓속말/채널 분리, 입력 중 WASD 억제.

## 5b. 그 이전 세션(2026-07-16 — M2 잡기 기반 소유권, D4-1 닫힘)

- **M2 실증 (D4 1단계 "잡기 기반 소유권" 닫힘):** 새 FEATURE `GrabbableProps`(런타임 `Assets/PromptScene/Content/GrabbableProps/`)로 2클라 잡기→이동→놓기→**핸드오버(Owner A→B→A)**를 라이브 판정. **5신호 전부 PASS**(①A잡기 Owner=A 양측 ②A놓기 위치 전파+Owner 유지=비반납 ③B탈취 Owner=B ④B놓기 위치 전파 ⑤A재탈취 Owner=A, 공유 objId 양측 교차 일치). **SYSTEMS·계약 무수정** — 프리팹 `GrabbableProp`(NetworkObject+`XumView` Takeover+client-auth `NetworkTransform`+`GrabbableView`)의 뷰가 SDK `XumView.RequestOwnership`를 직접 사용(M1 `RulerMeasurementView`와 동형). C1: `DefaultPrefabObjects` 자동 등록 + Room.exe 재빌드. 검증 SSOT: [grab-ownership-survey.md](promptscene/docs/grab-ownership-survey.md) §실증.
- **하네스 확장:** `AutoJoinClient`에 그랩 안무 추가(`-psGrabTest`/`-psGrabRole A|B`/`-psGrabEpoch` — 공유 에폭 타임라인). 런처 `Builds/App/play-grabtest.ps1`(서버+2클라, 직렬화 조인).
- **핵심 함정(신규, 아래 §7):** ①클라 런타임 스폰은 `IsClientStarted` 뒤에(ClientId 유효만으로 부족 → 스폰 RPC 드롭) ②2 데스크톱 게스트 동시 조인 MST flakiness(2번째 게스트 sign-in/validation 미완 + 서버 2nd-connect NRE) — 그랩 결함 아님, 인프라. 신뢰 토폴로지는 에디터+1데스크톱.
- **정직 범위:** 클릭 잡기·놓기·Takeover 핸드오버, 2인, 데스크톱, 비반납 모델까지 증명. **밖(알려진 한계):** VR 컨트롤러 그랩, 오너 이탈 중 잡힘 상태(NT 송신자 공백), 3인 동시 경합, 잡는 동안 원격 보간 품질, 예측(D4-2). 2클라 핸드오버는 **1회 클린 통과로 5신호 증명**했고 재현은 MST 게스트 flakiness로 불안정(1클라 스모크는 안정 재현).

## 5c. 그 이전 세션(2026-07-14~15, 멀티플레이 실증 + Ruler 결과값 공유)

- **M0 멀티플레이 실증 (프론티어 "멀티플레이 실증" 닫힘):** 에디터 클라 + **빌드된 Windows 데스크톱 클라**(2번째 클라, ParrelSync 없이) 2인을 localhost로 붙여 §6.5 확장 신호(①자기 IsOwner=True ②원격 Clone IsOwner=False ③위치 전파)를 **양측 시점**(에디터=리플렉션, exe=`-logFile`)에서 라이브 판정. 두 아바타 ObjId 교차 일치. 실체: arg 게이트 자동조인 하네스 `Assets/PromptScene/Harness/AutoJoinClient.cs`. 절차·함정 신규 문서 [build-desktop-client.md](promptscene/docs/build-desktop-client.md).
- **M1 Ruler 결과값 공유 (D4 왼쪽 열 실증):** RoomCore의 `INetSpawn`을 **FishNet 백엔드 `FishNetSpawn`**으로 실체화(계약 §4.5 메커니즘 승격 — SYSTEMS 해동 아님) + Ruler 측정을 `RulerMeasurement` 프리팹(NetworkObject + FEATURE-내부 `RulerMeasurementView`)으로 네트워크 스폰. 끝점은 `[ObserversRpc(BufferLast)]`로 전파(INetSpawn.Spawn이 못 나르는 per-object 데이터). **생성·제거 양방향 전파** 확인. RulerContent는 여전히 Core만 참조.
- **핵심 함정(문서 역기입됨, build-desktop-client §8):** ①서버 빌드가 `standaloneBuildSubtarget=Server`를 남겨 클라가 헤드리스(창 없음)로 나옴 → 클라 빌드 전 **Player 복구** ②하네스가 방 입장 시 파괴(C3 언로드) → `DontDestroyOnLoad` ③에디터 클라 활성 씬이 룸 씬이면 포트 7777 충돌 → **Client.unity 활성** ④실행 중 서버 exe 재빌드 불가(파일 락) → 종료 후 재빌드 ⑤빌드 완료 판정은 exe mtime 아님(`_Data/level0`·cfg) ⑥긴 블로킹 빌드/타깃 전환이 MCP 브리지 끊음 → 에디터 재시작 ⑦서버 재빌드는 cfg를 LAN IP로 되돌림 → localhost 테스트면 매번 교정.
- (설계 결정: M1은 사용자 선택으로 **RoomCore INetSpawn 실체화** 경로. 계약 주석의 "Despawn 매핑은 RoomCore 구현 참조"와 부합.)
- **수동 플레이 HUD(사람이 직접 몰기용):** 인게임 IMGUI 패널 `RulerHudUI`(룸 UI에 심음) — 레지스트리 순회로 콘텐츠별 ON/OFF 토글 + 측정 지우기 + 공유 카운트. `SimpleClickProvider`에 `SuppressWorldClick` 훅(HUD 클릭이 바닥 측정으로 새는 것 방지). `activeInputHandler=2(Both)`라 레거시 Input 클릭 동작. **클릭→네트워크 측정 스폰→공유** 라이브 검증. 런처 `Builds/App/play-2clients.ps1`. ⚠️ 이건 **Phase 3 런치패드(아이콘 그리드)가 아니라** "플레이 가능하게 하는 최소 UI" — 런치패드는 여전히 보류(§4). 절차·검증: build-desktop-client §9.

## 5d. 그 이전 세션(2026-07-13, Phase 5 관통)

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
1. ✅ **완료** — Phase 5 `/compose-room` 관통(§5d). 스킬 신설 커밋과 검증·문서 갱신 커밋을 분리해 기록.
2. ✅ **완료** — **D4-1 잡기 기반 소유권(§5b, 2026-07-16 M2).** `GrabbableProps` FEATURE로 2클라 핸드오버 5신호 PASS, SYSTEMS/계약 무수정.
2b. ✅ **완료** — **M3 채팅(§5a, 2026-07-20).** `ChatContent` FEATURE로 2클라 채팅 양방향 4신호 PASS — **원래 요청 3종(룰러 공유 M1 / 그랩 M2 / 채팅 M3) 완주.** 메시징 계약 승격은 검토서로 **보류** 판정([net-messaging-promotion-review.md](promptscene/docs/net-messaging-promotion-review.md)) — 두 번째 순수 버스 소비자가 생기면 재개.
2c. ✅ **완료** — **M4 다기능 합성 피날레(§5, 2026-07-20).** 자연어 한 줄 → `/compose-room` **슬래시 표면 발동** → 3기능(Chat+Ruler+Grabbable) 선택·ClickSpawner 비선택(음성 증거) → `ComposedRoom_2` §6.5+§5×3 PASS + 2클라 데모(시연). **§8-3의 "다기능 합성(2개+) 실증"과 "슬래시 표면 발동 검증"은 이것으로 닫힘.**
3. **로드맵 다음 순서(design-directions D5):** D4-1 완료 → **아픈 순서대로 D3(생성 에셋 파이프라인) 또는 D2(COMPOSITIONS 층)** → D4-2(예측). D2 착수 시점은 "서로 통신해야 하는 기능 2개"가 실제로 생길 때(지금 Ruler/ClickSpawner/GrabbableProps는 직교 → 수요 없음). ⚠️ 소유권 계약 승격(IRoomCore 헬퍼)은 **두 번째 잡기-소유권 소비자**가 실제로 생길 때 재검토(grab-ownership-survey 판정).
4. **잡기 소유권 확장 여지(M2는 최소 관통):** VR 컨트롤러 그랩, 오너 이탈 중 잡힘 상태(NT 송신자 공백) 처리, 반납형 정책, 다중 소품, 3인 경합.
3. **compose-room 확장 여지:** ~~다기능 합성(2개+) 실증~~ ✅ **닫힘(M4, §5 — N=3, 슬래시 표면 발동 포함)**. 잔존: `mode:"extend"`(기존 룸에 기능 추가), 파라미터(`params`) 실제 전달, MutuallyExclusive 충돌 케이스 실증.

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
