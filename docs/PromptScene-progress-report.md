# PromptScene 진행 보고서

> 작성 2026-06-12. 대상: XRCollabDemo(Unity 6000.3.11f1) 기반 룸 합성/검증.

---

## 1. 목표
**자연어 프롬프트로 XRCollabDemo에 ROOM 씬을 합성하고, 실제로 돌아가는지(입장·아바타·기능) 구조적으로 검증**하는 시스템(PromptScene). 두 층:
- (런타임) Unity = 얇은 코어 + 옵트인 콘텐츠 모듈 + 런치패드
- (저작) Claude Code 스킬 = MCP로 룸을 조립/검증하는 `/promptscene`, `/RoomContent-*`

---

## 2. 한 줄 요약 — 현재 상태
**"문서만 보고 작동하는 룸을 빌드"하는 것을 실제로 증명 완료.** 룸 베이스(네트워크+아바타+UI전환) + 콘텐츠 플러그인 아키텍처 + 첫 기능(Ruler) 파일럿까지 동작. 다음은 런치패드 UI와 스킬화.

---

## 3. 작동 검증 (핵심 증거)

### 3-1. 문서 기반 재현 — `BasicRoom` (가장 중요)
`docs/build-working-room.md` **하나만 보고** 새 룸을 처음부터 조립 → 빌드 → 실행 → 검증. 결과(실측):
- 서버: `Online Scene: BasicRoom` 등록 + `Client 0 is successfully validated`
- 에디터 클라: **Client(로비) 씬 언로드 / BasicRoom 로드 / C-MasterCanvas 소멸** (UI 자동 전환)
- **NetworkObjects=5**, 소유 `Desktop(Clone)`+`mixamorig:Hips`+플랫폼루트 전부 `scene=BasicRoom` (아바타 룸에 스폰)
- 아바타 카메라 활성, 게임뷰에 룸 표시(로비 가림 없음)

→ **"문서/패키지만으로 작동 룸 재현 가능"이 성립.**

### 3-2. 콘텐츠 플러그인 파일럿 — Ruler
`RulerContent : IToggleableContent`가 `RoomCore`에 자기등록 → `SetEnabled(true)` → 두 점 측정선+거리 라벨 렌더(4.00m) 확인. DeepChairProject 앱레이어 의존 0(계약 위에 클린 재구현).

---

## 4. 핵심 성과 / 발견

### 4-1. 룸 작동 불변식 C1~C4 (이거 안 맞으면 안 돈다 — 전부 실측으로 발굴)
- **C1 컬렉션 일치**: 룸서버·클라 NM 모두 `DefaultPrefabObjects`. (불일치 시 입장되나 아바타 안 보임)
- **C2 플레이어 스포너**: `XumPlayerSpawner`(+`XumSimpleSpawnServerExample`+`NetworkObject`+`XumNetwork`+`sp`), 플랫폼 카탈로그로 Desktop/UnityXR 스폰. 패키지 `R-PlayerSpawner`(Example Cube)는 금지.
- **C3 씬 전환**: `R-RoomServer.DefaultScene` `_onlineScene=<룸>`, **`_offlineScene=Client`**. (offline 비면 로비가 룸 위에 남음)
- **C4 토폴로지**: 서버=Master+Room exe / 에디터=Client+Room 동시 로드.

### 4-2. 스포너는 "프리팹"이어야 한다 (from-docs 재현이 찾아낸 갭)
스포너를 **스크립트로 AddComponent해 만들면** NetworkObject가 유효 scene id를 못 받아 → 입장 시 **"Failed to confirm the access"로 거부**(ReproRoom 실측). **프리팹으로 인스턴스화하면 통과**(BasicRoom 실측). → 스포너를 `Assets/PromptScene/Prefabs/Room-PlayerSpawner.prefab`로 패키지화.
> 부수 발견: DocRoom이 "됐던" 것도 사실은 스포너를 Room.unity에서 **클론**했기 때문 — 순수 문서만으론 안 됐음. 이 사실을 문서+프리팹으로 메꿔 진짜 재현 가능하게 함.

### 4-3. 콘텐츠 플러그인 아키텍처 (업계 표준 = UE5 Modular Game Features)
- **SYSTEMS(Core)**: 특정 기능을 컴파일타임에 **모름**. `RoomCore`가 서비스(`IRoomCore`)+레지스트리 제공.
- **FEATURES(Content)**: `IToggleableContent`로 `RoomCore.Instance`에 **자기등록**. 빼면 코어가 모름(안 깨짐). 판별: "빼도 안 깨지면 FEATURE".

### 4-4. 씬 계층 표준 (업계 관행 기반)
`===== SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC =====`

---

## 5. 산출물(deliverable) — 위치 & 구조
**폴더 통째로 올리지 말 것** (XRCollabDemo clone 수GB + scratch 포함). 결과물은 다음 3가지:

| 산출물 | 경로 | 비고 |
|---|---|---|
| **문서** | `c:\J_0\docs\*.md` | 계약·절차·이 보고서 |
| **런타임 코드/프리팹** | `XRCollabDemo\Assets\PromptScene\` | Core(계약+RoomCore) + Content(Ruler) + Prefabs(Room-PlayerSpawner) → UPM/복사용 추출 대상 |
| **Claude Code 스킬** | `.claude\skills\*` (Phase 4·5 예정) | `/promptscene`, `/RoomContent-*` |

> XRCollabDemo 자체는 **clone + 1회 수정**(부록)으로 재생성 가능 → 커밋 대상 아님.

---

## 6. 문서 목록 (`c:\J_0\docs\`)
- **build-working-room.md** — 작동 룸 조립 절차(스포너 구성 포함, 자급자족). ⭐ 이번 핵심.
- **build-xumlobby-server.md** — Master/Room.exe 빌드 + 런타임 검증 절차.
- **promptscene-content-contract.md** — 콘텐츠 계약(인터페이스/계층/C1~C4/하네스).
- **PromptScene-progress-report.md** — 본 보고서.

---

## 7. 남은 로드맵
- **Phase 3 런치패드 UI** — 레지스트리→아이콘 그리드→`SetEnabled`(스마트폰식 토글).
- **Phase 4 스킬화** — `/RoomContent-<feature>` + LLM 신규기능 생성 템플릿.
- **Phase 5 합성+하네스** — `/promptscene`(자연어→룸 조립) + C1~C4/콘텐츠 자동 검증.

---

## 부록: 셋업 메모
- **XRCollabDemo** (Unity 6000.3.11f1, MCP 27826): clone 후 com.oxr.sdk embed + XumLobby API 패치 + Dedicated Server 모듈 (build-xumlobby-server.md §1).
- **DeepChairProject** (Unity 6000.1.7f1, MCP 22863): 기능 레퍼런스 소스. 열 때 manifest `xrbuildkit→xumbuildkit`, `com.oxr.coop` embed 후 중복 샘플(Bim Editor/FileDownload/MultiScale) 제거.
- 검증 시 서버 IP: 마스터 `192.168.50.49:5000`, 룸 `:7777`. 클라 `serverIp`는 마스터와 일치 필수.
