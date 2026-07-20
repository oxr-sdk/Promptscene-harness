# XRCollabDemo — Windows 데스크톱 클라이언트 빌드 & 2-클라 멀티플레이 하네스 (검증된 절차)

> 이 문서는 실제로 끝까지 돌려 검증한 절차입니다 (2026-07-14).
> `build-meta-client.md`가 **Quest APK 클라**를, `build-xumlobby-server.md`가 **서버(.exe)**를 다룬다면, 이 문서는 그 짝인
> **Windows 데스크톱 클라(.exe)** 빌드와, 그 클라로 **"2인이 서로를 본다"를 실증하는 멀티플레이 하네스**를 다룹니다.
> 대상: `c:\J_0\XRCollabDemo`, Unity **6000.3.11f1**. 검증 룸: `ComposedRoom_1`(Ruler 포함).

---

## 0. 무엇을/왜 만드나

에디터 클라 + **빌드된 데스크톱 클라** 2인을 한 머신(localhost)에서 붙여, ①서로의 아바타를 보고 ②소유권이 맞고
③한쪽이 움직이면 상대 화면에서 위치가 바뀌는 것을 **양측 시점에서** 확인한다. ParrelSync 등 패키지 추가 없이
(매니페스트 가드 대상) 2번째 클라를 **빌드된 exe**로 세운다. HANDOFF §8 프론티어 "멀티플레이 실증"이 이 절차로 닫힘.

- 산출물: `Builds/App/Client/Win-Desktop/Client.exe` (~177MB, Player 서브타깃)
- 접속 대상: 같은 머신의 Master 서버(`build-xumlobby-server.md`, cfg를 **127.0.0.1**로).

---

## 1. 사전 조건
1. `build-xumlobby-server.md` §1의 1회성 프로젝트 수정이 끝나 **컴파일 0에러**.
2. 룸(`ComposedRoom_1` 등)이 **EditorBuildSettings**에 있고, `R-RoomServer.DefaultScene`의 `_onlineScene`=룸, `_offlineScene`=`Client.unity` (C3).
3. 서버(Master + Room)가 그 룸으로 빌드돼 있음(`build-xumlobby-server.md` §2, SceneList=룸). **서버·클라는 동일 `DefaultPrefabObjects`를 씀**(C1).

---

## 2. 빌드 절차 (MCP `script-execute`, `BuildPipeline.BuildPlayer` 직접)

### 2.1 타깃 + 디파인 (1회)
```csharp
EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
// 네트워킹 스택 디파인을 영구 추가 → 에디터 클라와 빌드 클라를 같은 코드경로에, 그리고 서버 빌더의
// extraScriptingDefines가 활성 디파인의 부분집합이 되게(빌드 중 도메인 리로드 방지).
var nbt = NamedBuildTarget.Standalone;
PlayerSettings.SetScriptingDefineSymbols(nbt,
  "FISHNET;FISHNET_V4;USE_XUM_LOBBY;USE_INPUT_SYSTEM_POSE_CONTROL;USE_STICK_CONTROL_THUMBSTICKS;UNITY_MCP_READY;UNIXR_USE_FISHNET;EDGEGAP_PLUGIN_SERVERS");
AssetDatabase.SaveAssets();   // 재컴파일 대기(isCompiling==false) 후 진행
```

### 2.2 ⚠️⚠️ 서브타깃을 **Player**로 (가장 흔한 함정)
```csharp
EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;   // NOT Server!
```
서버(Master/Room)를 `XumLobbyServerBuilderWindow`로 빌드하면 **`standaloneBuildSubtarget`이 `Server`로 바뀌고 지속**된다.
그 상태로 클라를 빌드하면 **dedicated Server(헤드리스) 빌드**가 나와 **`Forcing GfxDevice: Null` → 창이 안 뜨고 렌더가 없다**
(네트워킹은 정상이라 방엔 붙지만 화면이 없음). 서버 빌드 뒤 클라 빌드 전엔 **반드시 `Player`로 복구**. (증상: 실행해도 창 없음,
`client.log`에 `Forcing GfxDevice: Null` / `Renderer: Null Device`. 정상이면 `Renderer: <GPU명>`, 프로세스에 MainWindow 존재.)

