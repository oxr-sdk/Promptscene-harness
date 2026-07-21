# FishNet 예측(prediction) 견적 — capability-map 선행 조각 (P-G0)

> **성격:** 견적서다. **구현 착수 금지** — D4-2(client-side prediction)에 실제로 손대기 전, "감쌀 수 있는가 / 새로 지어야 하는가"와 "비용 등급"을 사전 판정하기 위한 소스 정찰. design-directions D4-2의 착수 전 확인 사항("FishNet의 prediction API가 XumNet 래퍼를 통과해 사용 가능한지 PackageCache 소스로 확인")에 대한 답.
>
> **방법:** `oxr-source-scout`가 로컬 PackageCache/DeepChairProject 소스를 읽어 시그니처·구조·포인터를 수집(2026-07-21), **메인이 두 하중 주장(② XumNet 무개입, 버전)을 소스에서 재검증**. 포인터의 `@해시` 접미사는 환경마다 다르니 항상 glob으로 찾을 것(`Library/PackageCache/*fishnet*/…`).
>
> **판정 한 줄:** 예측은 **XumNet을 통과해 직접 도달 가능**(감쌀 것도 막을 것도 없음) — 이 점에서 M1/M2/M3과 동형. **그러나** M-시리즈와 달리 **SYSTEMS 수준 배선(NetworkManager에 PredictionManager) + 프리팹별 `_enablePrediction` + 재직렬화 상태 정의 확정**을 요구하므로, "FEATURE-지역 감싸기"만으로 끝나지 않는다. 결국 design-directions D4-2의 "얼려둔 SYSTEMS를 녹여 재검증하는 대공사"라는 성격이 **소스로 확증**됨.

## 0. 버전 (재검증됨)

| 패키지 | 버전 | 비고 |
|---|---|---|
| FishNet (`com.firstgeargames.fishnet`) | **4.6.12** | 실제 캐시 해석값 |
| XumNet (`com.oxr-sdk.xumnet`) | **0.5.0** | manifest가 FishNet **4.6.17** 의존 선언 → **버전 불일치 플래그**: 매니페스트 요구(4.6.17) ≠ 실제 해석(4.6.12). 아래 시그니처는 실제 컴파일될 4.6.12 기준. 패키지 갱신 시 재확인 필요. |

**컴파일 스위치 주의:** FishNet엔 `#if !FISHNET_STABLE_REPLICATESTATES` 토글이 있다. **기본(심볼 미정의)** = 플래그 기반 `ReplicateState`(`[Flags] Invalid/Ticked/Replayed/Created`). 심볼을 정의하면 enum 멤버·네임스페이스가 통째로 바뀐다(비플래그 `CurrentCreated/ReplayedCreated/…`, `FishNet.Object`로 이동). **예측 코드 작성 전 프로젝트의 scripting define symbols를 먼저 확정할 것** — `state.IsFuture()`/`ContainsTicked()`가 어느 분기냐에 조용히 의존한다.

## 1. FishNet 예측 API의 실제 모양 (①)

포인터는 `Library/PackageCache/com.firstgeargames.fishnet@<hash>/` 기준 상대경로.

### 1a. 데이터 인터페이스
- `IReplicateData` / `IReconcileData` — `Runtime/Object/Prediction/Interfaces.cs`(각 ~L3, ~L23). 둘 다 멤버 3개로 동일: `uint GetTick(); void SetTick(uint); void Dispose();`. 두 개의 **별개** 인터페이스다(replicate 구조체는 앞, reconcile 구조체는 뒤를 구현). `_tick`은 런타임이 채움, 사용자는 저장/반환만. `Dispose()`는 비값형 필드 정리용(값형만이면 비워도 됨).

### 1b. 어트리뷰트
- `[Replicate]` / `[Reconcile]` — `Runtime/Object/Prediction/Attributes.cs`(~L10, ~L16). 둘 다 `AttributeTargets.Method`, 메서드당 1개. 마커일 뿐 실제 배관은 codegen(`CodeGenerating/Processing/Prediction/PredictionProcessor.cs`)이 생성 — 표시된 메서드를 내부 `Replicate_Current`/`Reconcile_Current`로 재작성.

### 1c. PredictionManager (NetworkManager 오브젝트에 붙는 컴포넌트)
- `Runtime/Managing/Prediction/PredictionManager.cs`(~L25, sealed MonoBehaviour). 공개 표면(요약):
  - 이벤트(전부 `(uint clientTick, uint serverTick)`): `OnPreReconcile/OnReconcile/OnPostReconcile/OnPrePhysicsTransformSync/OnPostPhysicsTransformSync/OnPostReconcileSyncTransforms/OnPreReplicateReplay/OnPostReplicateReplay`. (`OnReplicateReplay`는 internal.)
  - 상태(get): `IsReconciling`, `ClientReplayTick`, `ServerReplayTick`, `ClientStateTick`, `ServerStateTick`.
  - 설정: `byte StateInterpolation`(직렬화 `_stateInterpolation`, 기본 2, 0–5), `ReplicateStateOrder StateOrder` + `SetStateOrder(...)`, `SetMaximumServerReplicates(byte)`/`GetMaximumServerReplicates()`(기본 15, 2–255), `GetReconcileStateTick(bool)`.
  - `enum ReplicateStateOrder { Inserted, Appended }`(`Runtime/Managing/Prediction/StateOrder.cs`) — 기본 **Appended**(성능 지향/매틱 화해 안 함), Inserted(정확도 지향/매틱 화해).
