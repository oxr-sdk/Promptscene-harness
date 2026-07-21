# PromptScene — Room Content Contract (규격)

> **계약 버전: v0.2 (2026-07-09)** — 이후 템플릿·스킬은 "계약 vX 기준"을 명시할 수 있다.
> v0.2 변경 요지: SYSTEMS 서비스 열거 방식을 명시 프로퍼티(`User`/`Interaction`/`Net`)에서 제네릭 리졸버(`Contents` + `TryGet<T>`)로 교체(클린 브레이크). 서비스 추가 시 `IRoomCore` 무수정. 신규 "코어 승격 규칙" §추가.

> 목표: **XRCollabDemo에서 자연어 프롬프트로 룸을 합성하고, 잘 돌아가는지 구조적으로 검증.**
> 룸 = **반드시 있어야 하는 SYSTEMS(Core)** + **있어도/없어도 되는 FEATURES(Content)**.
> 이 문서는 스킬(`/compose-room`, `/scaffold-content`)·런치패드·모든 콘텐츠 모듈이 공유하는 **단일 규격**이다.

---

## 0. 용어 — 업계 표준 분류

| 분류 | 의미 | 업계 용어 | 예 |
|---|---|---|---|
| **SYSTEMS** | 게임의 규칙·뼈대. 없으면 룸 자체가 안 돈다. | Core / Systems | 네트워크, 세션, 플레이어 스폰, 코어 레지스트리 |
| **FEATURES** | 선택 기능 모듈. 원하는 것만. 코어가 그 존재를 모름. **서로도 모름.** | Content / Features / **Game Features(UE5)** / Modules / Plugins | Ruler, Chat, GrabbableProps, TargetProps, ScoreHud … |
| **COMPOSITIONS** | 게임모드/시나리오. **기능들을 "아는" 유일한 층** — 직교하는 FEATURE 여럿을 하나의 루프로 조율. | **GameMode(UE5)** / Scenario / Mediator | TargetShootoutMatch(과녁 점수전) |

**판별 테스트:** *"이 모듈을 빼도 프로젝트가 안 깨지나?"* → 깨지면 **SYSTEMS**, 안 깨지면 **FEATURE**. *"이 모듈이 서로 다른 FEATURE 여럿을 조율하나?"* → 그렇다면 **COMPOSITION**.

**핵심 원칙 (UE5 "Modular Game Features / GameMode"와 동일):** 의존성은 **COMPOSITIONS → FEATURES → SYSTEMS 한 방향(비순환)**.
- SYSTEMS는 특정 FEATURE를 컴파일타임에 참조하지 않는다(코어는 기능의 존재를 모른다).
- **FEATURE는 다른 FEATURE를 참조하지 않는다**(서로 모른다) — 상호작용이 필요하면 그 조율은 위층(COMPOSITION)이 맡고, FEATURE끼리는 **인프로세스 이벤트 버스**(§2 `IEventBus`)로만 느슨히 만난다.
- COMPOSITION은 자기가 조율하는 FEATURE의 **이벤트 타입**을 알 수 있으나(허용된 방향), 어떤 FEATURE가 실제 룸에 있는지는 **런타임 레지스트리 조회**로만 확인해 부재 시에도 안 깨진다.
그래서 기능은 런타임 on/off + 프로젝트 간 이식이 가능하고, 게임 루프는 전제(직교성)를 깨지 않고 층으로만 얹힌다. — 설계 결정: [design-directions-2026-07.md](design-directions-2026-07.md) D2.

