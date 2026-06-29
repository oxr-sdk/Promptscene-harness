# PromptScene — Room Content Contract (규격)

> 목표: **XRCollabDemo에서 자연어 프롬프트로 룸을 합성하고, 잘 돌아가는지 구조적으로 검증.**
> 룸 = **반드시 있어야 하는 SYSTEMS(Core)** + **있어도/없어도 되는 FEATURES(Content)**.
> 이 문서는 스킬(`/promptscene`, `/RoomContent-*`)·런치패드·모든 콘텐츠 모듈이 공유하는 **단일 규격**이다.

---

## 0. 용어 — 업계 표준 분류

| 분류 | 의미 | 업계 용어 | 예 |
|---|---|---|---|
| **SYSTEMS** | 게임의 규칙·뼈대. 없으면 룸 자체가 안 돈다. | Core / Systems | 네트워크, 세션, 플레이어 스폰, 코어 레지스트리 |
| **FEATURES** | 선택 기능 모듈. 원하는 것만. 코어가 그 존재를 모름. | Content / Features / **Game Features(UE5)** / Modules / Plugins | Ruler, Memo, Laser … |

**판별 테스트:** *"이 모듈을 빼도 프로젝트가 안 깨지나?"* → 깨지면 **SYSTEMS**, 안 깨지면 **FEATURE**.

**핵심 원칙 (UE5 "Modular Game Features"와 동일):** 의존성은 **FEATURE → SYSTEMS 한 방향**. 절대 SYSTEMS가 특정 FEATURE를 컴파일타임에 참조하지 않는다(= 코어는 기능의 존재를 모른다). 그래서 기능은 런타임 on/off + 프로젝트 간 이식이 가능하다.

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
├── ===== FEATURES =====       ← Content. 선택. 각 모듈이 스스로 레지스트리에 등록.
│   └── Ruler                  RulerContent (IToggleableContent)
└── ===== _DYNAMIC =====       런타임 생성물 (아바타 Clone, RulerMeasurement, 메모 등)
```

규칙
- **SYSTEMS는 특정 FEATURE를 컴파일타임에 참조하지 않는다.** (구 RoomManager의 `public RulerManager …` 하드필드 = 안티패턴, 금지)
- FEATURE 모듈은 `===== FEATURES =====` 아래에 통째로 들어오며, 빠지면 SYSTEMS에 참조가 0개 → 빌드/런타임 안 깨짐.
- 횡단 작업(스케일 변경 시 정리 등)은 SYSTEMS가 레지스트리를 순회해 처리. 기능 추가 시 SYSTEMS 코드 수정 0.
- ⚠️ FishNet **씬 네트워크 오브젝트**(R-RoomServer, --PLAYER_SPAWNER)를 재배치하면 씬오브젝트 ID가 바뀐다 → 네트워크 빌드(Room.exe) 재빌드 필요.

---

## 2. 계약 인터페이스 (C# — 구현됨)

> 위치: `XRCollabDemo/Assets/PromptScene/Core/` (namespace `PromptScene.Core`).
> FEATURE 모듈은 이 계약에만 의존한다 (특정 기능끼리 직접 의존 금지).

```csharp
namespace PromptScene.Core
{
    public interface IRoomUserState { string MultiScaleName { get; } }
    public interface IInteraction { void AddClick(Action<RaycastHit> onClick); void RemoveClick(Action<RaycastHit> onClick); }
    public interface INetSpawn { bool IsNetworked { get; } GameObject Spawn(GameObject prefab, Vector3 p, Quaternion r); void Despawn(GameObject instance); }

    public interface IRoomCore { IRoomUserState User { get; } IInteraction Interaction { get; } INetSpawn Net { get; } RoomContentRegistry Contents { get; } }

    public interface IRoomContent { string Id { get; } void OnRegister(IRoomCore core); void OnUnregister(); }
    public interface IToggleableContent : IRoomContent { ContentMeta Meta { get; } bool IsEnabled { get; } void SetEnabled(bool on); }
    public interface IScaleScopedContent { void DespawnByScale(string multiScaleName); }

    [Serializable] public struct ContentMeta { public string DisplayName; public Sprite Icon; public string Category; public bool DefaultOn; public string[] MutuallyExclusive; }

    public class RoomContentRegistry { /* Register/Unregister, All, Toggleable, GetById<>, OnContentRegistered/Toggled, DespawnByScale */ }
}
```

구현 파일: `Core/Contracts.cs`, `Core/RoomContentRegistry.cs`, `Core/SimpleClickProvider.cs`(데스크톱 클릭→레이캐스트, IInteraction), `Core/RoomCore.cs`(MonoBehaviour, `RoomCore.Instance`).
첫 FEATURE: `Content/Ruler/RulerContent.cs` (`IToggleableContent`).

---

## 3. 콘텐츠 생명주기

1. 씬 로드 → **RoomCore(SYSTEMS) 초기화** (IRoomCore 서비스 + 빈 RoomContentRegistry).
2. 각 **FEATURE 루트가 Awake/Start**에서 `RoomCore.Instance.Contents.Register(this)` → `OnRegister(core)`로 서비스 확보.
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
- **Phase 4** `/RoomContent-*` 스킬화 + LLM 신규기능 생성 템플릿.
- **Phase 5** `/promptscene` 합성 스킬 + 하네스.

---

## 참고 (업계 표준 출처)
- Unity 씬 계층 정리: [Game Dev Beginner — How to structure your Unity project](https://gamedevbeginner.com/how-to-structure-your-unity-project-best-practice-tips/), [Unity Learn — Organizing your Scene](https://learn.unity.com/course/3d-game-kit-lite/tutorial/organizing-your-scene)
- 모듈러 아키텍처(Systems/Content/UI): [GDQuest — Modular Game Architecture](https://www.gdquest.com/library/modular_game_architecture/)
- Core-unaware-of-feature 플러그인 패턴: [Unreal Engine — Modular Game Features in UE5](https://www.unrealengine.com/en-US/blog/modular-game-features-in-ue5-plug-n-play-the-unreal-way)