### 2.3 씬 + cfg + 빌드
```csharp
var opts = new BuildPlayerOptions {
  scenes = new[]{ "Assets/App/Scenes/Client.unity", "Assets/App/Scenes/ComposedRoom_1.unity" }, // Client=부트(0) + 룸
  locationPathName = "Builds/App/Client/Win-Desktop/Client.exe",
  target = BuildTarget.StandaloneWindows64, targetGroup = BuildTargetGroup.Standalone,
  subtarget = (int)StandaloneBuildSubtarget.Player,     // 명시
  options = BuildOptions.None };                        // extraScriptingDefines 없이(§2.1에서 박음)
var r = BuildPipeline.BuildPlayer(opts);                // 메인스레드 블로킹 → MCP "Response data is null" 정상
```
- **접속 IP 정합(localhost)**: `Client.unity`의 `ClientToMasterConnector.serverIp`=`127.0.0.1`(직렬화, 씬 저장) + `Assets/StreamingAssets/application.cfg`에 `-mstMasterIp=127.0.0.1\n-mstMasterPort=5000\n-mstStartClientConnection=True`. (에디터 클라도 이 serverIp를 씀 — 씬 저장 필수, 안 하면 도메인 리로드로 되돌아감.)
- **완료 판정**: exe mtime **아님**(신규 출력 경로면 생성되지만, 덮어쓰기 증분 빌드는 exe 스텁이 안 바뀜). **`Client_Data/globalgamemanagers`·`level0` mtime** 또는 `report.summary.result==Succeeded`로 판정. (서버 exe도 동일: `Room_Data/level0`·`application.cfg` mtime으로 판정 — `build-xumlobby-server.md`.)

**성공 판정:** `result==Succeeded`, `Client_Data/globalgamemanagers` 갱신, `client.log`에 `Renderer: <GPU>`(≠Null).

---

## 3. 자동 조인 하네스 (`Assets/PromptScene/Harness/AutoJoinClient.cs`)

빌드된 클라는 MCP `script-execute`로 몰 수 없다(그게 에디터 클라와의 차이). 그래서 **arg 게이트 하네스**를 Client.unity에 심어
2번째 클라가 스스로 조인·이동·관측하게 한다. HANDOFF §8이 예고한 "자동 조인하는 2번째 클라"의 실체이며 SYSTEMS/FEATURE 아닌 **테스트 하네스**다.

- **게이트**: `-psAutoJoin true`가 없으면 **휴면**(에디터/일반 플레이어 무영향). MST `Mst.Args.AsBool`은 **bare 플래그를 인식 못 함** → 값 `true` 필요.
- **⚠️ `DontDestroyOnLoad` 필수**: 하네스는 Client.unity에 사는데, 방 입장 시 C3 규약으로 **Client.unity가 언로드되며 하네스도 파괴**된다. `armed`일 때 `DontDestroyOnLoad(gameObject)`로 씬 전환을 살아남게 해야 방에서 관측·이동이 된다. (안 하면 조인은 되나 자기 아바타/원격을 못 봄.)
- 흐름: 마스터 접속 대기 → `SignInAsGuest` → `FindGames` → `MatchmakingBehaviour.StartMatch(games[0])` (검증된 `drive_matchmaking.cs`와 동일). 조인 후 자기 아바타(`NetworkObject.IsOwner==true`)를 찾아 이동(DummyController 비활성 후 transform 직접 세팅 — 입력 override 방지) + 매 2s 관측 로그.
- **신원 충돌 없음**: 게스트 사인인은 **고유 GUID id**를 준다(같은 머신 2게스트도 충돌 안 함). `-mstRememberUser false`로 저장된 토큰 재사용도 회피. (build-meta-client §7의 게스트 충돌은 여기선 재현 안 됨 — 각기 다른 GUID.)

실행:
```powershell
Client.exe -psAutoJoin true -mstRememberUser false -screen-width 1000 -screen-height 600 -screen-fullscreen 0 -logFile client2.log
```

---

## 4. 2-클라 실증 절차 (검증됨)

