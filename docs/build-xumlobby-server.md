# XRCollabDemo — XumLobby Server 빌드 & 런타임 검증 (검증된 절차)

> 이 문서는 실제로 끝까지 돌려서 검증한 절차입니다. PromptScene 하네스가 빌드/런타임을 자동 검증할 때 이 신호들을 그대로 사용합니다.
> 대상: `c:\J_0\XRCollabDemo` (oxr-sdk/XRCollabDemo), Unity **6000.3.11f1**.

---

## 0. 무엇을 빌드하나
README §6 **Server** 경로 = 두 개의 헤드리스 서버 실행파일:
- **MasterAndSpawner.exe** ← `Master.unity` (마스터 서버 + 스포너)
- **Room.exe** ← `Room.unity` (룸/세션 서버)

스택: MasterServerToolkit(MST) + FishNet + XumNet. 빌드 도구: **Xum Build Kit**의 `XumLobbyServerBuilderWindow`.

---

## 1. 사전 조건 — 프로젝트 1회성 수정 (이거 안 하면 컴파일/빌드 자체가 안 됨)

XRCollabDemo는 클론 그대로는 컴파일이 안 됩니다. 다음을 적용해야 컴파일 0에러가 됩니다:

| # | 문제 | 수정 |
|---|---|---|
| 1 | `com.oxr.sdk`(Backend-Frontend)가 `.asmdef.meta`/대부분 `.cs.meta` 없이 커밋됨 → git(immutable) 패키지라 Unity가 meta 자동생성 못 함 → `com.oxr.sdk.Runtime.dll` 미생성 → XumLobby `using OxrSdk` 실패 | PackageCache의 `com.oxr.sdk@*`를 `Packages/com.oxr.sdk/`로 **embed(복사)** → 가변 패키지가 되어 meta 자동생성. manifest의 `com.oxr.sdk` git 라인 제거 |
| 2 | XumLobby 0.4.4가 구 OxrSdk API 호출. `OXRReservationModule.cs:89` `GetAllMeetingsAsync(meetingApiBaseUrl, companyCode)` (string,string) ↔ 현재 API `GetAllMeetingsAsync(int page, int size)` | XumLobby도 `Packages/`로 embed 후 line 89를 `GetAllMeetingsAsync()`로 패치 |
| 3 | 임포트된 **XumLobby Sample이 불완전** — `Editor/UI/Builder/*`(창)만 있고 `Editor/Build/*BuildOptions.cs`(타입 정의) + asmdef 누락 → `XumLobbyServerBuildOptions` 못 찾음 | Package Manager Sample API로 **정식 재임포트**: `Sample.FindByPackage("com.kisti.xumbuildkit","")` 에서 "XumLobby Sample" → `.Import(Sample.ImportOptions.OverridePreviousImports)` |

추가 환경:
- 빌드 타깃: **StandaloneWindows64**, 스크립팅 백엔드 **Mono2x**.
- **Windows Dedicated Server 모듈 필수** (Server subtarget용). 확인: `<Unity>/Editor/Data/PlaybackEngines/WindowsStandaloneSupport/Variations/win64_server_nondevelopment_mono` 존재.
- 활성 스크립팅 디파인에 `FISHNET;FISHNET_V4;USE_XUM_LOBBY` 포함(빌더가 `extraScriptingDefines`로 `FISHNET;FISHNET_V4;EDGEGAP_PLUGIN_SERVERS;UNIXR_USE_FISHNET` 추가).

---

## 2. 빌드 절차

### 2-A. 수동 (README §6, GUI)
`Xum Build Kit → Build → XumLobby → Server 탭`:
- Target Platform: Windows
- Master Scene: `Assets/App/Scenes/Master.unity`
- Room Scene: `Assets/App/Scenes/Room.unity`
- IP/Port 설정 → **Master + Room 둘 다 Build**

### 2-B. 자동 (하네스 — MCP `script-execute`로 헤드리스 구동)
`XumLobbyServerBuilderWindow`의 빌드 메서드를 reflection으로 직접 호출:

```csharp
// 어셈블리명 = "XumLobby.Editor" (asmdef "name" 필드)
var winType = System.Type.GetType(
  "XumBuildKit.Samples.Editor.App.XumLobbyServerBuilderWindow, XumLobby.Editor");
var win = ScriptableObject.CreateInstance(winType);              // OnEnable이 _settings 로드
var settings = winType.GetField("_settings", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(win);
var st = settings.GetType(); // XumLobbyServerBuildOptions : NetworkBuildOptions : BaseBuildOptions
st.GetField("MasterScene").SetValue(settings, new List<EditorBuildSettingsScene>{
    new EditorBuildSettingsScene("Assets/App/Scenes/Master.unity", true) });
st.GetField("SceneList").SetValue(settings, new List<EditorBuildSettingsScene>{
    new EditorBuildSettingsScene("Assets/App/Scenes/Room.unity", true) });   // BaseBuildOptions.SceneList
st.GetField("SelectedPlatform").SetValue(settings, 0); // ServerOS.Win
st.GetField("IsHeadless").SetValue(settings, true);
// (MasterIp/MasterPort 기본값 127.0.0.1/5000 — 빌더가 로컬 LAN IP 자동감지해 cfg에 기록)
winType.GetMethod("BuildMasterForWindows", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win, null);
winType.GetMethod("BuildRoom",             BindingFlags.NonPublic|BindingFlags.Instance).Invoke(win, null);
```

