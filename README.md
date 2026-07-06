# promptscene-harness

**PromptScene** — XRCollabDemo에서 자연어 프롬프트로 룸(구조 + 선택 기능)을 합성하고, 실제로 잘 돌아가는지 구조적으로 검증하기 위한 **문서(규격) + Claude Code 스킬** 모음.

> 이 레포는 **문서와 스킬만** 담습니다. Unity 프로젝트 본체(`XRCollabDemo`)는 추적하지 않습니다.

---

## 핵심 아이디어

룸 = **반드시 있어야 하는 SYSTEMS(Core)** + **있어도/없어도 되는 FEATURES(Content)**.
의존성은 **FEATURE → SYSTEMS 한 방향**이며, 코어는 특정 기능의 존재를 컴파일타임에 모른다 (UE5 "Modular Game Features"와 동일 원칙). 그래서 기능은 런타임 on/off + 프로젝트 간 이식이 가능하다.

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
| [promptscene-content-contract.md](docs/promptscene-content-contract.md) | 모든 스킬·런치패드·콘텐츠 모듈이 공유하는 **단일 규격**. SYSTEMS/FEATURES 분류, C# 계약 인터페이스, 씬 계층 규약, 불변식 C1~C4, 검증 하네스, 로드맵 |
| [build-working-room.md](docs/build-working-room.md) | 입장·아바타 스폰·이동이 실제로 되는 ROOM 씬을 처음부터 조립하는 검증된 절차 (플레이어 스포너 + C1~C4) |
| [build-xumlobby-server.md](docs/build-xumlobby-server.md) | MST + FishNet + XumNet 스택의 헤드리스 서버(MasterAndSpawner.exe / Room.exe) 빌드·런타임 검증 |
| [build-meta-client.md](docs/build-meta-client.md) | Meta Quest용 클라이언트 APK 빌드(Android/OpenXR/IL2CPP/ARM64) + adb 설치·실행·검증 |

---

## Claude Code 스킬 (`.claude/skills/`)

| 스킬 | 하는 일 |
|---|---|
| [`/build-working-room`](.claude/skills/build-working-room/SKILL.md) | `build-working-room.md`만 보고 ROOM 씬을 조립하고 **end-to-end 라이브 증명** (C1~C4 적용 → Room.exe 재빌드 → Master+Room 서버 기동 → 에디터 클라 입장 → §6.5 런타임 신호 검증). `assets/`에 조립·빌드·검증 스크립트 포함 |
| [`/build-client`](.claude/skills/build-client/SKILL.md) | 대상 플랫폼(Meta / XReal / Tablet / Vision)용 클라이언트 앱 빌드, Android는 adb 배포까지 |

---

## 검증 하네스

"하네스"는 독립 코드가 아니라 **규격(계약 §5) + 실행 절차(`/build-working-room` 스킬)** 로 존재한다.

- **구조/SYSTEMS 하네스** — 불변식 C1~C4를 리플렉션 read-back + 런타임 신호(§6.5: 아바타 스폰, 로비 언로드, WASD-ready)로 집행. **작동 검증됨** (BasicRoom_2로 라이브 증명).
- **FEATURES 하네스** — 규격만 존재, 자동 실행체 미구현 (Ruler는 수동 검증).
- **합성 하네스** — 프롬프트→룸 합성 결과를 위 신호로 자동 판정하는 최종 목표 (로드맵 Phase 5).

---

## 진행 현황 (로드맵)

- ✅ **Phase 0** 룸 베이스 (구조 + 아바타 + UI 전환, 불변식 C1~C4)
- ✅ **Phase 1** 계약 규격
- ✅ **Phase 2** Ruler를 계약 위에 클린 재구현 (파일럿)
- ✅ **Phase 2.5** 씬 계층 표준화 (SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC)
- ⬜ **Phase 3** 런치패드 UI (registry → 아이콘 그리드 → SetEnabled)
- ⬜ **Phase 4** `/RoomContent-*` 스킬화 + LLM 신규 기능 생성 템플릿
- ⬜ **Phase 5** `/promptscene` 합성 스킬 + 하네스

> PromptScene 런타임 코드는 XRCollabDemo 쪽 `Assets/PromptScene/`(namespace `PromptScene.Core`)에 있으며 이 레포에는 포함되지 않는다.