1. **서버 cfg를 localhost로**: `MasterAndSpawner/application.cfg`·`Room/application.cfg`의 `mstMasterIp`/`mstRoomIp`를 `127.0.0.1`로. (빌더는 LAN IP를 자동 감지해 박으므로 매 재빌드 후 교정.)
2. **서버 기동**: `MasterAndSpawner.exe` → (7s) `Room.exe`. `room.log`에 `Online Scene: <룸>` + `Room registered successfully ... RoomIp:127.0.0.1`.
3. **standalone 클라**: 위 실행줄로. `client2.log`에 `become a player` / `MUTUAL_VISIBLE` 관측.
4. **에디터 클라**: ⚠️ **활성 씬을 `Client.unity`로** 열고(Single) Play. 활성 씬이 룸 씬(예: ComposedRoom_1)이면 그 씬의 R-RoomServer가 **자기 룸 서버를 7777에 띄우려다 standalone Room.exe와 포트 충돌**(Bind exception)해 깨끗한 클라가 안 된다. 그다음 `SignInAsGuest`→`FindGames`→`StartMatch`(에디터는 MCP로 구동 — `drive_matchmaking.cs`).
5. **판정(§6.5 확장 — 아래)**.

---

## 5. §6.5 확장 — 멀티플레이 상호 가시 신호 (2클라)

기존 §6.5(build-working-room)의 1인 신호에 더해, **각 클라 시점**에서:

| # | 신호 | 에디터 시점(리플렉션 리드백) | exe 시점(-logFile) |
|---|---|---|---|
| ① | 자기 아바타 `IsOwner=True` | `Desktop(Clone)` OWN | `OBSERVE ... self(IsOwner=true)=1` |
| ② | 원격 아바타 Clone `IsOwner=False` 존재 | 두 번째 `Desktop(Clone)` REMOTE | `remote(IsOwner=false)=1` `MUTUAL_VISIBLE=True` |
| ③ | 원격 위치가 상대 이동에 따라 변함 | 원격 `transform.position`이 시간에 따라 변함 | 원격 ObjId의 `pos=` 가 상대 이동에 따라 변함 |

두 클라의 `ObjectId`가 교차 일치해야 한다(A의 OWN = B의 REMOTE, 반대도). `room.log`엔 두 클라 각각 `become a player`.
**실측(2026-07-14)**: 에디터=ObjId 12373, standalone=36202가 서로를 IsOwner=False로 관측, 위치 오실레이션/이동 양방향 반영.
실증 기록(에디터 시점, 두 아바타 한 화면): [screenshots/m0-two-clients-mutual-visibility.png](screenshots/m0-two-clients-mutual-visibility.png).

---

## 6. Ruler 결과값 공유(M1) — 공유 스폰 검증 신호

`ComposedRoom_1`의 Ruler는 측정을 **네트워크 스폰**한다(아래 §7). 검증:
- 한 클라가 측정 생성 → 다른 클라가 **RulerMeasurementView**(선+거리라벨)를 **IsOwner=False**로 관측(끝점까지 전파 → 선 렌더). **양방향**.
- 한 클라가 `ClearAll` → 모든 측정 디스폰이 **양측 전파**(교차 소유 despawn: `RequireOwnership=false`).
- 하네스 로그의 `MEASUREMENTS=N` 카운트로 exe측 판정. **실측(2026-07-15)**: 에디터 생성→standalone 1→2, standalone 자가생성→에디터 관측, 에디터 ClearAll→양측 2→0. 실증 기록(공유 룰러 선+거리 라벨): [screenshots/m1-shared-ruler-measurement.png](screenshots/m1-shared-ruler-measurement.png).

---

## 7. 관련 코드 (런타임 — XRCollabDemo, 이 레포 밖)

- `Assets/PromptScene/Core/RoomCore.cs` — `INetSpawn`을 **FishNet 백엔드 `FishNetSpawn`**으로: `Spawn`→`XumNetwork.Instantiate(nob,p,r,ownerConn)`(클라는 ServerRpc 왕복·null 반환), `Despawn`→서버면 `ServerManager.Despawn`·클라면 `INetDespawnRequest.RequestServerDespawn()`(XumNet엔 Despawn 심볼 없음). 오프라인이면 로컬 Instantiate 폴백. 메커니즘 승격(계약 §4.5)이라 SYSTEMS 해동 아님.
- `Assets/PromptScene/Core/Contracts.cs` — `INetDespawnRequest`(비서버 클라가 서버 디스폰 요청 — INetSpawn을 제네릭하게 유지).
- `Assets/PromptScene/Content/Ruler/RulerMeasurementView.cs` — 프리팹의 FEATURE-내부 NetworkBehaviour: `[ServerRpc] SubmitEndpoints`→`[ObserversRpc(BufferLast=true)] BroadcastEndpoints`로 두 끝점 전파(late-join 대응) + 선/라벨 구성 + despawn RPC. `INetSpawn.Spawn(prefab,pos,rot)`이 못 나르는 **per-object 데이터**를 여기서 처리.
- `Assets/PromptScene/Content/Ruler/RulerContent.cs` — `IsNetworked`면 `core.Net.Spawn(measurementPrefab)` + 끝점 stash, 아니면 로컬 폴백. `ClearAll`은 씬의 `RulerMeasurementView`를 열거해 despawn(클라 스폰은 null 반환이라 핸들이 없음). **여전히 PromptScene.Core만 참조**.
- `RulerMeasurement.prefab` — 루트 NetworkObject + RulerMeasurementView. **DefaultPrefabObjects에 등록(C1)** — FishNet auto-populate로 자동 추가됨(에셋 PrefabId 65535는 정상, 런타임에 컬렉션 인덱스로 할당). ComposedRoom_1의 RulerContent에 할당.