> **COMPOSITIONS 층 상태(2026-07-21, ✅ 라이브 실증 완료):** 계약(이벤트 버스)·파일럿 FEATURE 2종(TargetProps/ScoreHud)·첫 COMPOSITION(TargetShootoutMatch)·MatchView **코드 + 구조 불변식 grep 검증(FEATURE↔FEATURE 참조 0) + 라이브 게임 루프 실증 완료**. 프리팹 2종(Target/MatchView)을 DefaultPrefabObjects에 C1 등록(auto-populate 16→18) → `ShootoutRoom_1`(씬 계층에 `===== COMPOSITIONS =====` 포함, spawner SceneId 할당) 조립 → Room.exe 재빌드(room.log `Online Scene: ShootoutRoom_1`) → **단일 클라 서버권위 게임 루프**(명중→집계 1→2→3→선취 3점 승자 P0→리셋→재판) + **2클라 점수 동기 파리티**(에디터 A[clientId 1] 명중 → **별도 데스크톱 프로세스 B**[clientId 0, 하나도 안 쏨]가 동일 서버권위 스코어보드 수신·승자 P1 일치·리셋, Chat 부재 시 `Contents.GetById("chat")` 런타임 조회로 자체표시 폴백해 무해)까지 라이브 판정. **버스 런타임**도 별도 스모크로 PASS(전달·멱등·예외격리·해지). 절차·증거: [build-desktop-client.md](build-desktop-client.md) §12, [HANDOFF.md](../../HANDOFF.md) §5.

**경계 문장:** 계약은 **모듈 경계**(등록·조회·토글·통지)에만 적용되며, 모듈 내부 구조는 규율하지 않는다. FEATURE 내부는 빠른 반복을 위해 자유롭게 작성해도 된다 — 격리가 보장되므로.

---

## 1. 씬 계층 규약 (Scene hierarchy convention)

> Unity 업계 관행: 빈 GameObject를 "폴더"로 써서 그룹화하고, `===== NAME =====` 헤더로 구분. 런타임 생성물은 `_DYNAMIC` 아래로 모은다. (순수 구분선만 두는 경우 `EditorOnly` 태그로 빌드 제외 — 단, 자식을 담는 폴더 부모에는 절대 EditorOnly 금지: 자식까지 빌드에서 빠진다.)

```
<Room>.unity
├── ===== SYSTEMS =====        ← Core. 항상 존재. 하네스가 존재/와이어 검증.
│   ├── Network                R-RoomServer (NetworkManager/RoomNetworkManager/RoomServerManager/DefaultScene), R-RoomClient, R-ConnectionToMaster
│   ├── Player                 --PLAYER_SPAWNER (XumPlayerSpawner + Player Prefab Catalog + Spawn Table)
│   └── RoomCore               RoomCore (IRoomCore 구현 + RoomContentRegistry) — 특정 기능을 모름
├── ===== ENVIRONMENT =====    Floor, Walls, Lighting, Main Camera
├── ===== UI =====             R-MasterCanvas (룸 HUD), Launchpad
├── ===== FEATURES =====       ← Content. 선택. 각 모듈이 스스로 레지스트리에 등록. 서로 참조 0.
│   ├── Ruler                  RulerContent (IToggleableContent)
│   ├── TargetProps            TargetProps (IToggleableContent) — 클릭 과녁, TargetHitEvent 발행
│   └── ScoreHud               ScoreHud (IToggleableContent) — ScoreChangedEvent 구독·표시
├── ===== COMPOSITIONS =====   ← Game modes. 선택. FEATURE들을 아는 유일한 층 (있을 때만 존재).
│   └── TargetShootoutMatch    TargetShootoutMatch (MonoBehaviour) — 과녁→점수→승자→리셋 조율
└── ===== _DYNAMIC =====       런타임 생성물 (아바타 Clone, RulerMeasurement, MatchView, Target 등)
```

