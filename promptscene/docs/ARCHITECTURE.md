> 이 문서는 promptscene-harness의 설계 개념과 구조를 설명한다. 설치·사용법은 README.md 참고.

# PromptScene — 설계와 구조

## 설계 개념 (Layered Architecture)

씬을 **작동 토대(SYSTEMS / runtime platform)** 와 그 위에 **조합해 쌓는
요소(FEATURES / content modules)** 로 분리해 설계한다. — *composition over
inheritance, separation of concerns.*

- **SYSTEMS — 작동 토대 (runtime platform).**
  룸이 *작동하는 방식* 그 자체. 네트워크 연결, 세션, 플레이어 입장·스폰,
  코어 레지스트리. 씬의 겉모습이 무엇이든 이 메커니즘은 동일하게 성립해야
  한다. 불변식 C1~C4로 집행한다.

- **FEATURES — 조합 요소 (content modules).**
  작동 토대 위에서 노는 선택 요소. 기능 모듈은 물론, 환경 연출(재질·조명·
  레이아웃 변형)도 이 층에 속한다. 자연어 의도가 바뀌면 이 층만 바뀐다.
  **FEATURE끼리는 서로 모른다** — 상호작용은 인프로세스 이벤트 버스로만.

- **COMPOSITIONS — 조율 층 (game modes / scenarios).**
  직교하는 FEATURE 여럿을 하나의 게임 루프로 엮는, **기능들을 "아는" 유일한
  층**. FEATURE 간 직접 의존을 허용하는 대신 조율자를 위에 얹는다(UE5 GameMode
  / 미디에이터). 있을 때만 존재하고, 빠지면 각 FEATURE는 독립 동작 그대로.
  — 도입: [design-directions-2026-07.md](design-directions-2026-07.md) D2.

```
COMPOSITIONS (게임모드/시나리오)  ← 기능들을 "아는" 유일한 층
      ↓ 한 방향
FEATURES (Ruler, Chat, Target…)   ← 여전히 서로 모름 (이벤트 버스로만 만남)
      ↓ 한 방향
SYSTEMS (네트워크·세션·스폰·코어)  ← 여전히 아무것도 모름
```

**핵심:** 자연어 입력이 바뀌어도 작동 토대는 바뀌지 않는다. 그래서 토대는
검증된 절차로 얼려두고(스킬화), 변하는 층만 조립하면 자연어→씬 자동화가
성립한다.

**성립 근거 (inversion of dependency).** 토대는 그 위에 무엇이 쌓이는지
알지 못한다 — 코어가 기능을 호출하는 게 아니라, 각 기능이 런타임에 코어의
레지스트리에 스스로 등록한다. 의존성은 **COMPOSITIONS → FEATURES → SYSTEMS
한 방향(비순환)**뿐이다. 층을 얹어도 이 한 방향이 유지되므로 기존 검증 논리
(모듈 on/off·이식성)가 그대로 산다. (UE5 "Modular Game Features / GameMode"와
동일 원칙.)

---

## 문서 트리 (`docs/`)

```
promptscene-content-contract.md   ← 단일 규격 (SSOT): 계약 인터페이스 · 씬 계층 · 불변식 C1~C4 · 검증 하네스
└── build-working-room.md         ← "이 문서 하나로" 작동하는 ROOM 씬 조립 (검증된 절차)
    ├── build-xumlobby-server.md  ← 서버(.exe) 빌드 & 런타임 검증 (Master + Room)
    ├── build-meta-client.md      ← 실기기(Meta Quest) 클라이언트 APK 빌드 · adb 배포
    └── build-desktop-client.md   ← Windows 데스크톱 클라(.exe) 빌드 + 2클라 멀티플레이 하네스(상호 가시 + 결과값 공유)
```

