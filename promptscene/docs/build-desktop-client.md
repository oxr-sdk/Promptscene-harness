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

---

## 12. D2 COMPOSITIONS 게임 루프 — 과녁 점수전 (2026-07-21)

design-directions **D2**의 첫 실증. FEATURE 간 직접 참조 없이(=서로 모름) **COMPOSITION 층**이 두 직교 FEATURE를 하나의 서버권위 게임 루프로 조율함을 라이브 판정. 계약 추가는 **딱 하나**(인프로세스 `IEventBus`, `IRoomCore` 무변경 — contract §2). 네트워크 권위 프리팹 `MatchView`는 M3 `ChatChannelView` 동형(`ServerRpc(RequireOwnership=false)` 상행=서버 주입 ClientId 집계, `ObserversRpc` 하행=스코어보드 방송) — 신규 플랫폼 API 0.

- **런타임 코드(XRCollabDemo, 이 레포 밖):**
  - `Assets/PromptScene/Core/Contracts.cs` — `IEventBus`(Publish/Subscribe/Unsubscribe). `Core/RoomCore.cs` — `Awake`에서 `RegisterService<IEventBus>(new EventBus())`(내장 서비스 4번째, IRoomCore 무변경). 구현은 예외 격리(GetInvocationList+try/catch) + (T,handler) 멱등.
  - `Content/TargetProps/`(`TargetProps.cs` = `TargetHitEvent` 발행 FEATURE, `TargetMarker.cs`, `Target.prefab`) / `Content/ScoreHud/ScoreHud.cs`(= `ScoreChangedEvent` 구독·IMGUI 표시 FEATURE). **두 소스 상호 타입 참조 0**(grep). `Compositions/TargetShootoutMatch/`(`TargetShootoutMatch.cs` = 조율 MonoBehaviour, `MatchView.cs`+`MatchView.prefab` = 서버권위 딕셔너리+RPC).
- **프리팹·씬 조립(스크립트):** ① `Target`(Sphere: MeshFilter/Renderer/SphereCollider + NetworkObject + TargetMarker), `MatchView`(NetworkObject + MatchView)를 `PrefabUtility.SaveAsPrefabAsset` — 저장 시 FishNet `PrefabCollectionGenerator`가 **DefaultPrefabObjects에 auto-populate**(C1, 16→18). ② `ShootoutRoom_1`: `build_composed_room.cs` 골격 + `===== COMPOSITIONS =====` 헤더 추가, TargetProps/ScoreHud를 FEATURES·TargetShootoutMatch를 COMPOSITIONS 아래 배치, `SerializedObject`로 `TargetProps.targetPrefab`←Target·`TargetShootoutMatch.matchPrefab`←MatchView 배선, `AssignFishNetSceneIds`(spawner SceneId — §1 함정). ③ Room.exe 재빌드(`XumLobbyServerBuilderWindow` SceneList=[ShootoutRoom_1]) → room.log `Online Scene: ShootoutRoom_1` 확인.
- **판정(라이브):**
  - **§6.5 (에디터 클라):** Client 언로드·ShootoutRoom_1 로드·Desktop(Clone) IsOwner=True·RoomCore 4서비스·TargetProps/ScoreHud 자기등록+SetEnabled 무예외·MatchView 스폰 1개·Target 4개 스폰.
  - **단일 클라 서버권위 루프:** 명중 주입(`bus.Publish(TargetHitEvent)` = TargetProps.OnClick의 명중 후 동작)→COMPOSITION→`MatchView.ReportHit`(ServerRpc)→**서버 집계 1→2→3**→ObserversRpc→ScoreChanged→ScoreHud. 방송 레코더(정적 `MatchView.OnBroadcast` 구독→파일)로 전이 캡처: `scores=[1]→[2]→[3] over=True winner=P0 → [] (리셋)`. ⚠️ resetDelay(기본 4s)가 짧아 MCP 왕복 지연으로 승자 스냅샷을 놓치기 쉬움 → **방송 레코더**로 잡을 것(폴링 리드는 리셋 후를 잡음).
  - **2클라 점수 동기 파리티(신뢰 토폴로지=에디터+데스크톱 1):** 데스크톱 B(`-psAutoJoin true`, Player 서브타깃=Renderer≠Null)가 먼저 조인(MatchView 스폰), 에디터 A 조인(MatchView **재사용**, avatars=2/own1/remote1). A(clientId 1) 명중 → **B의 `clientB.log`**에 `[MatchView] scoreboard players=1 leader=P1 … over=True winner=P1` + 리셋이 그대로 찍힘(B는 한 발도 안 쐈는데 P1 점수 상승 관측 = 서버권위·동기 증명). B의 COMPOSITION은 Chat 부재를 `Contents.GetById("chat")` 런타임 조회로 감지→자체표시 폴백(무해).
  - **버스 런타임 스모크(오프라인):** RoomCore 구성 후 `TryGet<IEventBus>` → 전달/멱등(재구독 중복호출 0)/예외격리(throw 핸들러가 타 핸들러·발행 중단 안 함)/해지/빈발행 안전.
