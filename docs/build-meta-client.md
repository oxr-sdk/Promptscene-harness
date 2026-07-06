# XRCollabDemo — Meta Quest 클라이언트 빌드 & 온-디바이스 배포 (검증된 절차)

> 이 문서는 실제로 Quest 3에 올려 **마스터 접속 → 룸 입장 → 이동**까지 확인한 절차입니다.
> `build-xumlobby-server.md`가 **서버(.exe)** 빌드를 다룬다면, 이 문서는 그 짝인 **클라이언트(APK)** 빌드/배포를 다룹니다.
> 대상: `c:\J_0\XRCollabDemo`, Unity **6000.3.11f1**, 빌드 도구: **Xum Build Kit (XRConfigPresets)** 의 Meta 디바이스 프리셋.

---

## 0. 무엇을 빌드하나
`Client.unity`(+ 룸 씬)를 **Android / IL2CPP / ARM64 + OpenXR(Meta Quest)** APK로 빌드해 Quest에 설치.
- 산출물: `Builds/App/Client/Meta-v1.3.4/XRCollabDemo.apk` (~117MB)
- 접속 대상: 같은 LAN의 Master 서버(`build-xumlobby-server.md`로 띄운 것).

---

## 1. 사전 조건
1. `build-xumlobby-server.md` §1의 프로젝트 수정(com.oxr.sdk embed, XumLobby API 패치, Sample 재임포트)이 끝나 **컴파일 0에러**.
2. Unity 에디터에 **Android Build Support + NDK + SDK + OpenJDK + IL2CPP** 설치. 확인 경로: `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/{NDK,SDK,OpenJDK,Variations/il2cpp}` 모두 존재.
3. **Xum Build Kit 디바이스 프리셋 임포트됨**: 프로젝트 루트에 `XumBuildKit/{CustomProjectSettings,CustomXRSettings,Plugins}/Android/Meta-v1.3.4` 존재. (Xum Build Kit → Settings → Device Presets → Import Demo Device Presets)
4. **Meta 프리셋은 순수 OpenXR 경로** — Meta XR SDK(`com.meta.xr.*`) 설치 불필요. Android XR 로더 = `OpenXRLoader`, OpenXR feature에 Meta Quest/Oculus Quest 활성. (검증: `manifest.json`에 meta.xr 없음에도 정상 빌드/구동)

---

## 2. 빌드 절차 (MCP `script-execute`로 헤드리스 구동, 검증됨)

> ⚠️ **정식 `XumLobbyClientBuilderWindow.RunBuildPipeline`을 reflection으로 그대로 돌리면 MCP 환경에서 실패한다.** 이유는 §2.4의 함정 두 개. 아래는 그걸 우회한 **직접 BuildPlayer** 경로다.

### 2.1 Android 타깃 전환 + Meta 프리셋 적용
```csharp
EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
XumBuildKit.Editor.Utility.XRSettingsUtility.LoadPreset("Meta-v1.3.4", "Android");
```
`LoadPreset`이 `CustomXRSettings/Android/Meta`(OpenXR 로더 포함) → `Assets/XR`, `CustomProjectSettings` → `ProjectSettings`로 복사한다. **이 프리셋 적용 경로가 XumBuildKit의 "XR Plug-in Management 프로바이더가 빌드에 반영 안 되는 버그"를 우회한다** — 수동 UI로 프로바이더만 체크하면 저장이 누락되지만, 프리셋 적용은 디스크에 확실히 박는다. (검증: 빌드 후 `Android XR loaders = OpenXRLoader`)

### 2.2 필수 스크립팅 디파인 영구 설정 (⚠️ 빌드 전에)
현재 활성 Android 디파인엔 `FISHNET;FISHNET_V4;USE_XUM_LOBBY;...`는 있지만 **`UNIXR_USE_FISHNET`, `EDGEGAP_PLUGIN_SERVERS`가 없다.** 이 둘을 **먼저** 영구 추가하고 재컴파일을 끝내둔다:
```csharp
var nbt = NamedBuildTarget.Android;
var defs = PlayerSettings.GetScriptingDefineSymbols(nbt) + ";UNIXR_USE_FISHNET;EDGEGAP_PLUGIN_SERVERS";
PlayerSettings.SetScriptingDefineSymbols(nbt, defs);
AssetDatabase.SaveAssets();   // 강제종료 대비 flush
// → 재컴파일/도메인 리로드 발생. isCompiling=false 될 때까지 대기 후 §2.3.
```