- **주의:** reconcile 재생 물리(`Physics.Simulate`)는 TimeManager PhysicsMode==TimeManager일 때만 돈다. `RedundancyCount`/`MaximumPastReplicates` 등은 internal(FEATURE 어셈블리에서 접근 불가).

### 1d. NetworkBehaviour 예측 훅 (사용자가 오버라이드/호출)
- `Runtime/Object/NetworkBehaviour/NetworkBehaviour.Prediction.cs`:
  - `public bool IsBehaviourReconciling { get; internal set; }` — 읽기 전용.
  - `public virtual void CreateReconcile()` — **오버라이드**해 reconcile 데이터를 만들고 `[Reconcile]` 메서드를 그 안에서 호출.
  - `public virtual void ClearReplicateCache()` — 텔레포트 후 등.
  - `Reconcile_Reader<T>` 계열은 public이지만 codegen용(손호출 아님).

### 1e. 예측 가능 NetworkBehaviour의 요구 구조 (데모로 검증됨)
데모 `Demos/Prediction/CharacterController/Scripts/CharacterControllerPrediction.cs` 기준:
1. **replicate 구조체** `: IReplicateData`.
2. **reconcile 구조체** `: IReconcileData`.
3. **`[Replicate]` 메서드**, 시그니처 정확히 `void M(TReplicate data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)` — 2·3번째 인자와 기본값이 codegen 필수.
4. **`[Reconcile]` 메서드** `void M(TReconcile data, Channel channel = Channel.Unreliable)`.
5. **`CreateReconcile()` 오버라이드** + 그 안에서 reconcile 호출(서버·클라 모두 빌드).
6. **틱 구동:** `TickNetworkBehaviour`(`Runtime/Utility/Template/TickNetworkBehaviour.cs`, `SetTickCallbacks(TickCallback.Tick)`로 `TimeManager.OnTick` 자동구독) 상속 + `TimeManager_OnTick()` 오버라이드해 매틱 replicate→`CreateReconcile()`. **대안:** 평범한 `NetworkBehaviour`가 `TimeManager.OnTick`에 직접 구독해도 됨(템플릿은 편의).
7. **NetworkObject에서 예측 활성화:** `NetworkObject._enablePrediction`(public `EnablePrediction`) — `Runtime/Object/NetworkObject/NetworkObject.Prediction.cs`. `PredictionType`(Other/Rigidbody/Rigidbody2D, 기본 Other) + `_enableStateForwarding`(기본 true). **`_enablePrediction`가 false면 NetworkObject 예측 초기화가 전부 early-return** — 즉 파이프라인 자체가 안 돈다.

## 2. XumNet의 예측 태도 — 직접 도달 가능 (감싸지도 막지도 않음) (②, 재검증됨)

- **재검증(메인):** XumNet Runtime의 `.cs` 전체에서 `Replicate|Reconcile|Predict` 토큰 = **0건**. 매치는 오직 `.prefab` 3개(`Runtime/Prefabs/XumNetwork.prefab`, `Runtime/Prefabs/Resources/XumNetDiagnostics/DiagnosticsPlayerCube.prefab`, `DiagnosticsTarget.prefab`)의 **FishNet `NetworkObject` 직렬화 필드**(`<PredictedSpawn>`, `<PredictedOwner>`, `_enablePrediction: 0`)일 뿐 — XumNet 코드가 아님.
- `XumView`(`Runtime/XumView.cs`, ~L40)는 `public partial class XumView : NetworkBehaviour` — **FishNet NetworkBehaviour를 직접 상속**. 감싸는 건 문자열키 리플렉션 **RPC** 디스패치(`RPC(string, RpcTarget, …)`)와 Photon식 소유권/식별(`IsMine/Owner/OwnerId/ViewId`)뿐. 오버라이드는 `OnStartServer/OnStartClient/OnStopClient/OnValidate` + private `Awake`(RPC 스캔) — **예측 파이프라인은 전혀 건드리지 않음**(`CreateReconcile` 오버라이드 없음, 틱 구독 없음).
- **결론:** 예측은 **M1/M2/M3이 ServerRpc/ObserversRpc/소유권에 도달한 것과 똑같이** 곧장 도달 가능. "XumPredictedView" 같은 예측 래퍼는 **존재하지 않음**. 예측 원하는 프리팹은 같은 NetworkObject 위에 **자기 소유의** `[Replicate]/[Reconcile]` NetworkBehaviour(또는 `TickNetworkBehaviour`)를 별도 컴포넌트로 얹으면 됨 — XumView의 RPC 스캔은 형제 컴포넌트를 간섭 안 함.
- **함정:** XumNet 기본 프리팹의 NetworkObject는 `_enablePrediction: 0`으로 출고. 예측은 per-NetworkObject라 프리팹에서 켜야(+`PredictionType` 설정) 파이프라인이 초기화됨. 이건 코드 한계가 아니라 프리팹 배선 단계.

