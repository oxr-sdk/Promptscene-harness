# 작동하는 ROOM 만들기 — 검증된 조립 절차 (PromptScene)

> 목적: **이 문서 하나만 읽고** XRCollabDemo에서 **실제로 입장·아바타 스폰·이동이 되는 ROOM 씬**을 처음부터 조립한다.
> 기존 출처(패키지 README, https://oxr-platform.gitbook.io/oxr-platform-docs)는 "룸 레시피"는 주지만 **플레이어 스포너 구성과 작동 불변식(C1~C4)이 빠져 있어 그대로 따라하면 안 돈다.** 이 문서가 그 빈틈을 채운 "되는 문서"다.
> 검증 기반: DocRoom(이 절차로 만든 작동 룸) + 빌드/런타임 검증(`build-xumlobby-server.md`) + 계약(`promptscene-content-contract.md`).
> 관련: **서버(.exe)** 빌드 = `build-xumlobby-server.md` · **실기기(Meta Quest) 클라(APK)** 빌드·배포 = `build-meta-client.md`.

---

## 0. 전제 (1회성, 이미 적용됨)
`build-xumlobby-server.md` §1의 프로젝트 수정(com.oxr.sdk embed, XumLobby API 패치, Sample 재임포트, Dedicated Server 모듈)이 끝나 **컴파일 0에러**여야 한다.

핵심 자원 경로:
- 룸 프리팹 세트: `Packages/com.kisti.xumlobby/Runtime/Prefabs/2) Room Scene/` (R-RoomServer, R-RoomClient, R-ConnectionToMaster, R-MasterCanvas, R-PlayerSpawner)
- 프리팹 컬렉션: `Assets/DefaultPrefabObjects.asset`
- 플레이어 스포너(아래 §2에서 만든): `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab`
- 플랫폼 아바타: `Assets/App/Prefabs/Desktop.prefab`(Windows) / `UnityXR.prefab`(Meta) — DefaultPrefabObjects에 등록돼 있어야 함

---

## 1. ROOM 씬 뼈대 (SYSTEMS)
새 씬을 만들고(`<Room>.unity`) 아래 **4개** R- 프리팹을 인스턴스화한다:

| 프리팹 | 역할 |
|---|---|
| `R-RoomServer` | FishNet NetworkManager + RoomNetworkManager + RoomServerManager + **DefaultScene** = 룸 두뇌 |
| `R-RoomClient` | RoomClientManager |
| `R-ConnectionToMaster` | ClientToMasterConnector (마스터 접속) |
| `R-MasterCanvas` | 룸 HUD/접속 UI |

> ⚠️ **R-PlayerSpawner(5번째)는 쓰지 않는다.** 그건 패키지 샘플용으로 기본 플레이어가 `Example Cube`라 앱 클라(DefaultPrefabObjects)와 안 맞는다. 대신 §2의 스포너를 쓴다.

---

## 2. 플레이어 스포너 (가장 중요 — 문서로 못 박는 부분)

### 2-1. 왜 직접 못 만들고 프리팹이어야 하나
플레이어 스폰 오브젝트는 **씬 NetworkObject**다. 스크립트로 `AddComponent<NetworkObject>()`해서 만들면 **유효한 scene id를 못 받아** FishNet 서버 씬오브젝트 초기화가 깨지고 → 입장 시 **"Failed to confirm the access"로 거부**된다(실측). 따라서 **프리팹으로 만들어 인스턴스화**해야 한다(프리팹 인스턴스는 저장 시 정상 scene id를 받음, R- 프리팹과 동일 원리).

### 2-2. 스포너 프리팹 구성 스펙 (`Room-PlayerSpawner.prefab`)
하나의 GameObject `--PLAYER_SPAWNER`에 아래 컴포넌트:

| 컴포넌트 | 설정 |
|---|---|
| `XumPlayerSpawner` | `_onClientSpawn = true`, `_playerCatalog = Player Prefab Catalog.asset`, `_spawnTable = Player Spawn Table.asset` |
| `XumSimpleSpawnServerExample` | `_object = SharedObject`(NetworkObject 프리팹), `_spawnPoint = null` |
| `FishNet.Object.NetworkObject` | `_isNetworked=true, _isSpawnable=true, _isGlobal=false` (기본값) |
| `XumNet.XumNetwork` | (직렬화 필드 없음) |
| 자식 `sp` | 빈 Transform = 스폰 기준점 |

> 동작: `XumPlayerSpawner.OnStartClient → SpawnPlayer()`가 `DetectRuntimePlatform.Current`로 카탈로그에서 플랫폼 아바타(Desktop/UnityXR)를 골라 `XumNetwork.Instantiate`로 스폰. → 데스크톱에서 키보드/마우스로 움직이는 아바타가 뜬다.
> 한번 만들어 두면 `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab`로 재사용. 모든 룸은 이걸 인스턴스화한다.

룸 씬에 이 프리팹을 인스턴스화한다.

---

## 3. 작동 불변식 C1~C4 (이거 안 맞으면 안 돈다)

| # | 계약 | 정확한 값 |
|---|---|---|
| **C1** | 프리팹 컬렉션 일치 | `R-RoomServer`의 NetworkManager `_spawnablePrefabs` = **DefaultPrefabObjects** (클라 C-RoomServer와 동일해야 함). 절대 SampleSpawnObjects 금지 |
| **C2** | 플레이어 스폰 | §2의 `Room-PlayerSpawner` 프리팹 사용 (R-PlayerSpawner/Example Cube 금지) |
| **C3** | 씬 전환 | `R-RoomServer`의 **DefaultScene**: `_onlineScene = "Assets/App/Scenes/<Room>.unity"`, **`_offlineScene = "Assets/App/Scenes/Client.unity"`**. `R-RoomClient.offlineRoomScene = "Client"`. ← offline 비우면 입장해도 로비가 안 사라짐 |
| **C4** | 실행 토폴로지 | 서버 = Master.exe + Room.exe 둘 다 / 에디터 = **Client + Room 씬 동시 로드**로 Play |

추가: 새 룸을 **EditorBuildSettings**(클라가 네트워크로 로드)와 **Room.exe 빌드 SceneList**(서버)에 넣어야 함.

---

## 4. 환경 / 콘텐츠
- ENVIRONMENT: 바닥(Plane, 콜라이더 필수 — 레이캐스트/이동), 벽, 조명, 카메라. (또는 기존 3D 모델 차용)
  - ⚠️ **VR 클라 배포 시**: 룸 씬의 `Main Camera`(Camera+AudioListener)는 **비활성화 + 태그 Untagged**. 안 그러면 Quest에서 XR 리그 카메라와 충돌해 화면 깜빡임+고정. 에디터/데스크톱 검증엔 켜둬도 무방. ☞ `build-meta-client.md` §2.4-D
- (선택) FEATURES: `RoomCore`(PromptScene.Core) + `RulerContent` 등 토글 콘텐츠. → `promptscene-content-contract.md`

---

## 5. 표준 씬 계층
```
<Room>.unity
├── ===== SYSTEMS =====    Network(R-RoomServer/Client/ConnectionToMaster) · Player(--PLAYER_SPAWNER) · RoomCore
├── ===== ENVIRONMENT ===== Floor/Walls/Lighting/Camera
├── ===== UI =====          R-MasterCanvas
├── ===== FEATURES =====    (옵트인 콘텐츠)
└── ===== _DYNAMIC =====    런타임 생성물
```

---

## 6. 빌드 & 실행 & 검증
1. **빌드**: `XumLobbyServerBuilderWindow`로 Master.unity + `<Room>.unity`를 Room.exe로 빌드 (`build-xumlobby-server.md` §2-B). 성공 신호 = `Room/application.cfg` 생성.
2. **서버 실행**: MasterAndSpawner.exe → (6초 후) Room.exe. 검증: master.log `listening to .*:5000` + `Spawner successfully created`; room.log `Online Scene: <Room>` + `Room registered successfully`.
3. **에디터 클라**: `Client.unity`(Single) + `<Room>.unity`(Additive) 로드. `C-ClientMasterConnector.serverIp` = 마스터 IP(예 192.168.50.49)로 맞추고 씬 저장. Play.
4. **입장**: 게스트 자동인증 후 `Mst.Client.Matchmaker.FindGames` → `MatchmakingBehaviour.StartMatch(games[0])`.
5. **작동 판정 (전부 충족해야 "됨")**:
   - room.log: `Client N has become a player` (← "Failed to confirm the access"면 C2 스포너 문제)
   - 에디터: `Client` 씬 언로드됨(`MovedObjectsHolder` 등장) = 로비 자동 소멸 (C3)
   - NetworkObjects에 `Desktop(Clone)`(owner=True) + 아바타 카메라 활성
   - 게임뷰에 룸이 보이고 WASD로 이동

---

## 7. 흔한 실패 → 원인
| 증상 | 원인 |
|---|---|
| 입장은 되는데 아바타 안 보임 | C1 위반(룸=SampleSpawnObjects, 클라=DefaultPrefabObjects 불일치) |
| 로비 UI가 룸 위를 덮음 | C3 `_offlineScene` 비어있음 |
| "Failed to confirm the access"로 즉시 퇴장 | C2 스포너를 스크립트로 만들어 NetworkObject scene id 무효 → **프리팹으로 인스턴스화할 것** |
| 아바타가 Client 씬에 스폰/룸 미로드 | 네트워크 씬 전환 미발동 (C3 online scene 미설정 or 빌드세팅 누락) |
| (VR) 룸 입장 후 화면 깜빡임+시점 고정 | 룸 씬의 Main Camera가 XR 리그 카메라와 충돌 → 룸 Main Camera(Camera+AudioListener) 비활성+Untagged (build-meta-client.md §2.4-D) |
| 에디터+Quest 동시 접속 시 "Room could not validate you" | 둘 다 게스트 로그인 → 게스트 아이디 충돌. 각 클라를 서로 다른 계정으로 (build-meta-client.md §7) |
| (VR APK) 룸 입장 시 "Room could not validate you" + 씬 로드 실패 | 클라 빌드 SceneList에 룸 씬 누락 → FishNet이 이름으로 룸 씬 로드 불가. Client+룸 씬 모두 포함 (build-meta-client.md §2.4-C) |
