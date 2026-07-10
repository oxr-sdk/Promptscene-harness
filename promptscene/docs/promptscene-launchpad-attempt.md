# PromptScene — 런치패드/네트워크 기능 시도 회고 (2026-07-07~08, 보류)

> 이 문서는 **보류(shelved)된 시도**의 기록이다. 아래 작업물은 사용자 요청으로 **되돌려 제거**했다
> (퀄리티가 기대에 못 미쳐 방향을 다시 고민하기로 함). 다시 손댈 때 참고하라고 **무엇을 했고, 무엇이
> 실제로 증명됐고, 무엇이 부족했고, 기술적으로 배운 것**을 남긴다.

## 하려던 것
"사용자 니즈에 맞춰 룸 UI/기능을 이쁘게 꾸며주기" — 그 첫 걸음으로 맥북 런치패드식 UI + 토글 기능을
계약(SYSTEMS/FEATURES) 위에 얹고, 궁극적으로 "마피아 게임 만들어줘" 같은 대화형 생성으로 확장하려 함.

## 만들었던 것 (전부 제거함)
- `/build-custom-room` 스킬 (`.claude/skills/build-custom-room/`): working room + 선택 FEATURES 조립·검증.
- `CustomRoom_1.unity`: 위 실험을 쌓은 룸 씬.
- `LaunchpadUI.cs` (`Assets/PromptScene/UI/`): 처음엔 코너 텍스트 패널 → 맥북식 **아이콘 그리드 런처**로
  재작성. 타일 클릭 → 항목 창(설정/룰러). **통합 셸**: 스톡 룸 HUD(`R-MasterCanvas`의 RoomHudView/
  ClientInformationView)를 CanvasGroup로 투명 처리(살려둠)하고 그 버튼 onClick 호출 + IP/ID 텍스트를
  리플렉션으로 읽어 흡수(재구현 0). 설정 창=IP·플레이어ID·전체음량·플레이어목록·방나가기.
- `Contracts.cs`에 `ContentCommand`+`IContentCommands` 추가, `RulerContent`이 "측정 지우기" 커맨드 선언.
- `SimpleClickProvider.cs`를 레거시 `Input` → 신형 Input System(`Mouse.current`)으로 변경.
- `MissionProgressContent.cs` (`Assets/PromptScene/Content/Mission/`): **서버권위 네트워크 기능 파일럿**
  (SyncVar<int> + [ServerRpc]) — 마피아의 공유 상태 원시요소.

## 실제로 증명된 것 (정직하게)
- 1인(에디터 클라)+서버로: 조인·아바타 스폰·로비 소멸·WASD, 런치패드 실제 클릭, 룰러 측정/지우기,
  설정(볼륨/플레이어목록 팝업 재사용/방나가기 실제 퇴장), 미션 SyncVar가 **서버↔에디터클라(별개 프로세스)**
  동기화(0→3 COMPLETE) — **로그/스냅샷/스크린샷으로 확인**.

## 부족했던 것 (보류 사유)
- **멀티플레이가 눈으로 확인 안 됨**: 끝까지 **1명 + 서버**만 테스트. 두 번째 실제 플레이어를 넣은 적 없음
  → "다른 사람이 로딩되어 서로 보이는" 그림이 없음. (스탠드얼론 클라는 자동 조인을 안 해서 스크립트 구동 불가.)
- **게임이 "보이지" 않음**: 미션은 **로그로만** 확인. 화면에 뜨는 진행도/결과가 없어 "게임"이라 하기 어려움.
- **미감**: 앱 아이콘식 모노그램 타일 수준. "이쁘게"와 거리가 있음(자동 미감 판정 불가, 사람/비전 루프 필요).