---

## 8. 함정 요약 (하네스가 알아야 함)

| # | 함정 | 대응 |
|---|---|---|
| A | **서버 빌드가 subtarget을 Server로 남김** → 이어진 클라가 헤드리스(창 없음) | 클라 빌드 전 `standaloneBuildSubtarget=Player` 복구 + `opts.subtarget=Player` 명시 |
| B | **하네스가 방 입장 시 파괴됨**(Client.unity 언로드, C3) | `armed`일 때 `DontDestroyOnLoad` |
| C | **에디터 클라의 활성 씬이 룸 씬이면 포트 7777 충돌** | 에디터 Play 전 활성 씬을 `Client.unity`로 |
| D | **긴 블로킹 빌드/타깃 전환이 MCP 브리지를 끊음**(`Response data is null` 지속) | 에디터 재시작(unity-mcp-server는 유지)해 재연결. 타깃 전환 후 첫 빌드가 특히 위험 |
| E | **서버 실행 중엔 그 서버 exe 재빌드 불가**(`Assembly-CSharp.pdb ... user-mapped section open`) | 서버 재빌드 전 `MasterAndSpawner`/`Room` 프로세스 **종료** |
| F | **완료 판정에 exe mtime 쓰면 오판**(증분 빌드는 exe 스텁 불변) | `_Data/level0`·`globalgamemanagers`·`application.cfg` mtime 또는 `BuildReport.result` |
| G | **`Mst.Args.AsBool`은 bare 플래그 미인식** | `-psAutoJoin true`처럼 값 전달 |
| H | 빌더가 cfg에 **LAN IP 자동 기입** | localhost 테스트면 매 재빌드 후 cfg의 IP를 `127.0.0.1`로 교정 |
| I | **클라 런타임 네트워크 스폰이 `IsClientStarted` 전에 드롭됨**(M2) | 스폰(`core.Net.Spawn`)은 `InstanceFinder.IsClientStarted` 게이트 뒤 + 프롭 등장까지 재시도. `ClientId` 유효만으론 부족 |
| J | **2 데스크톱 게스트 동시 조인 flakiness**(M2: 2번째 sign-in/validation 미완 + 서버 2nd-connect NRE) | 조인 직렬화(A player 확정 후 B 기동)+재시도. 신뢰 토폴로지는 에디터+1데스크톱. 그랩 결함 아님 |
| K | **콜드 스타트 클라의 방 조인이 액세스 토큰 만료로 실패**(M3): 방 액세스 토큰의 `AccessTimeoutPeriod`가 **10초**인데, exe **첫 실행**은 StartMatch→룸 씬 로드(셰이더 컴파일 등)가 10초를 넘겨 room.log `Failed to confirm the access` / 클라 `Room could not validate you`로 거부→Client.unity 재로드(하네스 2중 인스턴스, 로그 2줄씩) | **재시도가 정답**: 프로세스 재기동(웜 스타트는 씬 로드가 빨라 통과). 직렬화 조인(트랩 J)과 별개로, 같은 exe의 **워밍업 1회** 후 본 판정을 돌리는 게 안전. 하네스의 조인 실패 후 자동 재시도는 미구현(현재는 재기동) |

> **정직 계약:** 이 절차의 증명 범위는 **2클라 상호 가시 + 측정 결과값(생성/제거) 전파 + 잡기 소유권 핸드오버**(D4-1, §10) + **텍스트 채팅 양방향**(M3, §11)까지. 3인+, 실기기(Quest) 2클라, 예측(D4-2), 채팅 이력 백필은 밖.