| 문서 | 내용 |
|---|---|
| [promptscene-content-contract.md](promptscene-content-contract.md) | 모든 스킬·런치패드·콘텐츠 모듈이 공유하는 **단일 규격**. SYSTEMS/FEATURES 분류, C# 계약 인터페이스, 씬 계층 규약, 불변식 C1~C4, 검증 하네스, 로드맵 |
| [build-working-room.md](build-working-room.md) | 입장·아바타 스폰·이동이 실제로 되는 ROOM 씬을 처음부터 조립하는 검증된 절차 (플레이어 스포너 + C1~C4) |
| [build-xumlobby-server.md](build-xumlobby-server.md) | MST + FishNet + XumNet 스택의 헤드리스 서버(MasterAndSpawner.exe / Room.exe) 빌드·런타임 검증 |
| [build-meta-client.md](build-meta-client.md) | Meta Quest용 클라이언트 APK 빌드(Android/OpenXR/IL2CPP/ARM64) + adb 설치·실행·검증 |

---

## 검증 하네스

"하네스"는 독립 코드가 아니라 **규격(계약 §5) + 실행 절차(`/promptscene:assemble-room` 스킬)** 로 존재한다.

- **구조/SYSTEMS 하네스** — 불변식 C1~C4를 리플렉션 read-back + 런타임 신호(§6.5: 아바타 스폰, 로비 언로드, WASD-ready)로 집행. **작동 검증됨** (BasicRoom_2로 라이브 증명).
- **COMPOSITIONS 하네스 신호 (D2 신규, 2026-07-21)** — 새 층의 §5급 신규 검증 신호는 **"FEATURE 간 상호 참조 0"**: 파일럿 두 FEATURE(TargetProps/ScoreHud) 소스에 상대 타입 참조가 하나도 없어야 한다(`grep`로 기계 판정). 상호작용은 오직 `IEventBus` + COMPOSITION 조율로만 성립 — 이게 성립하면 "게임 루프를 얹어도 직교성(모듈 on/off·이식성)이 살아 있다"는 증거. **구조 검증됨**(grep: 양방향 0, 각 FEATURE는 `PromptScene.Core`만 의존). **라이브 게임 루프도 실증됨(2026-07-21)** — `ShootoutRoom_1`에서 단일 클라 서버권위 루프(집계 1→2→3→승자→리셋) + 2클라 점수 동기 파리티(A 명중→별도 프로세스 B가 동일 스코어보드 수신) 라이브 판정(HANDOFF §5, build-desktop-client §12).
- **FEATURES 하네스** — `/promptscene:scaffold-content` 스킬로 실행체 존재: 프롬프트→FEATURE 생성 후 RoomCore 룸에 얹어 §5(자기등록·SetEnabled 무예외·메타)를 라이브 룸에서 집행. **작동 검증됨** (Phase 4, ClickSpawnerContent로 라이브 증명).
- **합성 하네스** — `/promptscene:compose-room` 스킬로 실행체 존재: 자연어 요청→기능 선택(카탈로그 매칭)→`composition-plan.json` 박제→여러 부품(assemble-room·scaffold-content) 오케스트레이션→§6.5 SYSTEMS + §5 FEATURES(기능마다)를 라이브 룸에서 자동 판정. **작동 검증됨** (Phase 5, 2026-07-13, `ComposedRoom_1`로 라이브 증명). **N=3 실증(M4, 2026-07-20)**: 자연어 한 줄("채팅으로 소통 + 측정 공유 + 물건 잡아 옮기기")을 **슬래시 표면으로 발동** → Chat+Ruler+GrabbableProps 선택·ClickSpawner 비선택(카탈로그 매칭의 음성 증거) → `ComposedRoom_2` §6.5 4신호 + §5×3 전부 PASS + 2클라 데모(채팅/측정 공유/핸드오버 — 시연). 피날레 스크린샷: [screenshots/m4-composed-room-finale.png](screenshots/m4-composed-room-finale.png).
- **멀티플레이 하네스 (2클라 상호 가시 + 결과값 공유)** — 지금껏 하네스는 "1인 + 서버"만 봤다. 이제 **에디터 클라 + 빌드된 데스크톱 클라** 2인을 localhost로 붙여, ①자기/②원격 아바타 소유권 + ③위치 전파를 **양측 시점**(에디터=리플렉션, exe=`-logFile`)에서 판정하고, FEATURE 결과값(Ruler 측정)의 **생성·제거 전파**까지 확인한다. 실체는 빌드된 클라에 심는 arg 게이트 자동조인 하네스(`Assets/PromptScene/Harness/AutoJoinClient.cs`) + 절차 문서. **작동 검증됨** (2026-07-14, `ComposedRoom_1`로 2클라 상호 가시 + Ruler 결과값 공유 라이브 증명). 사람이 직접 몰아볼 수 있게 인게임 최소 HUD(`RulerHudUI`: 룰러 토글 + Clear)도 추가·검증(2026-07-15, 클릭→네트워크 측정→공유). 절차·함정: [build-desktop-client.md](build-desktop-client.md).
  - **핸드오버 신호 확장 (M2, 2026-07-16)** — 상호 가시(위치)에 더해 **소유권 이전**을 판정한다. `AutoJoinClient`에 그랩 안무(`-psGrabTest`/`-psGrabRole`/`-psGrabEpoch`)를 추가: 두 클라가 공유 에폭 타임라인 위에서 잡기 소품 하나를 A→B→A로 넘겨받으며, 각자 `-logFile`에 `ownerId`/`isMine`/`pos`를 기록. 신호 = ①A잡기 Owner=A ②A놓기 후 위치 전파+Owner 유지(비반납) ③B탈취 Owner=B ④B놓기 위치 전파 ⑤A재탈취 Owner=A(양측 교차 일치). **작동 검증됨** (`GrabbableProps` FEATURE, 5신호 PASS). 구현·판정·함정: [grab-ownership-survey.md](grab-ownership-survey.md) §실증. (주의: 2 데스크톱 게스트 동시 조인은 MST 인프라 flakiness — 그랩 결함 아님.)
  - **채팅 신호 확장 (M3, 2026-07-20)** — 임의 페이로드의 **양방향 방송**을 판정한다. `AutoJoinClient`에 채팅 안무(`-psChatTest`/`-psChatRole`/`-psChatEpoch`, B역은 반응형: A 5건 수신 후 2건 회신)를 추가. 신호 = ①A발신→B수신(내용+발신자) ②B발신→A수신 ③연속 5건 순서 보존 ④발신자 표시 양측 교차 일치. **작동 검증됨** (`ChatContent` FEATURE, 4신호 PASS). 절차: [build-desktop-client.md](build-desktop-client.md) §11. 같은 에폭-로그 판정 골격의 3번째 재사용 — N명 하네스의 판정 코어 후보(HANDOFF §8).