- **증거:** [screenshots/d2-shootout-scoreboard.png](screenshots/d2-shootout-scoreboard.png)(단일 P0 2/3+과녁4), [d2-shootout-2client.png](screenshots/d2-shootout-2client.png)(2클라 A뷰 P1 2/3), [d2-shootout-clientB-parity.txt](screenshots/d2-shootout-clientB-parity.txt), [d2-shootout-broadcasts.txt](screenshots/d2-shootout-broadcasts.txt).
- **함정(신규/재확인):** ① 서버 빌드 후 클라 빌드 전 `standaloneBuildSubtarget=Player` 복구(트랩 A) — 안 하면 B가 헤드리스라 화면 없음. ② `EditorApplication.ExecuteMenuItem("Fish-Networking/Refresh Default Prefabs")`는 **메뉴명 부재로 실패** — 그러나 `SaveAsPrefabAsset`의 postprocessor가 이미 auto-populate하므로 무해(등록은 됨). ③ MatchView reset delay가 짧아 승자 스냅샷 폴링 리드로는 놓침 → 방송 레코더 필수. ④ 데스크톱에서 룸 Main Camera + 아바타 카메라 = "2 audio listeners" 경고가 콘솔을 도배 → 판정은 **파일 출력**(MatchView.Latest/레코더)으로, 콘솔 grep 의존 금지(VR은 §2.4-D처럼 Main Camera 비활성).
- **정직 범위:** 구조 불변식(참조 0) + 버스 런타임 + §6.5 + **서버권위 집계·2클라 점수 동기·승자·리셋**까지. **밖:** 실제 마우스클릭 레이캐스트→명중(주입은 버스 경계), 게임 "재미"/밸런스, 3인+·B 대칭 득점, VR 입력, Target 머티리얼(URP 셰이더 미해결 마젠타 — 콜라이더/NetworkObject 정상), compose-room의 COMPOSITION 편입(후속).

---

## 13. 던지기 — 다트 (V1, 2026-07-22)

D2 점수전 위에 **던지기(비경합 투사체)**를 얹어 라이브 판정. **신규 플랫폼 API 0** — [capability-map](capability-map.md)의 "던지기=재조합" 첫 실증. 던진 사람이 비행 내내 오너(Takeover, 비반납), client-auth `NetworkTransform`이 궤적 전파, 명중→**자체 이벤트**→COMPOSITION→서버권위 점수. **뺏기 경합 전까지 D4-2(예측) 불요.**

- **런타임 코드(XRCollabDemo, 이 레포 밖) — `Assets/PromptScene/`:**
  - `Content/DartProps/DartProps.cs` — `IToggleableContent`(Core-only). `SetEnabled(true)`→`INetSpawn`으로 이 클라의 다트 N개 스폰(오너=스폰 클라, 각자 자기 탄약). M2 `GrabbableProps`와 동형.
  - `Content/DartProps/DartView.cs` — 프리팹 내부 FishNet `NetworkBehaviour`. **오너만 물리 시뮬**(`isKinematic = !(IsOwner && _inFlight)` — 정지·비오너·명중후엔 kinematic, 비행 중에만 dynamic → 스폰 다트가 안 떨어지고 대기, 명중 시 꽂혀 정지), 입력경로=`XRGrabInteractable` 단일(그랩→`XumView.RequestOwnership`, 릴리즈→`throwOnDetach`). **첫 비행 충돌 1회** 오너가 `DartHitEvent{Struck,Position}` 발행 후 정지. 데스크톱 검증용 주입점 `ThrowLocal(velocity)`(입력경로 아님 — 릴리즈 직후 velocity 주입만). **다트끼리 충돌 무시**(`Physics.IgnoreCollision`)로 자기 탄약더미에 막혀 멈추는 것 방지.
  - `Content/DartProps/DartHitEvent.cs` — DartProps 소유 이벤트. **TargetProps 무참조**(FEATURE↔FEATURE 0 유지) — 다트는 "무엇을 쳤나"만 싣고, "과녁인가?"는 COMPOSITION이 판정(2026-07 결정: 공유 이벤트로 승격 대신 자체 이벤트).
  - `Compositions/TargetShootoutMatch/TargetShootoutMatch.cs` — `Subscribe<DartHitEvent>` 1줄 추가. `OnDartHit`이 `Struck`에 `TargetMarker` 있으면(=COMPOSITION만 아는 정책, COMPOSITION→FEATURE 허용) **클릭과 동일한 `MatchView.ReportHit`** 경로로 라우팅 → **버스 배당금: 한 점수 루프, 두 소스(클릭/다트)**.
  - 프리팹 `Content/DartProps/Dart.prefab` — `GrabbableProp` 골격 클론(NetworkObject + XumView(Takeover) + client-auth NT) + Rigidbody(mass 0.1, gravity, **ContinuousDynamic**=고속 관통 방지) + BoxCollider + XRGrabInteractable(throwOnDetach) + DartView. **C1**: DefaultPrefabObjects auto-populate(index 17, 총 18). URP/Lit 머티리얼(V1c).
