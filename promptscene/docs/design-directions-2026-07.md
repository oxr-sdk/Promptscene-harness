# PromptScene — 설계 방향 결정 기록 (2026-07)

> 2026-07-10 아키텍처 토론(Claude.ai 세션)의 결정 사항. 배경 질문:
> "축구게임 만들어줘" 같은 요청에서 드러난 두 갈래 한계 — ① 기능 간
> 상호작용/물리 설계, ② 기본에셋 없는 디자인 — 를 어떻게 다룰 것인가.

---

## D1. 프로젝트의 정직한 사거리 (재정의)

**결정:** PromptScene의 주장은 "자연어로 임의의 게임을 합성"이 아니라
**"검증된 룸 플랫폼 위에서, 자연어로 모듈을 선택·조합·파라미터화 + 구조
자동 검증"**이다.

**근거:** 자연어→씬은 본질적으로 명세 부족 문제("축구" 한 단어 뒤에 수백
개의 미결정). 현재 하네스는 §5 구조/계약 적합성만 증명하며(정직 계약),
자동화의 실제 가치는 창의적 20%가 아니라 틀리면 다 죽는 80%(씬 배선,
C1~C4, 서버 재빌드, 배포)를 기계로 보장하는 데 있다. 사람의 반복 예산을
손맛·미감 판단에 집중시키는 것이 가치 제안.

**사거리 안:** 기능들이 직교하는 구성(측정+메모+스포너가 있는 협업 룸).
**사거리 밖:** 기능이 깊게 결합된 게임 루프(축구) — D2로 확장 가능,
단 D4의 한계는 남음.

## D2. COMPOSITIONS 층 — 전제를 깨지 않고 층을 추가

**결정:** 기능 간 상호작용이 필요해지면 FEATURE 간 직접 의존을 허용하는
대신, **조율자 층을 위에 추가**한다:

COMPOSITIONS (게임모드/시나리오) ← 기능들을 "아는" 유일한 층
↓ 한 방향
FEATURES (Ball, Goal, ...)      ← 여전히 서로 모름
↓ 한 방향
SYSTEMS                          ← 여전히 아무것도 모름

계약 추가분은 IRoomCore에 타입드 이벤트 버스(Publish<T>/Subscribe<T>)
하나. 각 FEATURE는 이벤트 발행/자기 동작까지만 알고, 조율(골→점수→리셋)은
COMPOSITION만 안다. (UE5 GameMode / 미디에이터 패턴과 동일.)

**근거:** 직교성 전제를 해제하면 하네스의 검증 근거·모듈 on/off·이식성이
같이 무너진다. 층 추가는 비순환 한 방향 의존을 유지하므로 기존 검증 논리가
그대로 산다.

**착수 시점 (중요):** 지금 아님. 현재 FEATURE(Ruler, ClickSpawner)는
직교라 수요가 없고, 수요 없는 추상화는 인터페이스가 틀리게 설계된다
(launchpad 1차 시도의 교훈과 동형). **"서로 통신해야 하는 기능 2개"가
실제로 생기는 순간**이 착수 시점.