### 2.3 씬 리스트 + 패키지명 + cfg + IP 주입 + 빌드
```csharp
// 씬: Client(부트, index 0) + 룸 씬. 룸 씬은 반드시 포함해야 함 (§2.4-C)
string[] scenes = { "Assets/App/Scenes/Client.unity", "Assets/App/Scenes/BasicRoom_3.unity" };
// 패키지명: 기본 urpblank와 충돌 피하려 고유 id
PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kisti.xrcollabdemo");
// application.cfg → StreamingAssets (Quest 런타임이 읽는 접속 설정)
File.WriteAllText("Assets/StreamingAssets/application.cfg",
  "-mstStartClientConnection=True\n-mstMasterIp=192.168.50.49\n-mstMasterPort=5000");
AssetDatabase.Refresh();
// Client.unity의 ClientToMasterConnector.serverIp/serverPort 주입 (씬 파일 backup→주입→빌드→restore)
// 빌드 (extraScriptingDefines 없이! §2.4-A)
var opts = new BuildPlayerOptions {
  scenes = scenes,
  locationPathName = "Builds/App/Client/Meta-v1.3.4/XRCollabDemo.apk",
  target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android,
  options = BuildOptions.None };
var report = BuildPipeline.BuildPlayer(opts);   // 블로킹 → MCP "Response data is null"은 정상
```
`BuildPlayer`가 메인 스레드를 블로킹 → MCP는 `Response data is null` 타임아웃 → **정상**. 결과는 APK 산출물로 확인.

### 2.4 반드시 알아야 할 함정
| # | 함정 | 원인 & 대응 |
|---|---|---|
| **A** | `extraScriptingDefines` → 도메인 리로드 | 정식 파이프라인은 `extraScriptingDefines={FISHNET,FISHNET_V4,EDGEGAP_PLUGIN_SERVERS,UNIXR_USE_FISHNET}`를 넘긴다. 이게 **활성 디파인과 다르면 Unity가 재컴파일+도메인 리로드**를 일으켜 MCP의 Roslyn 스크립트를 중단시킨다(→ `Response data is null` + pipe broken + APK 미생성). **대응: §2.2로 디파인을 미리 박아두고, BuildPlayer는 `extraScriptingDefines` 없이 호출.** |
| **B** | `PreparePresetForBuild`가 디파인을 되돌림 | 파이프라인은 빌드 직전 `PreparePresetForBuild → LoadPreset`으로 프리셋 ProjectSettings를 **다시 덮어써서** §2.2에서 넣은 디파인을 날린다. **대응: 정식 파이프라인 대신 직접 `BuildPlayer` 호출**(프리셋은 §2.1에서 이미 적용됨). |
| **C** | 룸 씬 미포함 → 입장 실패 | FishNet은 룸의 Online Scene을 **이름으로 클라에서 로드**한다. 씬 리스트에 룸(`BasicRoom_3`)이 없으면 `Scene 'BasicRoom_3' couldn't be loaded` + `[Error\|RoomClientManager] Room could not validate you`. **대응: 클라 빌드 SceneList에 Client + 룸 씬 모두 포함**(EditorBuildSettings도). 룸의 온라인 씬 이름 = `R-RoomServer.DefaultScene._onlineScene` / room.log `Online Scene: <Room>`. |
| **D** | 룸 씬의 Main Camera가 VR에서 깜빡임 | 룸 씬(`BasicRoom_3`)이 자체 `Main Camera`+`AudioListener`를 가지면, 유지되는 XR 리그 카메라와 **둘 다 활성** → 화면 깜빡 + 고정("2 audio listeners" 경고). **대응: 룸 씬의 Main Camera(Camera+AudioListener) 비활성 + 태그 Untagged**(→ `Camera.main`이 XR 리그로 잡힘). ☞ build-working-room.md와 공유되는 이슈. |
| **E** | Active Input Handling | 기본 "Both"는 Android 빌드 때마다 경고 다이얼로그를 띄운다(무시 가능하나 빌드 여러 개 쌓이면 루프). "New(1)"로 바꾸면 사라지지만 **레거시 `Input.GetAxis` 코드가 깨진다**(`DummyController.cs` → 매 프레임 InvalidOperationException + 이동 불가). **대응: activeInputHandler=1 + DummyController를 New Input(`Keyboard.current`)으로 전환**(적용에 에디터 재시작 필요). |

---