- **씬·빌드:** `ShootoutRoom_1`의 `===== FEATURES =====`에 `DartProps` 편입(dartPrefab 배선) → Room.exe 재빌드(SceneList=[ShootoutRoom_1], room.log `Online Scene: ShootoutRoom_1`) + Client.exe 재빌드(DPO+씬 동기).
- **하네스 args**: `-psAutoJoin true -psDartTest true -psDartRole A|B -psDartEpoch <unixMs>`. A역=TargetProps+DartProps 인에이블→T≥8/13/18에 각 과녁으로 **탄도(ballistic) 조준** 다트 발사(`ThrowLocal`, 비행시간 0.5s로 velocity 역산). B역=관측만. 런처 `Builds/App/play-darttest.ps1`(서버 cfg localhost + 직렬화 조인 + 공유 에폭).
- **판정(V1a, 2클라 데스크톱, PASS 2026-07-22):**
  - **A(thrower, myId=0):** 다트 3개 스폰(대기 중 안 떨어짐) → 3발 발사 → **3/3 명중** → 서버권위 P0:1→2→3 → **over=True winner=P0**(선취 3) → resetDelay 후 리셋. dart-view 로그: `first flight collision with 'Target(Clone)' ... published DartHitEvent (owner)`.
  - **B(observer, myId=1):** ①**비행 궤적 관측** — 에폭 위치로 다트를 공중에서 포착(T=9 z=1.38, T=14 z=1.85 등 스폰 z=0.5→과녁 z=3.5 사이) ②**Owner=A 유지** — 매 관측 `owner=0 mine=False`(비행 중·명중 후 내내) ③**점수 양측 동기** — B가 `[MatchView] scoreboard` 방송 수신, P0:1→2→3→over=True winner=P0→리셋(B는 한 발도 안 쐈음) ④위치 교차 일치(B (-0.85,1.37,3.11) ≈ A (-0.86,1.37,3.12)). avatars own1/remote1 상호가시.
- **V1c 마젠타 교정:** Target/Dart에 URP/Lit 머티리얼(빨강/노랑) 배선 + **"에러 셰이더 0" 검사**(`Assets/PromptScene/Harness/Editor/ShaderSanityCheck.cs`, 메뉴 `PromptScene/Check Error Shaders`) — 7 슬롯 스캔, `Hidden/InternalErrorShader`/null 0건 PASS. 실기기 가기 전 필수 게이트.
- **V1b XR 입력경로(소스 검증):** `throwOnDetach` 릴리즈 velocity는 `XRGrabInteractable`이 **attach/targetPose 프레임 델타**를 평활화해 산출([XRGrabInteractable.cs] `m_ThrowSmoothingLinearVelocityFrames[f] = (targetPose - lastPose)/dt`, `Detach()`에서 적용) — **실기기/시뮬 동일 코드경로**(인터랙터 포즈만 다름, XRGI는 입력원을 모름). **발견·수정:** `Detach()`는 **kinematic Rigidbody를 던지지 않음**("Cannot throw a kinematic Rigidbody") — 다트는 정지 시 kinematic이라 XR 그랩→릴리즈에서 발사 실패 가능 → `DartView.OnGrabbed/OnReleased`에서 non-kinematic 강제(소스 근거 수정, 데스크톱 `ThrowLocal` 경로는 무관). **⚠️ 라이브 XR 시뮬 스윙은 미실행 — V2 재검증 항목**(아래).

### V2 준비물 (다음: 실기기 던지기)
- **Quest 3 충전 + adb 페어링** 선행. 클라 APK 빌드·배포는 [build-meta-client.md](build-meta-client.md)(검증됨) + `/deploy-client Meta` 스킬. 화면 깜빡임/시점고정은 build-meta-client §2.4-D.
- **V1b에서 넘어온 재검증 항목:** ① XR 시뮬레이터/실기기로 **그랩→스윙→릴리즈→throwOnDetach velocity 계승**을 실제로 관측(코드경로는 동일 검증됨, 스윙 magnitude·손맛은 비검증). 시뮬 재현법: 룸 씬에 `XRInteractionSimulator`(XRI 패키지 Runtime, 샘플/매니페스트 불요) + 인터랙터 배치 → 시뮬 컨트롤러로 다트 그랩 후 이동(스윙)→릴리즈. 릴리즈 velocity가 0/미미하면 스윙 프레임수(`throwSmoothingDuration`)·이동 속도 조정. ② non-kinematic 강제 수정이 `Detach()` 순서상 실제로 발사를 성립시키는지 실기기 확인. ③ 다트끼리 IgnoreCollision이 VR 다중 그랩에서도 유지되는지.
- **정직 계약(V1의 밖 = V2의 몫):** 손맛·실기기 함정(build-meta-client §2.4-D 등)·크로스플랫폼(HMD+데스크톱 동시)·경합 뺏기(D4-2)는 **V2 몫이며 V2의 판정자는 사용자.** V1은 데스크톱 2클라에서 **물리·비행 전파·소유권·명중→점수 동기**(구조/동작)까지만 증명.
