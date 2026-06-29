> **발표 흐름 요약 (3줄)**
> 1. **문제**: 기존 XR 플랫폼 문서만으로는 실제 작동하는 룸을 만들 수 없었다 — 핵심 불변식이 빠져 있었기 때문이다.
> 2. **증명**: 그 빈틈을 채운 문서(`build-working-room.md`) 하나만 보고 BasicRoom을 처음부터 빌드·실행·검증 완료 → `NetworkObjects=5`, 아바타 스폰, 로비 자동 소멸까지 전부 실측.
> 3. **다음**: 런치패드 UI(Phase 3) → 스킬 자동화(Phase 4~5)로 자연어 프롬프트 → 작동 룸을 완성한다.

---

# PromptScene 발표 보고서

**자연어 프롬프트 한 줄로 XR 협업 룸을 만들고, 그게 실제로 작동하는지 구조적으로 증명한다.**

> 작성 2026-06-15 / 기반 환경: XRCollabDemo (Unity 6000.3.11f1)

---

## 1. 왜 하는가

XR 협업 플랫폼(XRCollabDemo)에서 새 룸을 만들려면 네트워크·아바타·UI 전환이 모두 맞아야 한다. 기존 패키지 문서와 공식 가이드는 "룸 레시피"를 주지만, **플레이어 스포너 구성법과 4개의 작동 불변식이 빠져 있어 그대로 따라 하면 룸이 돌지 않는다.** PromptScene은 그 빈틈을 발굴하고 문서화해, "문서만 보고 작동 룸을 재현"할 수 있게 하는 것이 1단계 목표다. 장기적으로는 Claude Code 스킬(AI)이 이 문서를 읽고 룸을 자동 조립·검증하는 시스템으로 확장한다.

---

## 2. 접근 방식 — 두 층 구조

| 층 | 도구 | 역할 |
|---|---|---|
| **런타임 (Unity)** | `얇은 Core` + 옵트인 콘텐츠 모듈 | 작동하는 룸 구조 제공 |
| **저작 (Claude Code)** | `/promptscene`, `/RoomContent-*` 스킬 | MCP로 룸을 조립·검증하는 AI |

핵심 원칙: Core는 개별 기능의 존재를 **컴파일타임에 모른다.** 기능 모듈이 스스로 레지스트리에 등록한다.

---

## 3. ⭐ 핵심 증명: "문서만 보고 작동 룸 빌드"

`docs/build-working-room.md` **한 파일만 보고** BasicRoom을 처음부터 조립 → 빌드 → 실행 → 검증했다.

### 실측 결과 (전부 충족해야 "됨")

| 확인 항목 | 실측 값 |
|---|---|
| 서버 룸 등록 | `Online Scene: BasicRoom` + `Client 0 is successfully validated` |
| 로비 자동 소멸 | 에디터: `Client` 씬 언로드 / `C-MasterCanvas 소멸` |
| 아바타 스폰 | `NetworkObjects=5`, `Desktop(Clone)`(owner=True) + `mixamorig:Hips` → scene=BasicRoom |
| 화면 | 아바타 카메라 활성, 게임뷰에 룸 표시 (로비 가림 없음) |
| 이동 | WASD 키보드 조작 정상 |

**결론: "문서/패키지만으로 작동 룸 재현 가능"이 성립한다.**

---

## 4. 콘텐츠 플러그인 파일럿 — Ruler

`RulerContent : IToggleableContent`가 `RoomCore`에 자기등록 → `SetEnabled(true)` 호출 → 두 점 사이 측정선 + 거리 라벨 **4.00m** 렌더 확인.

- DeepChairProject(앱 레이어) 의존 = **0**.
- 계약(`IToggleableContent`) 위에 클린 재구현 → 다른 룸에도 그대로 이식 가능.

---

## 5. 핵심 발견

### 5-1. 룸 작동 불변식 C1~C4

> 이 네 가지 중 하나라도 어긋나면 룸이 돌지 않는다. 전부 실측으로 발굴했다.

| # | 계약 | 어길 때 증상 |
|---|---|---|
| **C1** 프리팹 컬렉션 일치 | 룸서버·클라 NetworkManager 모두 `DefaultPrefabObjects` | 입장은 되나 아바타 안 보임 |
| **C2** 플레이어 스포너 | `XumPlayerSpawner` + `NetworkObject` + `XumNetwork` + 자식 `sp` — **프리팹으로 인스턴스화** | `"Failed to confirm the access"` 즉시 퇴장 |
| **C3** 씬 전환 | `R-RoomServer.DefaultScene`: `_onlineScene=<룸>`, **`_offlineScene=Client.unity`** 필수 | 로비 UI가 룸 위에 남음 |
| **C4** 실행 토폴로지 | 서버 = Master.exe + Room.exe / 에디터 = Client + Room 동시 로드 | 씬 전환 미발동 / 아바타 Client에 스폰 |