---

## 9. 수동 플레이 (인게임 HUD)

사람이 직접 몰아보려면 룰러를 켜고/지우는 UI가 필요하다(자동조인 하네스는 아바타를 하이재킹하므로 수동 플레이엔 부적합 — 수동은 auto-join 없이 로비에서 직접 입장).

- **`Assets/PromptScene/Content/Ruler/RulerHudUI.cs`** — 룸 씬 `===== UI =====`의 얇은 IMGUI 패널(런치패드 아님). 레지스트리의 `Toggleable`을 순회해 콘텐츠마다 ON/OFF 버튼 + "측정 지우기(Clear)" + 공유 측정 카운트 + 조작 안내. 헤드리스 서버는 OnGUI 미호출이라 클라에서만 뜬다.
- **`Core/SimpleClickProvider.cs`** — `public static bool SuppressWorldClick` 추가(제네릭 메커니즘). HUD가 커서를 자기 패널 위에 두는 동안 이 플래그를 켜서 **버튼 클릭이 바닥 레이캐스트로 새지 않게** 한다(uGUI `IsPointerOverGameObject`는 IMGUI를 못 막으므로). `activeInputHandler=2(Both)`라 레거시 `Input.GetMouseButtonDown`이 동작(HUD/클릭 검증됨 2026-07-15).
- **런처**: `Builds/App/play-2clients.ps1` — 서버 cfg를 localhost로 교정 + Master/Room 기동 + 클라 2개 실행(auto-join 없이). 각 창에서 Guest 로그인→방 입장→HUD로 룰러 ON→바닥 두 지점 클릭=측정(양쪽 공유), 이동 WASD.
- **검증(2026-07-15)**: HUD 렌더 + 룰러 토글 ON + **클릭 두 지점→네트워크 측정 스폰**(SimpleClickProvider→RulerContent.OnClick→core.Net.Spawn) + Clear→측정 0. 스크린샷: [screenshots/hud-play.png](screenshots/hud-play.png).

---

## 10. 잡기 소유권 핸드오버 (D4-1, M2 2026-07-16)

멀티플레이 하네스를 **소유권 이전**까지 확장. `AutoJoinClient`에 그랩 안무를 추가해(arg 게이트) 2클라가 공유 에폭 타임라인 위에서 잡기 소품 하나를 A→B→A로 넘겨받고, 각자 `-logFile`에 프롭 `ownerId`/`isMine`/`pos`를 기록한다.

- **런처**: `Builds/App/play-grabtest.ps1` — 서버 cfg localhost 교정 + Master/Room 기동 + **직렬화 조인**(A가 player 된 뒤 B 기동, 트랩 J 완화) + 공유 에폭(`-psGrabEpoch`) 주입.
- **하네스 args**: `-psAutoJoin true -psGrabTest true -psGrabRole A|B -psGrabEpoch <unixMs>`. 역할 A=스폰+첫 잡기+재탈취+정리, B=탈취.
- **판정 신호(5)**: ①A잡기 Owner=A 양측 ②A이동·놓기 위치 전파+Owner 유지(비반납) ③B탈취 Owner=B(A도 확인) ④B이동·놓기 위치 전파 ⑤A재탈취 Owner=A. 공유 `objId` 양측 교차 일치. **검증됨(2026-07-16, 5신호 PASS).**
- **구현·함정 SSOT**: [grab-ownership-survey.md](grab-ownership-survey.md) §실증 (FEATURE `GrabbableProps`, 프리팹 `GrabbableProp`, `GrabbableView`가 `XumView.RequestOwnership` 직접 사용 — SYSTEMS/계약 무수정).
- ⚠️ 2 데스크톱 게스트 동시 조인은 MST flakiness(트랩 J)로 재현이 불안정 — 그랩 로직 결함 아님. 1클라 스모크(스폰→잡기→이동→놓기→정리)는 안정 재현.

---

## 11. 텍스트 채팅 양방향 (M3, 2026-07-20)

`ChatContent` FEATURE(2클라 채팅)의 라이브 검증. 검증된 두 기계의 조합만 사용 — `[ServerRpc(RequireOwnership=false)]` 상행(M2의 `CmdTakeOwnership`과 동형) + `[ObserversRpc]` 하행(M1의 `BroadcastEndpoints`와 동형). 신규 플랫폼 API 없음.

