# PromptScene Core — carried spec (bootstrap source)

These 4 files are the **`PromptScene.Core` spec** the `assemble-room` skill carries so it can build a room on a
**fresh XumFlow checkout**. Core is NOT part of the committed XumFlow base — in the studio project it lives
**untracked** under `Assets/App/Scripts/ContentLogic/PromptScene/Core/` (local-only). A fresh clone has no
`PromptScene.Core.RoomCore`, so `build_skeleton.cs`'s `FindType("PromptScene.Core.RoomCore")` would fail hard.
The skill's **Phase 0 (Bootstrap Core)** copies these into the project first when the type is absent.

| File | Role |
|------|------|
| `Contracts.cs` | The interface surface: `IRoomCore`, `IRoomContent`, `IToggleableContent`, `IInteraction`, `INetSpawn`, `IRoomUserState`, `IEventBus`, `IScaleScopedContent`, `INetDespawnRequest`, `ContentMeta`. |
| `RoomCore.cs` | The thin core `MonoBehaviour`. `Awake` registers the 4 SYSTEMS services (`IInteraction`=SimpleClickProvider, `INetSpawn`=FishNetSpawn, `IRoomUserState`=local stub, `IEventBus`=in-process bus) + builds the registry. `RoomCore.Instance` is the global entry. |
| `RoomContentRegistry.cs` | Holds CONTENT modules; content self-registers via `Register`. `GetById` / `All` / `Toggleable` are what FEATURE/HUD lookups use. |
| `SimpleClickProvider.cs` | Desktop `IInteraction` (mouse → raycast) + `SubmitExternalRay` (XR world-click) + claim-based `SuppressWorldClick`. |

**Rules**
- **Verbatim copies** of the studio Core (namespace `PromptScene.Core`, compiles into `App.HotUpdate`, no separate
  asmdef). Dependencies: `UnityEngine`, `FishNet`, `XumNet`. If the studio Core is ever revised, re-sync these.
- **Never overwrite an existing local Core.** Phase 0 copies these ONLY when the type is absent — the studio project
  that already has Core (possibly newer) is left untouched.
- `.meta` files are intentionally NOT carried — Unity regenerates them on import. Core is referenced by **type name**
  (reflection in build_skeleton) and by `using PromptScene.Core` (compile-time), not by GUID, so fresh GUIDs are fine.