규칙
- **SYSTEMS는 특정 FEATURE를 컴파일타임에 참조하지 않는다.** (구 RoomManager의 `public RulerManager …` 하드필드 = 안티패턴, 금지)
- **FEATURE는 다른 FEATURE를 참조하지 않는다.** 상호작용이 필요하면 `IEventBus`(§2)로 이벤트를 주고받고, 그 조율은 `===== COMPOSITIONS =====`의 게임모드가 맡는다. 신규 검증 신호: 파일럿 두 FEATURE 소스에 **상대 타입 참조 0**(grep). — COMPOSITIONS 층 도입: [design-directions-2026-07.md](design-directions-2026-07.md) D2.
- **COMPOSITION은 조율 대상 FEATURE의 이벤트 타입은 알아도(허용 방향), 어떤 FEATURE가 룸에 있는지는 런타임 레지스트리(`Contents.GetById`)로만 확인**해 부재 시에도 안 깨진다. COMPOSITION이 빠지면 각 FEATURE는 그대로 독립 동작(이벤트를 아무도 안 들을 뿐).
- FEATURE 모듈은 `===== FEATURES =====` 아래에 통째로 들어오며, 빠지면 SYSTEMS에 참조가 0개 → 빌드/런타임 안 깨짐. COMPOSITION도 `===== COMPOSITIONS =====` 아래에 통째로 들어오고 빠질 수 있다.
- 횡단 작업(스케일 변경 시 정리 등)은 SYSTEMS가 레지스트리를 순회해 처리. 기능 추가 시 SYSTEMS 코드 수정 0.
- ⚠️ FishNet **씬 네트워크 오브젝트**(R-RoomServer, --PLAYER_SPAWNER)를 재배치하면 씬오브젝트 ID가 바뀐다 → 네트워크 빌드(Room.exe) 재빌드 필요.
- ⚠️ **스크립트 단발 빌드는 FishNet SceneId를 자동 할당하지 못한다** (2026-07-13 발견). 한 번의 `script-execute` 안에서 `NewScene`→오브젝트 배치→`SaveScene`을 끝내면, FishNet의 자동 SceneId 생성 훅(`EditorSceneManager.sceneSaving`)이 **비결정적으로 건너뛰어져** 스포너 NetworkObject가 `SceneId=0 / IsSceneObject=false`로 저장될 수 있다. 증상이 **조용하다**: 룸 입장·로비 소멸(C3)·스포너 복제까지 정상인데 **아바타(Desktop(Clone))만 스폰되지 않는다**(전용 서버가 SceneId 없는 스포너로 플레이어를 못 띄움). "become a player" 로그는 MST 레벨이라 떠도 FishNet 스폰은 실패. **해법**: 저장 직전에 `Tools/Fish-Networking/Utility/Reserialize NetworkObjects`가 하는 일을 코드로 재현 — `NetworkObject.CreateSceneId(scene, force:true, out changed)` + 각 nob에 `ReserializeEditorSetValues(true,false)`(둘 다 `internal`→리플렉션) 후 `SaveScene`. 검증: 재오픈해서 스포너 `IsSceneObject==true`. compose-room의 `build_composed_room.cs`(`AssignFishNetSceneIds`)와 scaffold-content의 `build_feature_room.cs`에 반영됨. (assemble-room처럼 여러 `script-execute`에 걸쳐 씬을 열어두고 저장하는 흐름은 훅이 붙을 틈이 있어 우연히 통과하기도 했다 — 그래서 함정.)
- ⚠️ **VR 클라 주의**: `ENVIRONMENT`의 `Main Camera`는 데스크톱/에디터 검증용이다. **VR 클라(Quest)에선 유지되는 XR 리그 카메라와 충돌**해 화면 깜빡임+시점 고정을 유발한다("2 audio listeners" 경고 동반). 룸 씬의 Main Camera(Camera+AudioListener)를 **비활성화하고 태그를 Untagged**로 두면 `Camera.main`이 XR 리그로 잡힌다. ☞ `build-meta-client.md` §2.4-D

---

## 2. 계약 인터페이스 (C# — 구현됨)

> 위치: `XRCollabDemo/Assets/PromptScene/Core/` (namespace `PromptScene.Core`).
> FEATURE 모듈은 이 계약에만 의존한다 (특정 기능끼리 직접 의존 금지).