## 3. 빌드 산출물 = 성공 판정
```
Builds/App/Client/Meta-v1.3.4/XRCollabDemo.apk   (~117MB, result=Succeeded)
```
APK 내부(zip) 검증 엔트리:
- `lib/arm64-v8a/libil2cpp.so` (~150MB, IL2CPP 네이티브)
- `lib/arm64-v8a/libopenxr_loader.so`, `libUnityOpenXR.so`, `libUnityOpenXRHands.so` (OpenXR/핸드)
- `lib/arm64-v8a/libunity.so`, `AndroidManifest.xml`, `assets/application.cfg`(=박힌 접속 설정)
- 빌드 로그에 `ModifyAndroidManifestMeta/Oculus...OnPostGenerateGradleAndroidProject` 훅이 뜨면 = Meta Quest 타깃 빌드 확정.

---

## 4. 배포 & 실행 (adb)
adb 경로: `<Unity>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe`
```powershell
adb devices                                  # Quest 상태 확인
adb install -r <apk>                         # 같은 패키지=덮어쓰기, 새 패키지=신규
adb shell monkey -p com.kisti.xrcollabdemo -c android.intent.category.LAUNCHER 1   # 실행
adb shell pidof com.kisti.xrcollabdemo       # 실행 확인
```
- **`unauthorized` 함정**: Quest는 절전/재연결/`adb kill-server` 후 자주 `unauthorized`로 돌아간다. → 헤드셋 착용 후 **"USB 디버깅 허용"(항상 허용)** 탭. 그래야 install 가능.
- **`INSTALL_FAILED_UPDATE_INCOMPATIBLE`**: 같은 패키지 id가 **다른 서명**으로 이미 설치돼 있을 때. → **패키지명 변경 후 재빌드**(§2.3의 `SetApplicationIdentifier`)로 기존 앱 안 지우고 나란히 설치. (기존 앱 제거는 사용자가 원치 않음)

---

## 5. 런타임 검증 신호
- 클라 logcat: `[Info | ClientToMasterConnector] connected to server at: 192.168.50.49:5000` (초 단위 접속)
- master.log: `Peer [N] opened connection` + `Client N connected to server`
- netstat: `192.168.50.49:5000  <Quest LAN IP(예 192.168.50.43)>  ESTABLISHED`
- 룸 입장 후: 깜빡임 없이 XR 시점 유지(§2.4-D 적용 시), 룸 이동 동작.

---

## 6. 원격 클라(Quest)를 위한 룸 서버 IP
- 수동 `Room.exe` 실행 시 기본 `RoomIp:127.0.0.1`로 등록됨 → **원격 Quest가 못 붙는다.** 
- 대응: `Room.exe -mstRoomIp 192.168.50.49 -mstMasterIp 192.168.50.49 -mstMasterPort 5000`으로 실행 → `RoomIp:192.168.50.49` 등록. (또는 스포너 자동 스폰에 맡기면 master cfg의 `-mstRoomIp`가 전달됨)
- 클라 cfg의 `mstMasterIp`와 서버가 바인딩한 IP가 **반드시 일치**해야 함. Quest는 별도 기기라 `127.0.0.1` 금지 — 마스터 PC의 LAN IP 사용.

---

## 7. 멀티클라이언트 / 게스트 충돌 함정
에디터 클라 + Quest 클라를 **둘 다 "게스트"로 로그인**하면 같은 게스트 아이디가 배정돼 룸이 `Failed to confirm the access` / `Room could not validate you`로 거부할 수 있다(실측). → **각 클라를 서로 다른 계정으로 로그인.** (계정 분리하니 정상 입장 확인)

room 서버측 로그로 구분: `Client N is successfully validated`(성공) vs `Failed to confirm the access`(거부).

---

## 8. 미해결 / TODO
- **로비/서버 캔버스가 서 있는 눈높이보다 낮게 보임.** 후보 원인: 로비 캔버스가 `Xreal` 리그 하위 y=1.6에 있는데 XR Origin은 `Meta` 리그(`trackingMode=Floor`, `camYOffset=1.36`). 캔버스를 카메라 기준으로 재배치하거나 리그/오프셋 정합 필요. (기능엔 지장 없음)

---

## 부록: 오늘 검증 실측값
- 기기: **Quest 3** (Oculus), LAN IP `192.168.50.43`. 마스터 PC LAN IP `192.168.50.49`.
- 패키지: `com.kisti.xrcollabdemo` (기본 `com.UnityTechnologies.com.unity.template.urpblank`와 별도 설치).
- 룸: `BasicRoom_3` (온라인 씬), 서버 Room.exe가 `RoomIp:192.168.50.49:7777`로 등록.
- 코드 수정: `DummyController.cs` → New Input System(WASD/방향키). `ProjectSettings` `activeInputHandler: 2→1`.
- 씬 수정: `BasicRoom_3.unity`의 `Main Camera` 비활성(Camera+AudioListener) + 태그 Untagged.
