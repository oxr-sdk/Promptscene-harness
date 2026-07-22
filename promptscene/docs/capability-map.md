# PromptScene — Capability Map (능력 지도)

> **목적.** "무엇을 지어줄 수 있고(✅), 무엇을 아직 못 하나(⛔)"를 한곳에 모은 **능력 카탈로그**.
> [D6 상담층](design-directions-2026-07.md#d6-상담층--견적--브리프-2026-07-후속-결정)의 근거 자료: 브리프 질문은 **✅ 항목에서만** 파생하고(지킬 수 있는 것만 질문),
> ⛔ 항목은 질문 대신 **의향 기록 + 개척 청구서**로 남긴다. 각 능력은 **재조합(검증된 기계 조합) / 신규(새 기계 필요)** 로 표시한다.
>
> **정직 계약.** ✅ = 하네스로 **라이브 검증된** 구조/동작. 미감·재미·실기기 손맛은 여전히 비검증(V2/사람 루프). 근거 문서를 각 줄에 링크한다.
> **최초 작성:** 2026-07-22 (V1 다트 던지기 — capability-map의 첫 조각).

---

## ✅ 지금 지어줄 수 있는 것 (라이브 검증됨)

| 능력 | 등급 | 검증 | 근거 |
|---|---|---|---|
| 작동 룸 토대(네트워크·세션·입장·아바타 스폰·코어 레지스트리, 불변식 C1~C4) | 재조합 | §6.5 | [build-working-room.md](build-working-room.md), assemble-room 스킬 |
| 자연어 한 줄 → 기능 선택·조립(N=3)·구조 자동판정 | 재조합 | §5+§6.5 | HANDOFF §5a (M4), compose-room 스킬 |
| 측정 결과값 공유(Ruler, 확정 데이터 스폰+RPC 전파) | 재조합 | M1 | [build-desktop-client.md](build-desktop-client.md) §6 |
| **잡기 기반 소유권**(잡는 사람이 주인, 놓으면 확정 위치 전파, A→B→A 핸드오버) | 재조합 | M2 5신호 | [grab-ownership-survey.md](grab-ownership-survey.md) §실증 |
| 텍스트 채팅(2인 양방향, 서버 주입 발신자) | 재조합 | M3 4신호 | build-desktop-client §11 |
| 게임모드(COMPOSITIONS 층): 이벤트 버스로 직교 기능들을 서버권위 루프로 조율(점수·승자·리셋) | 재조합 | D2 | build-desktop-client §12, [design-directions](design-directions-2026-07.md#d2-compositions-층--전제를-깨지-않고-층을-추가) |
| **던지기(비경합 투사체)**: 던진 사람이 비행 내내 오너, client-auth NT로 궤적 전파, 명중→이벤트→서버권위 점수 양측 동기 | **재조합** | **V1 (2클라)** | build-desktop-client §13 (아래) |

**던지기 = 재조합인 이유(경계 명시).** 던지기는 새 기계가 아니라 **이미 검증된 기계들의 재조합**이다:
`XumView`(Takeover 소유권, M2) + client-authoritative `NetworkTransform`(M1/M2) + `IEventBus`(D2) + `MatchView` 서버권위 집계(D2) +
`XRGrabInteractable.throwOnDetach`(XRI 내장). 신규 플랫폼 API 0. 던진 사람이 비행 내내 단독 오너이고 **아무도 공중의 다트를 뺏지 않기 때문에**
소유권 경합 화해(reconciliation)가 필요 없다 — 그래서 **예측(D4-2) 없이** 성립한다.

---

## ⛔ 아직 못 하는 것 (의향만 기록 · 개척 청구서)

| 원하는 것 | 막는 것 | 개척 비용(견적) | 근거 |
|---|---|---|---|
| **공중의 물체를 서로 뺏기(경합 투사체)** — 날아가는 공을 두 사람이 다투기 | client-side prediction + 화해 + authority 핸드오버. **던지기의 경계선**: 비경합까지가 ✅, 경합부터 ⛔ | 도달성 낮음 / **구조 공사 높음**(SYSTEMS 해동 + 예측 검증 인프라) | D4-2 [prediction-survey.md](prediction-survey.md): FishNet 4.6.12 예측은 XumNet 통과해 **도달 가능**하나 `PredictionManager`+프리팹별 `_enablePrediction`이 M-시리즈의 "SYSTEMS 무수정"을 깸 |
| 생성 에셋(임의 소품 모양·색·분위기) | text2img→3D 파이프라인 + 후처리(데시메이션·피벗·스케일·콜라이더) 라이브러리 공장 | 미착수(D3). **외형 질문 3종이 등장하면 착수 신호**(D6) | [design-directions](design-directions-2026-07.md#d3-생성-에셋-파이프라인--라이브러리-공장-모델) |
| 3인+ 동시(대칭 득점 포함), 실기기(Quest) 2클라 | 조인 인프라 안정화(트랩 J 멀티 게스트 flakiness / 트랩 K 콜드스타트 토큰), N명 플레이테스트 하네스 | 조인 재시도/워밍업 하네스 내장이 선행 과제 | HANDOFF §8, build-desktop-client §8 J/K |
| 실기기 던지기 손맛(실제 컨트롤러 스윙→릴리즈 velocity) | 실기기 + 사람 판정. 코드경로는 시뮬/실기기 동일(V1b 소스검증), 미감은 비검증 | V2 (사람이 판정자) | build-desktop-client §13 V2 준비 |

---

## 경계 한 줄 요약

**협업 룸에서 "물건을 만들고, 재고, 잡고, 옮기고, 던져 맞히고, 채팅하고, 점수내는" 직교 기능들의 조합은 ✅ 재조합으로 지어준다.
"실시간으로 하나의 물리 오브젝트를 서로 다투는" 경합(축구공 태클 같은)만 ⛔ — 그 선이 D4-2 예측 공사의 경계다.**