```csharp
namespace PromptScene.Core
{
    public interface IRoomUserState { string MultiScaleName { get; } }
    public interface IInteraction { void AddClick(Action<RaycastHit> onClick); void RemoveClick(Action<RaycastHit> onClick); }
    public interface INetSpawn { bool IsNetworked { get; } GameObject Spawn(GameObject prefab, Vector3 p, Quaternion r); void Despawn(GameObject instance); } // Despawn: Spawn으로 만든 인스턴스만. v0.2에서 FishNet 백엔드로 실체화(RoomCore.FishNetSpawn) — 아래 주석
    public interface INetDespawnRequest { void RequestServerDespawn(); } // 네트워크 스폰된 인스턴스가 구현: 비서버 클라가 서버에 디스폰 요청(ServerRpc). INetSpawn.Despawn을 제네릭하게 유지(SYSTEMS가 FEATURE를 모름). XumNet엔 Despawn 심볼 없음 → FishNet ServerManager.Despawn 경유.

    // COMPOSITIONS 층 활성화용 유일한 계약 추가(2026-07-21, design-directions D2). 인프로세스 타입드 이벤트 버스.
    // FEATURE는 타입으로 발행/구독만 하고 누가 듣는지 모름 → FEATURE 간 직접 참조 0을 유지한 채 COMPOSITION이 조율.
    // 메커니즘-비정책(§4.5): 이 프로세스 안에서만 라우팅하며 네트워크로 나가지 않는다. 복제가 필요한 값(예: 서버 권위 점수)은
    // 여전히 발행/구독자 프리팹 내부의 검증된 RPC(M1/M3 동형)로 나른다. 두 소비자(TargetProps 발행 / COMPOSITION 구독)로
    // 도입 시점에 rule-of-two(§4.5) 충족. 내장 서비스로 등록되고 core.TryGet<IEventBus>()로 조회 → IRoomCore는 무변경(v0.2).
    public interface IEventBus
    {
        void Publish<T>(T evt);                 // 구독자 전원에 동기 전달. 예외 던지는 핸들러는 로그+격리(전달·발행 중단 안 됨).
        void Subscribe<T>(Action<T> handler);   // (T,handler)당 멱등 — 같은 델리게이트 재구독해도 중복 호출 안 됨.
        void Unsubscribe<T>(Action<T> handler); // 구독 안 된 핸들러에도 안전.
    }

    // v0.2: 서비스는 프로퍼티로 열거하지 않고 TryGet<T>로 조회한다. 서비스가 늘어도 IRoomCore는 안 바뀐다.
    public interface IRoomCore
    {
        RoomContentRegistry Contents { get; }
        bool TryGet<T>(out T service) where T : class;   // 없으면 false — 호출측은 우아하게 대응(로그+비활성)
    }

    // 서비스 컨테이너는 구체 클래스(RoomCore)에만 있다 — FEATURE는 IRoomCore만 보므로 등록이 타입 수준에서 불가.
    public sealed class RoomCore : MonoBehaviour, IRoomCore
    {
        public void RegisterService<T>(T service) where T : class; // SYSTEMS만 호출(Awake에서 내장 3종 등록)
        public bool TryGet<T>(out T service) where T : class;
        public RoomContentRegistry Contents { get; }
    }

    public interface IRoomContent { string Id { get; } void OnRegister(IRoomCore core); void OnUnregister(); }
    public interface IToggleableContent : IRoomContent { ContentMeta Meta { get; } bool IsEnabled { get; } void SetEnabled(bool on); }
    public interface IScaleScopedContent { void DespawnByScale(string multiScaleName); }

    [Serializable] public struct ContentMeta { public string DisplayName; public Sprite Icon; public string Category; public bool DefaultOn; public string[] MutuallyExclusive; }

    public class RoomContentRegistry { /* Register/Unregister, All, Toggleable, GetById<>, OnContentRegistered/Toggled, DespawnByScale */ }
}
```