> **착수 (2026-07-21):** 게임 루프 요청(과녁 맞추기 점수전)으로 "서로 통신하는
> 기능 2개" 수요가 발생 → 착수 조건 충족. 원안 그대로 구현:
> - 계약 추가 **딱 하나** — 인프로세스 `IEventBus`(Publish/Subscribe/Unsubscribe).
>   내장 서비스로 등록·`TryGet<IEventBus>` 조회라 `IRoomCore`는 무변경(v0.2 원칙).
>   네트워크 전송은 버스 책임 아님(메커니즘-비정책 §4.5) — 복제는 프리팹 RPC로.
> - 파일럿 FEATURE 2종(**서로 모름이 핵심**): `TargetProps`(과녁 스폰→`TargetHitEvent`
>   발행까지만, 점수 모름) / `ScoreHud`(`ScoreChangedEvent` 구독·표시만, 과녁 모름).
>   두 소스 상호 타입 참조 **0**(grep 검증) — 이 층의 §5급 신규 검증 신호.
> - 첫 COMPOSITION `TargetShootoutMatch`(+ 네트워크 권위 프리팹 `MatchView`,
>   ChatChannelView 동형): TargetHit 구독 → **서버 권위** 집계 → ScoreChanged 발행
>   → 선취 N점 승자 공지(자체 표시; Chat 부재 시에도 무해 — 레지스트리 런타임 조회)
>   → 리셋. 신규 플랫폼 API 0(M1/M3 검증 기계 재사용).
>
> **실증 (2026-07-21, ✅ 라이브 완료):** 코드 + 구조 불변식(참조 0·Core-only) grep 검증에
> 더해 **라이브 게임 루프를 실제로 돌려 판정.** 프리팹 2종(Target/MatchView) C1 등록 →
> `ShootoutRoom_1`(씬에 `===== COMPOSITIONS =====` 포함) → Room.exe 재빌드 → ①**단일 클라
> 서버권위 루프**(명중→집계 1→2→3→선취 3점 승자→리셋→재판) ②**2클라 점수 동기 파리티**
> (에디터 A 명중 → 별도 데스크톱 프로세스 B가 동일 서버권위 스코어보드 수신·승자 P1 일치)
> ③**버스 런타임 스모크**(전달·멱등·예외격리) 전부 PASS. 절차·증거: [build-desktop-client.md](build-desktop-client.md) §12, HANDOFF §5.
> **정직 계약:** compose-room은 아직 COMPOSITION을 모름(합성 대상은 여전히 FEATURES만 — 편입은 후속);
> 게임 루프의 "재미"는 비검증(구조·서버권위·동기만 증명); 예측은 견적만([prediction-survey.md](prediction-survey.md)).

## D3. 생성 에셋 파이프라인 — "라이브러리 공장" 모델

**결정:** Qwen text2img → img23D 파이프라인은 런타임 즉석 생성이 아니라
**에디터 타임 에셋 공장**으로 설계한다: 생성 → 자동 후처리 → 구조 검증
→ 사람 승인 → 라이브러리 축적. `/compose-room`은 그 라이브러리에서
선택·배치만 한다.

**스킬의 본체는 생성이 아니라 후처리:** 데시메이션(Quest 폴리 예산),
피벗 정규화, 스케일 캘리브레이션(VR은 실측 미터 필수 — 프롬프트에 치수
요구 → 바운딩박스 맞춤), 콜라이더 자동 생성, 텍스처 압축.

**구조 검증은 기존 하네스 철학에 태운다:** 트라이 수 ≤ 예산 / 콜라이더
존재 / 스케일 ±10% / 머티리얼 수 ≤ N — 전부 기계 판정. "이쁜가"는 못
재도 "게임에 들어갈 자격"은 잰다.

**알려진 한계:** 스타일 일관성(생성마다 독립 → 룸이 잡탕 위험, 스타일
프리픽스 고정으로 완화만 가능), 사거리는 정적 소품까지(리깅 캐릭터·소켓
정렬 모듈러 조각은 밖).

## D4. 공유 물리 — 2단계로 분리, 예측은 미룸

**결정:** "공유"를 두 등급으로 구분한다.

| | 결과값 공유 (Ruler) — ✅ **실증됨(2026-07-14)** | 공유 리지드바디 (공) |
|---|---|---|
| 공유 대상 | 확정된 데이터 | 진행 중 시뮬레이션 |
| 주인 | 만든 사람, 고정 | 실시간 이동 (경합) |
| 지연 | 늦어도 정확 | 조작감 파괴 (VR 치명) |
| 필요 기계 | 스폰+RPC (있음) | authority+예측+화해+핸드오버 (없음) |