### 5-2. ⚠️ "스포너는 프리팹이어야 한다" — 문서 재현이 찾아낸 갭

스포너를 스크립트로 `AddComponent<NetworkObject>()` 해서 만들면 **FishNet이 씬 ID를 부여하지 못해** 서버 씬 오브젝트 초기화가 깨진다. 반드시 **프리팹으로 인스턴스화**해야 정상 씬 ID를 받는다.

- ReproRoom (스크립트 방식) → `"Failed to confirm the access"` 실측.
- BasicRoom (프리팹 방식) → `"Client 0 is successfully validated"` 실측.

> 기존 DocRoom이 "됐던" 것도 사실은 Room.unity에서 스포너를 **클론**했기 때문이었다 — 순수 문서만으론 안 됐음. 이 사실을 발굴해 `Room-PlayerSpawner.prefab`으로 패키지화함으로써 진짜 재현 가능 상태로 만들었다.

### 5-3. 콘텐츠 플러그인 아키텍처 — UE5 Modular Game Features와 동일 원리

```
UE5:         GameFeature Plugin   → 자기등록 → Game Feature Subsystem
PromptScene: IToggleableContent   → 자기등록 → RoomContentRegistry (IRoomCore)
```

- **SYSTEMS(Core)**: 특정 기능을 컴파일타임에 **모른다.** `RoomCore`는 레지스트리와 서비스만 제공.
- **FEATURES(Content)**: `Awake`에서 `RoomCore.Instance.Contents.Register(this)` 한 줄로 등록. 빼도 Core가 모르므로 빌드·런타임 안 깨진다.
- **판별 테스트**: *"이 모듈을 빼도 프로젝트가 안 깨지나?"* → 안 깨지면 FEATURE.

---

## 6. 산출물 위치·구조

| 산출물 | 경로 | 비고 |
|---|---|---|
| **문서** | `c:\J_0\docs\*.md` | 계약·절차·보고서 |
| **런타임 코드·프리팹** | `XRCollabDemo\Assets\PromptScene\` | Core(계약+RoomCore) + Content(Ruler) + Prefabs(Room-PlayerSpawner) |
| **Claude Code 스킬** | `.claude\skills\*` | `/promptscene`, `/RoomContent-*` (Phase 4·5 예정) |

> XRCollabDemo 자체는 clone + 1회 수정으로 재생성 가능 → 커밋 대상 아님.

---

## 7. 남은 로드맵

| Phase | 내용 |
|---|---|
| **Phase 3** | 런치패드 UI — 레지스트리 → 아이콘 그리드 → `SetEnabled` 토글 (스마트폰식) |
| **Phase 4** | 스킬화 — `/RoomContent-<feature>` + LLM 신규 기능 생성 템플릿 |
| **Phase 5** | 합성+하네스 — `/promptscene`(자연어 → 룸 조립) + C1~C4/콘텐츠 자동 검증 |

---

## 핵심 용어 한 줄 설명

| 용어 | 설명 |
|---|---|
| **NetworkObject** | FishNet이 네트워크 상에서 추적하는 GameObject 단위. 씬에 배치 시 씬 ID를 부여받아야 서버가 인식한다. |
| **스포너 (XumPlayerSpawner)** | 클라이언트 접속 시 플랫폼(Desktop/VR)에 맞는 아바타 프리팹을 골라 네트워크 스폰하는 컴포넌트. |
| **IToggleableContent** | 콘텐츠 모듈이 구현하는 계약. `SetEnabled(bool)` 하나로 켜고 끌 수 있다. |
| **RoomContentRegistry** | Core가 들고 있는 레지스트리. FEATURE 모듈이 자기등록하고, 런치패드·스케일 변경 등 SYSTEMS가 이를 순회한다. |
| **DefaultPrefabObjects** | 서버·클라가 공유해야 하는 NetworkObject 프리팹 목록. 불일치 시 아바타가 상대편에게 안 보인다. |
| **C1~C4** | 룸이 작동하기 위한 4개의 불변식 계약. 하나라도 어기면 입장 불가 또는 아바타 미표시 등의 증상이 난다. |

---

> **참고: 재현 환경**
> XRCollabDemo (Unity 6000.3.11f1, MCP 27826), 서버 IP 192.168.50.49 (마스터 :5000 / 룸 :7777).
> 셋업 세부 절차는 `build-xumlobby-server.md` §1 참고.