구현 파일: `Core/Contracts.cs`, `Core/RoomContentRegistry.cs`, `Core/SimpleClickProvider.cs`(데스크톱 클릭→레이캐스트, IInteraction), `Core/RoomCore.cs`(MonoBehaviour, `RoomCore.Instance`). `RoomCore`는 `Awake`에서 내장 서비스 4종(`IInteraction`=SimpleClickProvider, `INetSpawn`=**`FishNetSpawn`**, `IRoomUserState`, **`IEventBus`=인프로세스 EventBus**)을 `Type→object` 딕셔너리에 `RegisterService`로 담고, FEATURE는 `TryGet`으로 꺼낸다. `IEventBus` 추가로 `IRoomCore` 인터페이스는 **무변경**(서비스로만 추가 — v0.2 원칙, 코어가 god 인터페이스로 자라지 않음).
> **INetSpawn 실체화(2026-07-14, 계약 §4.5 메커니즘 승격 — SYSTEMS 해동 아님):** 파일럿의 `LocalNetSpawn`(IsNetworked=false)을 **`FishNetSpawn`**으로 교체. `Spawn`→`XumNetwork.Instantiate(nob,p,r,ownerConn)`(클라는 ServerRpc 왕복·**null 반환**), `Despawn`→서버면 `ServerManager.Despawn`·클라면 `INetDespawnRequest`. 네트워크 없으면 로컬 Instantiate 폴백. **주의(narrowness):** `Spawn(prefab,pos,rot)`은 **per-object 데이터를 못 나른다**(클라 null 반환이라 초기화 핸들도 없음). 그래서 결과값(예: 룰러 두 끝점)은 **FEATURE 프리팹의 내부 NetworkBehaviour**(SyncVar/RPC)가 나른다 — 계약 §1이 "모듈 내부는 자유"라 명시하므로 그 컴포넌트는 FishNet을 직접 써도 되고, 등록되는 `IToggleableContent`(RulerContent)는 여전히 `PromptScene.Core`만 참조한다. 실증: Ruler 결과값 공유(생성·제거 양방향 전파), [build-desktop-client.md](build-desktop-client.md) §6·§7.
**우아한 실패(graceful failure)**: FEATURE는 `OnRegister`에서 필요한 서비스를 `TryGet`으로 확보하되, 실패 시 `Debug.LogWarning($"[{Id}] required service {nameof(IInteraction)} not available — feature stays disabled")`를 남기고 **비활성 상태를 유지**한다(예외·NullReference 금지). 이 패턴은 Phase 4 템플릿의 표준 동작이다.
첫 FEATURE: `Content/Ruler/RulerContent.cs` (`IToggleableContent`).

---

## 3. 콘텐츠 생명주기

1. 씬 로드 → **RoomCore(SYSTEMS) 초기화**:
   - **1a. `Awake`**: RoomCore 초기화 + SYSTEMS 서비스 등록 완료 (빈 RoomContentRegistry + `RegisterService`로 내장 서비스 3종).
   - **1b. `Start`**: 각 FEATURE 루트가 자기등록 시작.
   - ⚠️ **순서 규약**: 서비스 등록 = `Awake`, 콘텐츠 자기등록 = `Start`. Script Execution Order 설정에 의존하지 말고 Awake/Start 단계 분리로만 레이스를 막는다(콘텐츠가 TryGet하는 시점에 서비스가 반드시 존재).
2. 각 **FEATURE 루트가 Start**에서 `RoomCore.Instance.Contents.Register(this)` → `OnRegister(core)`에서 `TryGet`으로 서비스 확보(실패 시 우아한 실패).
3. **런치패드**가 `registry.Toggleable`을 그려 `SetEnabled(true/false)`.
4. 스케일 변경/룸 종료 → SYSTEMS가 `registry`의 `IScaleScopedContent` 등 관심 콘텐츠에만 통지.
5. FEATURE 제거(룸에서 뺌) = 등록 안 됨 = SYSTEMS는 그 기능을 전혀 모름.