## 3. 지연·패킷로스 시뮬레이션 — FishNet 내장 있음 (③, 검증 인프라)

- `LatencySimulator`(`Runtime/Managing/Transporting/LatencySimulator.cs`, ~L13) — `[Serializable]`, **컴포넌트가 아니라 `TransportManager`의 직렬화 필드**(`TransportManager.cs` ~L78/L82로 노출; TransportManager는 NetworkManager 오브젝트의 컴포넌트). 출처 주석: TiToMoskito/FishyLatency.
- 런타임 세터(+인스펙터 직렬화): `SetEnabled(bool)`, `SetLatency(long ms)`(0–60000, 호스트는 2배), `SetPacketLoss(double 0–1)`, `SetOutOfOrder(double 0–1, **unreliable 채널만**)`, `_simulateHost`(기본 true).
- **주의:** reliable 채널은 실제로 드롭/재정렬되지 않음(드롭 시 재전송 모델로 ~30% 지연만 가산). 관측 가능한 손실/재정렬 효과는 **unreliable** 트래픽에서만 — 그런데 예측 상태 갱신·replicate/reconcile RPC가 기본 `Channel.Unreliable`이라 **예측 검증에 그대로 적합**. per-NetworkManager라 2클라 로컬 하네스에서 양쪽을 독립 설정. 별도 `NetworkTrafficSimulator`는 없음(주입점은 이 하나).

## 4. 판정 초안 — (a)감싸기 vs (b)신규 + 비용 등급

**메커니즘 도달성: (a) 감싸기 가능.** XumNet이 예측을 막거나 가로채지 않으므로, 예측 로직 자체는 M1/M2/M3처럼 **FEATURE 프리팹 내부의 NetworkBehaviour**에 `[Replicate]/[Reconcile]`로 얹는 "지역 감싸기"로 시작할 수 있다. 신규 플랫폼 계층을 짓지 않아도 된다.

**그러나 구조 비용: M-시리즈보다 한 등급 위 (SYSTEMS 접촉 불가피).** 아래가 M1/M2/M3의 "SYSTEMS·계약 무수정"을 깨는 지점:
1. **PredictionManager를 NetworkManager 오브젝트에 추가** — NetworkManager는 SYSTEMS(룸 R-RoomServer/R-RoomClient 프리팹은 XumLobby PackageCache=읽기전용). 씬 인스턴스에 컴포넌트를 얹는 배선은 가능하나, 이는 M-시리즈가 지킨 "네트워크 매니저 무수정"을 벗어남 → 하네스의 SYSTEMS-diff 규칙에 걸림(승격 절차 필요).
2. **프리팹별 `_enablePrediction` ON + `PredictionType` 설정** — 프리팹 배선 단계(Room.exe 재빌드 동반 가능).
3. **`FISHNET_STABLE_REPLICATESTATES` 정의 확정** — 코드 작성 전 선행.
4. **정확한 codegen 시그니처**(replicate/reconcile 구조체 + 4단계 메서드 규약 + `CreateReconcile` + 틱 구동) — 오타 1개로 codegen 실패.
5. **검증 인프라**: `LatencySimulator`(있음, 낭보) 기반 지연/손실 시나리오 + 2클라 경합 판정 하네스(연속 경합이 예측의 존재 이유).
6. **버전 불일치 리스크**(4.6.17 요구 vs 4.6.12 실물)가 잠복.

**비용 등급:** 도달성 **낮음(직접 가능)** · 구조 공사 **높음**(첫 SYSTEMS 해동 대상 + 검증 하네스 신설). ⇒ **세션 크기 파일럿이 아니라 프로젝트급 공사.** design-directions **D4-2 "대공사, 보류"** 판정이 소스로 확증됨 — 단, "보류" 이유가 *불가능*이 아니라 *SYSTEMS 배선 + 검증 인프라 비용*임이 이제 구체화됨.

**착수 트리거(권고):** ① `_enablePrediction`/`FISHNET_STABLE_REPLICATESTATES` 확정 → ② PredictionManager의 SYSTEMS 승격을 계약 §4.5(mechanism-not-policy) 관문에 통과시킬지 결정 → ③ `LatencySimulator` 기반 2클라 경합 판정 하네스 선행 → 그 다음에야 예측 FEATURE 프리팹.

---
*관련: design-directions-2026-07.md D4-2, grab-ownership-survey.md(감싸기 판정 선례), HANDOFF §8.*