- **구현(런타임 `Assets/PromptScene/Content/Chat/`)**: `ChatContent.cs`(IToggleableContent, 기능 지역 IMGUI 패널 — 우상단, 메시지 목록+입력줄+전송+Enter, RulerHudUI 무확장) + `ChatChannel.prefab`(루트 NetworkObject + `ChatChannelView`, DefaultPrefabObjects 자동 등록 C1) + `ChatChannelView.cs`(`CmdSend(text, NetworkConnection sender = null)` — **발신자는 서버가 주입한 ClientId**(XumView.SpecificActorServerRPC와 동일 패턴, 위조 불가) → `RpcReceive(senderId, text)` 전원 방송, 수신은 static Log로 **전 채널 집계**).
- **채널 스폰-또는-재사용**(이번 유일한 신규 설계 지점): `SetEnabled(true)` 시 씬에 `ChatChannelView`가 없으면 `core.Net.Spawn`으로 1개 스폰, 있으면 재사용. 재시도 루프(3s)가 트랩 I(active 전 스폰 드롭)를 흡수. 동시-인에이블 레이스로 2채널이 나도 무해(수신 집계+발신 단일 채널). **실측**: B의 ENABLE 시점(막 active, 채널 미복제)에 스폰 시도 1건이 나갔으나 서버 미도달(트랩 I 경로로 드롭)—채널은 양측 내내 1개(objId 38402 교차 일치).
- **SetEnabled(false) = 패널 숨김만**: 채널은 다른 클라를 위해 존치(개인 나가기 ≠ 룸 철거). 재켜기 시 기존 채널+로그 재사용(H2에서 재토글 후 channels=1 확인).
- **Core 메커니즘 일반화 1건(정직 기록)**: `SimpleClickProvider.SuppressWorldClick`을 단일 writable bool → **클레임 기반**(`SetWorldClickSuppressed(claimant,on)`, 클레임 1개라도 있으면 억제)으로 교체. 패널이 2개(RulerHUD+채팅)가 되는 순간 마지막-쓰기-승리로 한쪽 억제가 스크립트 실행 순서에 따라 조용히 깨지는 잠재 버그의 수정. 계약(Contracts.cs) 무수정.
- **판정(4신호, 에폭 동기 로그 — M2 방식)**: 에디터 A(myId=0, MCP 구동) + 데스크톱 exe B(myId=2, `-psChatTest true -psChatRole B -psChatEpoch <unixMs>`, 반응형 안무). **전부 PASS(2026-07-20)**:
  ① A 발신 5건 → B 수신(`RECV from=0`, 내용 일치) ② B 회신 2건 → A 수신(P2) ③ B TRANSCRIPT [0..4]=A-msg-1..5 **순서 보존** ④ 발신자 교차 일치(양측 모두 A건=P0, B건=P2). 스크린샷(양쪽 패널 같은 대화): [screenshots/m3-chat-two-clients.png](screenshots/m3-chat-two-clients.png).
- **1인 스모크(H2, 반복 재현 가능)**: 스폰(1채널/live/owner=본인)→off·on 재토글 후에도 1채널→루프백(자기 발신이 ObserversRpc로 돌아와 P0 표기)→SetEnabled(false) 패널 숨김+억제 해제.
- **하네스 args**: `-psAutoJoin true -psChatTest true -psChatRole A|B -psChatEpoch <unixMs>`. A역=T≥12부터 5건 순차 발신, B역=A 5건 수신 후 2건 회신(반응형 — 느린 수동 구동에도 강건), 양측 TRANSCRIPT 덤프.
- **정직 범위**: 텍스트 채팅 양방향, 2인, 데스크톱, **백필 없음**(늦게 조인한 클라는 과거 메시지 못 봄 — `ObserversRpc BufferLast`는 마지막 1건만 버퍼라 이력에 부적합; 이력 동기화는 수요 생기면). 3인+, VR 입력(가상 키보드), 귓속말/채널 분리, 입력 중 WASD 이동 억제는 밖.
- **승격 검토**: FEATURE 내부 네트워크 사용이 3건이 된 시점의 `INetMessaging` 계약 승격 판정(보류 권고)은 [net-messaging-promotion-review.md](net-messaging-promotion-review.md).