---

## 4. SYSTEMS(Core) 불변 계약 (Phase 0 검증됨 — 하네스 체크)

| # | 계약 | 검증 |
|---|---|---|
| C1 | 프리팹 컬렉션 일치 | 룸서버 NM `_spawnablePrefabs` == 클라 NM (= DefaultPrefabObjects) |
| C2 | 플레이어 스폰 = XumPlayerSpawner | `--PLAYER_SPAWNER`에 XumPlayerSpawner + Catalog(Desktop/UnityXR) + SpawnTable. FishNet 기본 PlayerSpawner 금지 |
| C3 | 씬 전환 | `R-RoomServer.DefaultScene`: `_onlineScene=<Room경로>`, **`_offlineScene=Client.unity`** (비면 로비 UI 안 사라짐) |
| C4 | 실행 토폴로지 | 서버=Master+Room exe / 에디터=Client+Room 동시 로드 |

---

## 4.5 코어 승격 규칙 (Core promotion rules)

> 언제 무엇이 SYSTEMS(코어)로 올라갈 수 있는가. `IRoomCore`가 god 인터페이스로 자라지 않도록 하는 관문.

- **mechanism-not-policy 테스트**: 코어에 들어올 수 있는 것은 **메커니즘**(전달·조회·통지의 *방법*: 입력 라우팅, 스폰 추상화, 레지스트리 통지 등)뿐이다. **정책**(게임 규칙·승리 조건·밸런스 등 "*무엇을* 하나")은 영원히 FEATURE에 남는다.
- **두 소비자 규칙 (rule of two consumers)**: 어떤 서비스의 코어 승격은 **두 번째** 기능이 그것을 필요로 할 때 수행한다. 첫 소비자 단계에서는 해당 FEATURE 내부에 지역적으로 둔다(성급한 일반화 방지).
- 이 두 규칙은 하네스의 **"SYSTEMS diff 0" 체크에 대한 공식 예외 절차**다. 즉, 위 테스트를 통과한 서비스 승격에 한해 SYSTEMS diff가 허용되며, 그 외의 SYSTEMS 변경은 여전히 위반이다.

---

## 5. 검증 하네스 (구조 안전성만; 취향은 사람/스크린샷)

- **SYSTEMS 체크**: Network/Player/RoomCore 존재 + C1~C4 만족.
- **FEATURES 체크**: 각 콘텐츠가 런타임에 `registry`에 등록됨 + `SetEnabled(true/false)` 예외 없음 + (토글형) 런치패드 메타 유효.
- **런타임 체크**: 컴파일 0에러 / 룸 등록 / 아바타 스폰·렌더 / 입장 시 Client 씬 언로드(로비 소멸).

---

## 6. 로드맵