---

## 진행 현황 (로드맵)

- ✅ **Phase 0** 룸 베이스 (구조 + 아바타 + UI 전환, 불변식 C1~C4)
- ✅ **Phase 1** 계약 규격
- ✅ **Phase 2** Ruler를 계약 위에 클린 재구현 (파일럿)
- ✅ **Phase 2.5** 씬 계층 표준화 (SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC)
- ⬜ **Phase 3** 런치패드 UI (registry → 아이콘 그리드 → SetEnabled) — 1차 시도는 **보류/롤백**됨 (회고: [promptscene-launchpad-attempt.md](promptscene-launchpad-attempt.md)). 단, 수동 플레이용 **최소 HUD**(`RulerHudUI`: registry 순회 토글 + Clear, IMGUI)는 2026-07-15 추가·라이브 검증 — 아이콘 그리드 런치패드가 아니라 "플레이 가능하게 하는 얇은 UI". 절차: [build-desktop-client.md](build-desktop-client.md) §9.
- ✅ **Phase 4** `/scaffold-content` 스킬화 + LLM 신규 기능 생성 템플릿 (`skills/scaffold-content/`: Ruler 패턴을 동결한 `FeatureContent.cs.template` + `build_feature_room.cs` + `verify_feature.cs`, 네트워크 live-prove는 `assemble-room` 자산 재사용). **라이브 검증됨** (2026-07-09): 샘플 FEATURE `ClickSpawnerContent`("클릭한 지점에 색 구 스폰")를 프롬프트→생성→`FeatureLab_1` 룸 조립→Room.exe 재빌드→서버+에디터 클라 조인까지 돌려, §6.5 SYSTEMS(아바타 스폰·로비 소멸·WASD)와 §5 FEATURES(자기등록·SetEnabled 무예외·메타 유효) **양쪽 PASS**. 동작(클릭→스폰, _DYNAMIC 배치, 끄면 정리)도 시뮬 클릭으로 확인.
- ✅ **Phase 5** `/compose-room` 합성 스킬 + 합성 하네스 (`skills/compose-room/`: `build_composed_room.cs` + `verify_composition.cs`, 씬/네트워크/조인 절차는 `assemble-room` 자산 재사용, RoomCore+FEATURES 배치는 `scaffold-content` 참조). compose-room이 신규 담당하는 건 **자연어→기능 선택**과 **부품 오케스트레이션+최종 판정**뿐 — 조립 절차는 하위 스킬을 **참조 호출**(복제 금지, SSOT). **라이브 검증됨** (2026-07-13): "측정 도구 있는 룸 만들어줘"→Ruler 선택→`composition-plan.json`→`ComposedRoom_1` 조립→Room.exe 재빌드→조인까지 돌려 §6.5 4신호 + §5 COMPOSITION(ruler) **모두 PASS**. 이 과정에서 **스크립트 단발 빌드의 FishNet SceneId 미할당 함정**(contract §1)을 발견·수정(compose-room·scaffold-content 양쪽 빌더에 `CreateSceneId` 명시 호출 추가).
- ✅ **D4 1단계 (잡기 기반 소유권)** — "잡는 순간 잡은 사람이 주인, 놓으면 확정 위치 전파(비반납)". SYSTEMS/계약 **무수정** — `GrabbableProps` FEATURE의 프리팹 뷰가 SDK `XumView`(Takeover) + client-auth `NetworkTransform`을 직접 씀(M1 `RulerMeasurementView`와 동형). **라이브 검증됨** (2026-07-16, M2, 2클라 핸드오버 5신호 PASS). D2(COMPOSITIONS)·D4-2(예측)는 여전히 밖. 근거·함정: [grab-ownership-survey.md](grab-ownership-survey.md) §실증, [design-directions-2026-07.md](design-directions-2026-07.md) D4.
- ✅ **D2 COMPOSITIONS 층 (2026-07-21)** — 게임 루프를 전제 파괴 없이 층으로 추가. 계약 1개 추가(인프로세스 `IEventBus`, `IRoomCore` 무변경) + 파일럿 FEATURE 2종(TargetProps 발행 / ScoreHud 표시, **상호 참조 0**) + 첫 COMPOSITION(TargetShootoutMatch: 과녁→서버 권위 점수→선취 N점 승자 공지→리셋) + 네트워크 권위 프리팹(MatchView, ChatChannelView 동형). **라이브 검증됨**: `ShootoutRoom_1`(§6.5 아바타 스폰·로비 소멸 + 프리팹 C1 등록 + `===== COMPOSITIONS =====` 씬 배치 + Room.exe 재빌드) 위에서 단일 클라 서버권위 루프(집계 1→2→3→선취 3점 승자→리셋) + **2클라 점수 동기 파리티**(에디터 A 명중→별도 데스크톱 B가 동일 서버권위 스코어보드 수신·승자 일치) + 버스 런타임 스모크 전부 PASS. SYSTEMS·C1~C4·FEATURE 상호참조 0 유지. 예측(D4-2) 견적: [prediction-survey.md](prediction-survey.md). 근거·절차: [design-directions-2026-07.md](design-directions-2026-07.md) D2, [build-desktop-client.md](build-desktop-client.md) §12.
- ✅ **M3 채팅** — 2클라 텍스트 채팅 양방향(발신자 표시 포함). 계약 **무수정** — `ChatContent` FEATURE의 프리팹 뷰(`ChatChannelView`)가 `ServerRpc(RequireOwnership=false)` 상행 + `ObserversRpc` 하행을 내부 구현(M1/M2 동형 분리), 발신자는 서버 주입 ClientId. UI는 기능 지역 IMGUI 패널(런치패드 교훈 — RulerHudUI 무확장). **라이브 검증됨** (2026-07-20, 4신호 PASS — 원래 요청 3종 M1/M2/M3 완주). v1 정직 한계: 백필 없음(늦게 조인하면 과거 메시지 안 보임). 절차: [build-desktop-client.md](build-desktop-client.md) §11. 메시징 계약 승격은 **보류** 판정: [net-messaging-promotion-review.md](net-messaging-promotion-review.md).

> PromptScene 런타임 코드는 XRCollabDemo 쪽 `Assets/PromptScene/`(namespace `PromptScene.Core`)에 있으며 이 레포에는 포함되지 않는다.