> **왼쪽 열 실증(2026-07-14):** Ruler 측정을 네트워크 스폰(RoomCore `FishNetSpawn` INetSpawn) + 프리팹 내부 NetworkBehaviour의 끝점 RPC(SyncVar 대안 `[ObserversRpc(BufferLast)]`)로 공유. 2클라(에디터+데스크톱 exe)에서 **생성·제거 양방향 전파** 확인. D4의 "스폰+RPC (있음)"이 계약 배선까지 내려와 실체화됨. 절차: [build-desktop-client.md](build-desktop-client.md). **주의**: 이는 "확정값 공유"만 — 1단계(잡기 소유권)·2단계(예측)는 여전히 밖.

- **1단계 (~~SYSTEMS 소폭 확장~~):** 잡기 기반 소유권 — "잡는 순간 잡은
  사람이 주인, 놓으면 확정 위치 전파". 예측 없이 협업 룸의 물건
  옮기기/배치 대부분 커버.
  > **정정(2026-07-16):** SYSTEMS 확장 **불필요** — SDK `XumView`(Takeover
  > + ServerRpc `GiveOwnership`)가 기계를 이미 제공, client-auth `NetworkTransform`이
  > 소유권 이전 시 authority 승계. 접점은 `RequestOwnership()` 한 줄이라 FEATURE
  > 지역 감싸기로 충분(계약 승격은 두 번째 소비자 시점). 조사: [grab-ownership-survey.md](grab-ownership-survey.md).
  > **실증됨(2026-07-16, M2):** `GrabbableProps` FEATURE(`GrabbableProp` 프리팹의
  > `GrabbableView`가 `XumView.RequestOwnership` 직접 사용 — M1 `RulerMeasurementView`와
  > 동형)로 2클라 잡기→놓기→핸드오버(Owner A→B→A)를 라이브 판정. 5신호 전부 PASS.
  > SYSTEMS/계약 무수정. 결과·함정: [grab-ownership-survey.md](grab-ownership-survey.md) §실증.
- **2단계 (대공사, 보류):** client-side prediction — 날아가는 공을 서로
  뺏는 연속 경합에만 필요. 얼려둔 SYSTEMS를 녹여 재검증해야 하는 공사.
  착수 전 확인 사항: FishNet의 prediction API가 XumNet 래퍼를 통과해
  사용 가능한지 PackageCache 소스로 확인 (oxr-docs-routing 사거리).
  > **정찰 완료(2026-07-21, P-G0):** [prediction-survey.md](prediction-survey.md).
  > 판정 — 예측은 **XumNet 통과해 직접 도달 가능**(감쌀 것도 막을 것도 없음, FishNet
  > 4.6.12). 단 **NetworkManager에 PredictionManager + 프리팹별 `_enablePrediction`**을
  > 요구해 M1/M2/M3의 "SYSTEMS 무수정"을 깬다 → "보류"의 이유가 *불가능*이 아니라
  > *SYSTEMS 해동 + 검증 인프라 비용*임이 확증됨. 지연/손실 시뮬은 FishNet 내장
  > `LatencySimulator` 존재(검증 인프라 낭보). 비용 등급: 도달성 낮음/구조 공사 높음.

## D5. 로드맵 순서

**닫기(완료) → Phase 5 관통 → 아픈 순서대로 D3 또는 D2 → D4-1단계 →
D4-2단계.**

Phase 5 최소 관통 목표: "측정 도구 있는 룸 만들어줘" 한 줄 → 기능 선택
→ assemble-room 조합 → §6.5 신호 자동 판정까지. 화려함 말고 관통.
첫 설계 질문: compose-room이 기존 스킬을 부품으로 호출하는가, 절차를
복제하는가.

## 미결 메모

- ~~contract §2의 `INetSpawn.Despawn` — XumNetwork.cs에 Despawn 심볼
  없음~~ → **해소(2026-07-14)**: RoomCore `FishNetSpawn.Despawn`이 서버면
  `InstanceFinder.ServerManager.Despawn`, 클라면 `INetDespawnRequest`
  ServerRpc로 매핑. contract §2에 주석·인터페이스 반영. "XumNetwork.Despawn"은
  존재하지 않으니 지어내지 말 것(여전히 유효한 경고).