- **Phase 0** ✅ 룸 베이스(구조+아바타+UI전환, 불변 C1~C4) — XRCollabDemo 검증 완료.
- **Phase 1** ✅ 계약 규격(이 문서).
- **Phase 2** ✅ Ruler를 계약 위에 **클린 재구현**해 XRCollabDemo에 편입(파일럿). 자기등록·토글·측정 검증.
- **Phase 2.5** ✅ 씬 계층 표준화(SYSTEMS/ENVIRONMENT/UI/FEATURES/_DYNAMIC).
- **Phase 3** 런치패드 UI (registry → 아이콘 그리드 → SetEnabled).
- **Phase 4** ✅ `/scaffold-content` 스킬화 + LLM 신규기능 생성 템플릿 (`skills/scaffold-content/`). Ruler 패턴을 `FeatureContent.cs.template`로 동결(§2·§3 배관 고정, 기능 로직만 채움) → RoomCore 룸에 얹어 §5 FEATURES 체크를 **라이브 네트워크 룸**에서 집행. **라이브 검증됨**(2026-07-09, `ClickSpawnerContent`로 §5+§6.5 양쪽 PASS).
- **Phase 5** ✅ `/compose-room` 합성 스킬 + 합성 하네스 (`skills/compose-room/`). 자연어→기능 선택(PARSE·RESOLVE·PLAN)과 부품 오케스트레이션+최종 판정(EXECUTE·VERIFY)만 신규 담당하고, 씬 조립은 `assemble-room`, RoomCore+FEATURES 배치는 `scaffold-content`를 **참조 호출**(절차 복제 금지). PLAN은 `composition-plan.json`(§7)으로 박제하고 unresolved/conflict 시 정지. **라이브 검증됨**(2026-07-13, "측정 도구 있는 룸" → Ruler → `ComposedRoom_1` → §6.5 4신호 + §5 COMPOSITION 모두 PASS). 이 과정에서 **스크립트 단발 빌드의 FishNet SceneId 미할당 함정**(§1)을 발견·수정.

---

## 7. 합성 계획 스키마 (`composition-plan.json`) — compose-room

> compose-room(Phase 5)이 **실행 전에** 자연어 요청의 해석 결과를 박제하는 기록. 목적: 실패 시 **계획 문제(선택이 틀림)와 실행 문제(조립·스폰이 틀림)를 분리**하고, 합성을 diff 가능하게 남긴다. 위치: `skills/compose-room/composition-plan.json`(실행마다 덮어씀).

```json
{
  "roomName": "ComposedRoom_1",   // 사용자가 지정 없으면 ComposedRoom_N
  "mode": "create",                // v1 고정. 필드만 예약(향후 "extend" 등)
  "request": "측정 도구 있는 룸 만들어줘",  // 원문(추적용, 선택)
  "features": [                    // RESOLVE가 카탈로그(Content/*.cs의 ContentMeta)와 매칭한 결과
    { "id": "ruler", "class": "RulerContent", "params": {} }
  ],
  "unresolved": [],                // 카탈로그에 매칭 안 된 요청 능력(비면 진행 가능)
  "conflicts": []                  // MutuallyExclusive 충돌 쌍(비면 진행 가능)
}
```

규칙
- **카탈로그는 소스에서 구성** — 별도 manifest 파일 신설 금지(두 소비자 규칙 §4.5). `id`는 각 콘텐츠의 `Id`, `class`는 클래스명, 매칭 근거는 `ContentMeta`의 `DisplayName`/`Category`/`MutuallyExclusive`.
- **`unresolved` 또는 `conflicts`가 비어있지 않으면 EXECUTE 금지 — 사람에게 보고 후 대기.** scaffold-content 자동 연쇄 금지("Memo가 카탈로그에 없음, scaffold할까?" 형태로 질문).
- `features`가 EXECUTE에 그대로 넘어간다: `class[]` → `build_composed_room.cs`의 `FEATURE_TYPES`, `id[]` → `verify_composition.cs`의 `FEATURE_IDS`.

## 참고 (업계 표준 출처)
- Unity 씬 계층 정리: [Game Dev Beginner — How to structure your Unity project](https://gamedevbeginner.com/how-to-structure-your-unity-project-best-practice-tips/), [Unity Learn — Organizing your Scene](https://learn.unity.com/course/3d-game-kit-lite/tutorial/organizing-your-scene)
- 모듈러 아키텍처(Systems/Content/UI): [GDQuest — Modular Game Architecture](https://www.gdquest.com/library/modular_game_architecture/)
- Core-unaware-of-feature 플러그인 패턴: [Unreal Engine — Modular Game Features in UE5](https://www.unrealengine.com/en-US/blog/modular-game-features-in-ue5-plug-n-play-the-unreal-way)
