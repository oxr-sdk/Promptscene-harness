# 네트워크 메시징 계약 승격 검토서 (M3 후속) — 판정: 보류

> 2026-07-20 작성. M3(채팅) 완료로 **FEATURE 내부 네트워크 사용이 3건**이 된 시점에서, "이 수요들이 같은 모양인가 —
> `INetMessaging` 같은 계약(IRoomCore 서비스)으로 승격할 때가 됐는가"를 판정한다. **검토서까지만** — 승격 실행은 하지 않는다.
> 판정 기준은 D4/G0a부터 일관 적용해 온 **두-소비자 규칙**: 같은 모양의 소비자가 실제로 2개 생겼을 때만 계약을 승격한다
> ([grab-ownership-survey.md](grab-ownership-survey.md) 판정과 동일 원칙).

---

## 판정 (결론 먼저)

**보류.** 세 소비자는 "프리팹 내부 NetworkBehaviour"라는 **구현 패턴**을 공유할 뿐, **계약으로 뽑을 수 있는 같은 모양의
수요**가 아니다. 순수 메시지-버스 수요는 채팅 1건뿐 — 두-소비자 규칙 미충족.

## 현황: FEATURE 내부 네트워크 사용 3건

| # | 소비자 | 네트워크 기계 | 수요의 본질 |
|---|---|---|---|
| M1 | `RulerMeasurementView` | `ServerRpc` → `ObserversRpc(BufferLast=true)` | **오브젝트 결합 상태 전파** — 스폰된 측정 오브젝트 1개의 per-object 데이터(두 끝점). BufferLast로 late-join 백필이 **필요**(늦게 온 클라도 선을 봐야 함) |
| M2 | `GrabbableView` | `XumView.RequestOwnership`(SDK) + client-auth `NetworkTransform` | **소유권+transform 동기화** — 자기 RPC가 거의 없음(디스폰 1개). 메시징이 아예 아님 |
| M3 | `ChatChannelView` | `ServerRpc(RequireOwnership=false)` → `ObserversRpc` | **오브젝트 무관 방송 버스** — 페이로드(발신자, 텍스트)를 전원에게. BufferLast는 오히려 **유해**(마지막 1건만 남아 "이력처럼 보이는 거짓말") |

## 왜 같은 모양이 아닌가

1. **전달 의미론이 상충한다.** Ruler는 "새 옵서버에게 마지막 상태를 백필"이 요구사항이고(BufferLast), 채팅은 그게 금지사항이다
   (부분 이력은 무이력보다 나쁨 — v1 정직 계약). 하나의 `INetMessaging.Broadcast(topic, payload)`로 덮으면 이 차이가 옵션
   플래그로 숨는데, 그 플래그의 올바른 값은 기능의 도메인 지식이다 — 계약이 정책을 삼키는 모양새(메커니즘-비정책 위반).
2. **Grabbable은 표에 있을 뿐 수요가 아니다.** 소유권 코어는 이미 패키지 레이어(`XumView`)에 있고 G0a에서 "계약 승격 없이
   FEATURE 지역 사용으로 충분" 판정이 실증됐다. 메시징 계약이 생겨도 GrabbableView는 소비자가 되지 않는다.
3. **순수 버스 수요는 채팅 1건.** "임의 페이로드를 전원에게 방송"이 필요한 소비자는 ChatChannelView 하나다. 두-소비자 규칙
   미충족 — Ruler를 버스 위로 옮기는 건 승격을 정당화하기 위한 역방향 리팩터링이지 수요가 아니다.
4. **패턴 복제 비용이 아직 싸다.** 세 뷰의 공통 보일러플레이트는 `INetDespawnRequest` 구현(4줄)과 RPC 애트리뷰트 2줄 정도.
   복제 3벌의 유지비 < 계약 1개의 고정비(문서·하네스·모든 룸의 호환성 보증).

## 승격 트리거 (이걸 다시 여는 조건)

- **두 번째 순수 버스 소비자**가 실제로 생길 때: 이모지 리액션/핑·신호/투표/상태 알림처럼 "오브젝트와 무관한 페이로드 방송"
  기능. 그때 ChatChannelView의 상행+하행 코어를 `INetMessaging`으로 추출하고, **전달 의미론(백필 여부)은 옵션 플래그가 아니라
  계약 분리**(예: 버스 계약 vs 상태-전파 계약)로 다룬다.
- **D2 COMPOSITIONS(기능 간 통신)** 착수 시: 기능 A→기능 B 통신이 생기면 그건 방송 버스와 다른 모양(수신자 지정)이므로 별도
  검토. HANDOFF §8의 D2 착수 조건("서로 통신해야 하는 기능 2개")과 동일 시점.

## 승격 전까지의 지침

새 FEATURE가 네트워크가 필요하면 **M1/M2/M3 동형 패턴을 복제**한다: 프리팹 루트 NetworkObject + FEATURE 내부
NetworkBehaviour(RPC는 뷰 안에), 등록 콘텐츠는 PromptScene.Core만 참조, C1(DefaultPrefabObjects) + Room.exe 재빌드.
발신자 신원이 필요하면 `NetworkConnection sender = null` 서버 주입 패턴(ChatChannelView.CmdSend, 위조 불가)을 쓴다.
복제가 4벌째가 되면 이 문서를 다시 연다.