## 기술적으로 배운 것 (다시 할 때 유효)
1. **입력**: 이 프로젝트 `ProjectSettings/activeInputHandler=1` = **신형 Input System 전용**.
   - uGUI 클릭: EventSystem에 `InputSystemUIInputModule` 필요(+런타임 AddComponent 시 액션 비면
     `AssignDefaultActions()`). 레거시 `StandaloneInputModule`은 클릭 무반응(버튼은 보이는데 안 눌림).
   - 월드 클릭: 레거시 `UnityEngine.Input`은 예외 → `Mouse.current.leftButton.wasPressedThisFrame` +
     `Mouse.current.position.ReadValue()`.
2. **네트워크 씬 오브젝트**(NetworkObject를 씬에 배치/재배치): FishNet **SceneId 재직렬화 필수**
   (`NetworkObject.CreateSceneId(scene,force:true,out changed)` = 메뉴 Tools/Fish-Networking/Utility/
   Reserialize NetworkObjects) → **MCP scene-save(실제 파이프라인)** → **Room.exe 재빌드**. 안 하면 조인·
   "become a player"는 정상인데 서버가 스폰 안 함 → 아바타/오브젝트 안 뜸(`IsSceneObject=false`/ObjectId=65535).
   씬 오브젝트는 DefaultPrefabObjects 등록 불필요(SceneId로 서버가 스폰).
3. **실행 순서(전용 서버)**: `MasterAndSpawner.exe` → (6s) `Room.exe` → 그다음 에디터 Play. **서버 안 켜고
   Play만 누르면 로비에서 안 넘어가고 아무도 안 뜸** — "사람 로딩 안 됨"의 흔한 원인.
4. **네트워크 기능 패턴**(이 프로젝트 XumNet/FishNet, `A_DisplayName` 참조): `readonly SyncVar<T> _x = new
   SyncVar<T>(init)` (`.Value`/`.OnChange(prev,next,asServer)`), 클라→서버 `[ServerRpc(RequireOwnership=false)]`.
5. **통합 셸 요령**: 스톡 UI를 지우지 말고 CanvasGroup(alpha0/interactable·blocksRaycasts false)로 숨겨
   살려두면 버튼 onClick 호출·텍스트(리플렉션, TMP라 `GetProperty("text",typeof(string))`) 재사용 가능.
6. 스톡 룸 HUD는 전부 `R-MasterCanvas` 안(RoomHudView=Players list/Leave game/Esc-to-pause,
   ClientInformationView=IP+플레이어ID, PlayersListView 팝업, 각종 다이얼로그).

## 다시 고민할 것 (열린 질문)
- 런치패드 아이콘 그리드가 맞는 은유인가? 방마다 UI 컨셉이 다를 텐데 "이쁘게"의 기준·범위는?
- **멀티플레이를 실제로 보이게** 하려면: auto-join하는 2번째 클라 빌드(또는 사람이 2번째 클라 수동 실행).
- 게임 로직 "정확성"은 구조 하네스로 안 됨 → **시뮬 플레이어 N명 플레이테스트 하네스**가 필요(프론티어).
- 시각화: 진행도/역할/결과를 화면에 띄우는 게 먼저(로그 말고).
- 미감: 스크린샷 → 비전/사람 승인 루프 없이는 "이쁘게" 자동 달성 불가.

## 정리 시 남긴 것 / 지운 것
- 지움: 위 스킬·씬·`LaunchpadUI`·`MissionProgressContent`, `Contracts`/`RulerContent`/`SimpleClickProvider`의
  이번 세션 변경, EditorBuildSettings의 CustomRoom_1 등록, contract 문서·메모리의 관련 추가분.
- 건드리지 않음: 기존 PromptScene 코어(Contracts 원형/RoomCore/RoomContentRegistry/RulerContent 원형),
  `assemble-room` 스킬, 기존 룸 씬들.
- 참고: `Room.exe` 서버 빌드는 이번에 CustomRoom_1로 마지막 재빌드된 상태(재생성 가능한 산출물이라 방치).
  다시 쓸 땐 원하는 씬으로 재빌드하면 됨.