> `BuildPipeline.BuildPlayer`가 메인 스레드를 블로킹하므로 MCP 호출은 `Response data is null`로 타임아웃될 수 있음 — **정상**. 결과는 산출물(아래)로 확인.

---

## 3. 빌드 산출물 = 성공 판정 (하네스 체크)

```
Builds/App/Server/StandaloneWindows64/
  MasterAndSpawner/  MasterAndSpawner.exe  MasterAndSpawner_Data/  application.cfg
  Room/              Room.exe              Room_Data/              application.cfg
```

- **`application.cfg` 존재 ⇔ `BuildResult.Succeeded`** (빌더가 성공 분기에서만 cfg를 씀 → 가장 강한 머신 체크).
- `MasterAndSpawner/application.cfg` 내용 예:
  ```
  -mstStartMaster=True
  -mstStartSpawner=True
  -mstStartClientConnection=True
  -mstRoomExe=...\Room\Room.exe
  -mstMasterIp=<LAN IP>   -mstMasterPort=5000
  -mstRoomIp=<LAN IP>
  ```
- `Room/application.cfg`: `-mstStartClientConnection=True`, `-mstMasterIp=<LAN IP>`, `-mstMasterPort=5000`.

---

## 4. 서버 실행 & 검증

```powershell
# 각 exe를 자기 폴더 cwd로 실행(폴더의 application.cfg를 읽음). -logFile로 로그 캡처.
Start-Process MasterAndSpawner.exe -WorkingDirectory <ms폴더> -ArgumentList "-logFile","<ms>\master.log"
Start-Process Room.exe             -WorkingDirectory <room폴더> -ArgumentList "-logFile","<room>\room.log"
```

검증 신호 (로그 정규식):
- master.log: `Master Server Behaviour started and listening to: <IP>:5000` + `Successfully initialized modules` + `Spawner successfully created`
- room.log: `Room Server started and listening to: :7777` + `Room registered successfully. Room ID:`
- netstat: `<IP>:5000` LISTENING(master), `UDP 0.0.0.0:7777`(room)

> 정상적으로는 **스포너가 룸 생성 시 Room.exe를 자동 실행**함. Room.exe 수동 실행 시 R-RoomServer가 스스로 마스터에 방을 등록함.

---

## 5. 에디터 클라이언트 검증 (클라 빌드 없이)

1. `Client.unity` 열기 (필요시 `Room.unity` Additive).
2. **⚠️ IP 일치 필수**: 마스터는 cfg의 `mstMasterIp`(예: 192.168.50.49)에 **그 IP로만 바인딩**됨 (127.0.0.1 아님). `C-ClientMasterConnector`의 `serverIp`(= `ConnectionHelper.serverIp`, 기본 127.0.0.1)를 그 IP로 바꾸고 **씬을 저장**할 것.
   - 저장 안 하면 Play 진입 시 **도메인 리로드로 127.0.0.1로 되돌아가 "연결 서버 로스트"** 발생(실제로 겪음).
3. Play.
4. 검증: master.log의 `opened connection` 카운트 증가(에디터 클라 접속). 콘솔에 연결 에러 없음.

> 로컬 단일머신 테스트라면 대안: 두 cfg의 `mstMasterIp`/`mstRoomIp`를 `127.0.0.1`로 바꾸고 서버 재시작 → 클라 기본값(127.0.0.1)과 일치.

---

## 6. 알려진 함정 (하네스가 가드해야 함)

- **IP 바인딩**: 빌더가 로컬 LAN IP를 자동감지해 cfg에 박음 → 클라 `serverIp`가 반드시 일치해야 함. 불일치 = "연결 로스트".
- **serverIp 미저장**: 인메모리 변경은 Play 도메인 리로드로 되돌아감 → 반드시 씬 저장.
- `Client.unity`에 **누락 프리팹**(XR Interaction Hands Setup, guid `3182b5074a6bf3d4887a44a38cbb9cb6`) — 비치명적 경고.
- room.log에 `RoomNetworkManager.cs:96 NullReferenceException` (서버 연결상태 핸들러, 기존 데모 이슈) — 방 등록은 정상 진행됨.
- **Client + Room 동시 로드** 시 NetworkManager 다중(C-RoomServer + R-RoomServer + MotionSystemNetworkManager) → 깨끗한 클라 테스트는 Client 단독 권장(방 입장 시 Room이 클라로 로드됨).
- Server subtarget 빌드는 **Windows Dedicated Server 모듈** 필요.

---

## 7. 하네스 검증 신호 요약 (머신 체크 가능)

| 단계 | 통과 조건 |
|---|---|
| 컴파일 | `console-get-logs(Error)` 에 `error CS` 0건 |
| 빌드 | exe + `_Data/` + `application.cfg` 존재; (스크립트로 잡으면) `BuildReport.summary.result==Succeeded && totalErrors==0` |
| 서버 런타임 | master.log `listening to: .*:5000` + `Spawner successfully created`; room.log `Room registered successfully` |
| 클라 연결 | Play 후 master.log `opened connection` 카운트 증가; "connection lost" 부재 |

---

## 부록: 검증 당시 실측값
- 머신 LAN IP: `192.168.50.49` (cfg에 자동기록), Master `:5000`, Room `:7777`.
- 등록된 방: `Room-1A5C-ED20-3935`, ID 0, MaxConnections 10, Public.
- 빌드 시간: Master+Room 합 ~48초(어셈블리 캐시된 상태). 첫 빌드는 더 김.
- exe ~652KB + `_Data` (Master 131MB / Room 171MB).
