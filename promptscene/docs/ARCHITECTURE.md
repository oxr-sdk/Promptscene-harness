> 이 문서는 promptscene-harness의 설계 개념과 구조를 설명한다. 설치·사용법은 README.md 참고.

# PromptScene — 설계와 구조

## 설계 개념 (Layered Architecture)

씬을 **작동 토대(SYSTEMS / runtime platform)** 와 그 위에 **조합해 쌓는
요소(FEATURES / content modules)** 로 분리해 설계한다. — *composition over
inheritance, separation of concerns.*

- **SYSTEMS — 작동 토대 (runtime platform).**
  룸이 *작동하는 방식* 그 자체. 네트워크 연결, 세션, 플레이어 입장·스폰,
  코어 레지스트리. 씬의 겉모습이 무엇이든 이 메커니즘은 동일하게 성립해야
  한다. 불변식 C1~C4로 집행한다.

- **FEATURES — 조합 요소 (content modules).**
  작동 토대 위에서 노는 선택 요소. 기능 모듈은 물론, 환경 연출(재질·조명·
  레이아웃 변형)도 이 층에 속한다. 자연어 의도가 바뀌면 이 층만 바뀐다.

**핵심:** 자연어 입력이 바뀌어도 작동 토대는 바뀌지 않는다. 그래서 토대는
검증된 절차로 얼려두고(스킬화), 변하는 층만 조립하면 자연어→씬 자동화가
성립한다.

**성립 근거 (inversion of dependency).** 토대는 그 위에 무엇이 쌓이는지
알지 못한다 — 코어가 기능을 호출하는 게 아니라, 각 기능이 런타임에 코어의
레지스트리에 스스로 등록한다. 의존성은 FEATURE → SYSTEMS 한 방향뿐이다.
그래서 기능은 런타임 on/off와 프로젝트 간 이식이 가능하다.
(UE5 "Modular Game Features"와 동일 원칙.)

---

## 문서 트리 (`docs/`)

```
promptscene-content-contract.md   ← 단일 규격 (SSOT): 계약 인터페이스 · 씬 계층 · 불변식 C1~C4 · 검증 하네스
└── build-working-room.md         ← "이 문서 하나로" 작동하는 ROOM 씬 조립 (검증된 절차)
    ├── build-xumlobby-server.md  ← 서버(.exe) 빌드 & 런타임 검증 (Master + Room)
    └── build-meta-client.md      ← 실기기(Meta Quest) 클라이언트 APK 빌드 · adb 배포
```

| 문서 | 내용 |
|---|---|
| [promptscene-content-contract.md](promptscene-content-contract.md) | 모든 스킬·런치패드·콘텐츠 모듈이 공유하는 **단일 규격**. SYSTEMS/FEATURES 분류, C# 계약 인터페이스, 씬 계층 규약, 불변식 C1~C4, 검증 하네스, 로드맵 |
| [build-working-room.md](build-working-room.md) | 입장·아바타 스폰·이동이 실제로 되는 ROOM 씬을 처음부터 조립하는 검증된 절차 (플레이어 스포너 + C1~C4) |
| [build-xumlobby-server.md](build-xumlobby-server.md) | MST + FishNet + XumNet 스택의 헤드리스 서버(MasterAndSpawner.exe / Room.exe) 빌드·런타임 검증 |
| [build-meta-client.md](build-meta-client.md) | Meta Quest용 클라이언트 APK 빌드(Android/OpenXR/IL2CPP/ARM64) + adb 설치·실행·검증 |

---

## 검증 하네스

"하네스"는 독립 코드가 아니라 **규격(계약 §5) + 실행 절차(`/promptscene:assemble-room` 스킬)** 로 존재한다.

- **구조/SYSTEMS 하네스** — 불변식 C1~C4를 리플렉션 read-back + 런타임 신호(§6.5: 아바타 스폰, 로비 언로드, WASD-ready)로 집행. **작동 검증됨** (BasicRoom_2로 라이브 증명).
- **FEATURES 하네스** — `/promptscene:scaffold-content` 스킬로 실행체 존재: 프롬프트→FEATURE 생성 후 RoomCore 룸에 얹어 §5(자기등록·SetEnabled 무예외·메타)를 라이브 룸에서 집행. **작동 검증됨** (Phase 4, ClickSpawnerContent로 라이브 증명).
- **합성 하네스** — `/promptscene:compose-room` 스킬로 실행체 존재: 자연어 요청→기능 선택(카탈로그 매칭)→`composition-plan.json` 박제→여러 부품(assemble-room·scaffold-content) 오케스트레이션→§6.5 SYSTEMS + §5 FEATURES(기능마다)를 라이브 룸에서 자동 판정. **작동 검증됨** (Phase 5, 2026-07-13, `ComposedRoom_1`로 라이브 증명).

---

## 진행 현황 (로드맵)

- ✅ **Phase 0** 룸 베이스 (구조 + 아바타 + UI 전환, 불변식 C1~C4)
- ✅ **Phase 1** 계약 규격
- ✅ **Phase 2** Ruler를 계약 위에 클린 재구현 (파일럿)
- ✅ **Phase 2.5** 씬 계층 표준화 (SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC)
- ⬜ **Phase 3** 런치패드 UI (registry → 아이콘 그리드 → SetEnabled) — 1차 시도는 **보류/롤백**됨 (회고: [promptscene-launchpad-attempt.md](promptscene-launchpad-attempt.md))
- ✅ **Phase 4** `/scaffold-content` 스킬화 + LLM 신규 기능 생성 템플릿 (`skills/scaffold-content/`: Ruler 패턴을 동결한 `FeatureContent.cs.template` + `build_feature_room.cs` + `verify_feature.cs`, 네트워크 live-prove는 `assemble-room` 자산 재사용). **라이브 검증됨** (2026-07-09): 샘플 FEATURE `ClickSpawnerContent`("클릭한 지점에 색 구 스폰")를 프롬프트→생성→`FeatureLab_1` 룸 조립→Room.exe 재빌드→서버+에디터 클라 조인까지 돌려, §6.5 SYSTEMS(아바타 스폰·로비 소멸·WASD)와 §5 FEATURES(자기등록·SetEnabled 무예외·메타 유효) **양쪽 PASS**. 동작(클릭→스폰, _DYNAMIC 배치, 끄면 정리)도 시뮬 클릭으로 확인.
- ✅ **Phase 5** `/compose-room` 합성 스킬 + 합성 하네스 (`skills/compose-room/`: `build_composed_room.cs` + `verify_composition.cs`, 씬/네트워크/조인 절차는 `assemble-room` 자산 재사용, RoomCore+FEATURES 배치는 `scaffold-content` 참조). compose-room이 신규 담당하는 건 **자연어→기능 선택**과 **부품 오케스트레이션+최종 판정**뿐 — 조립 절차는 하위 스킬을 **참조 호출**(복제 금지, SSOT). **라이브 검증됨** (2026-07-13): "측정 도구 있는 룸 만들어줘"→Ruler 선택→`composition-plan.json`→`ComposedRoom_1` 조립→Room.exe 재빌드→조인까지 돌려 §6.5 4신호 + §5 COMPOSITION(ruler) **모두 PASS**. 이 과정에서 **스크립트 단발 빌드의 FishNet SceneId 미할당 함정**(contract §1)을 발견·수정(compose-room·scaffold-content 양쪽 빌더에 `CreateSceneId` 명시 호출 추가).

> PromptScene 런타임 코드는 XRCollabDemo 쪽 `Assets/PromptScene/`(namespace `PromptScene.Core`)에 있으며 이 레포에는 포함되지 않는다.
