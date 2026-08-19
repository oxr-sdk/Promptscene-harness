# 2클라 하네스 (studio, ParrelSync 클론용) — 소스 미러

studio 프로젝트(`XumFlow-studio/`)는 `.gitignore` 대상이라 여기 **미러**를 둔다.
**설치 위치(studio 안):** `Assets/PromptScene/Harness/Editor/`

| 파일 | 역할 |
|---|---|
| `QuickTestRoleBridge.cs` | `ClonesManager.IsClone()` → 클론이면 런타임에 shipped `QuickTestStarter` 의 `startAsServer`/`hostMode` 를 `false` 로 덮어 **A=host / B=client** 자동 분기. 원본에서는 아무것도 하지 않는다(fail-closed). |
| `CloneHarness.cs` | 클론 전용. 자동 Play(`.promptscene-autoplay`) · 파일 명령 채널(`.promptscene-cmd`) · 1초 프로브 로그. 원본에서는 `EditorApplication.update` 구독조차 안 한다. |

## 왜 `Editor` 폴더이고 asmdef가 없나 (바꾸지 말 것)

asmdef 없는 `Editor` 폴더 → **`Assembly-CSharp-Editor`** 에 들어간다. 이게 필수 조건이다:

- `QuickTestStarter` 는 **`Assembly-CSharp`** 에 있다 → asmdef를 붙이면 참조할 수 없다.
- `ParrelSync.ClonesManager` 는 Editor asmdef(autoReferenced) → 여기서만 같이 보인다.
- `App.HotUpdate`(ContentLogic) 안에 두면 **hot-update DLL = Smart-Deploy 배포물**에 하네스가 섞인다. 그래서 일부러 `Assets/PromptScene/` 최상위에 뒀다.

또한 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 는 **Editor 어셈블리에서도 발화한다**(실측 확인) — 그래서 런타임 컴포넌트도, shipped 씬 수정도 필요 없다.

## 절차

[build-studio-room.md](../../build-studio-room.md) **§6.6** (매 세션 절차 + 함정),
[xumflow-migration.md](../../xumflow-migration.md) **§17** (실측 기록 + 트랩표 9종).

## 나중에

트랩 J/K 실측을 포함한 이 절차 전체가 **`/multiplayer-check` 스킬의 SSOT 씨앗**이다.
그 스킬을 만들 때 이 폴더를 `promptscene/skills/multiplayer-check/assets/` 로 옮긴다.
