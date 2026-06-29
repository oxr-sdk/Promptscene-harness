---
marp: true
theme: oxr
title: OXR-SDK는 AI-Ready 되었는가
paginate: true
---

<!-- _class: cover -->
<!-- _paginate: false -->
<div class="dot"></div><div class="line"></div>

# OXR-SDK는 AI-Ready 되었는가
<div class="sub">문서와 패키지만으로, 사람의 암묵지 없이 — AI가 검증 가능하게 작동하는 산출물을 만드는가</div>

---

## 이 발표의 주장

- 프레임워크가 *AI-ready* 가 됐다 — **OXR-SDK**가 그 사례
- 주인공은 XRCollabDemo가 아님 → XRCollabDemo는 AI-ready의 **증거**

> **AI-ready 정의**
> SDK의 문서·패키지만으로, 사람의 암묵지 없이,
> AI 에이전트가 검증 가능하게 작동하는 산출물을 만들 수 있는 상태

**오늘 증명**: 문서만 보고 작동 룸을 자율 빌드·검증
**오늘 아님 (Phase 5)**: 문장 하나로 임의의 룸을 합성

---

## 1) 추진 배경

- 병목의 이동: "사람이 짤 수 있는가" → **"AI가 쓸 수 있는가"**
- **DX(개발자 경험) → AX(에이전트 경험)** 전환
- AI에게 legible하지 않은 프레임워크 = 마찰 → 도태

**"그냥 클론하면 되지 않나?"**
- 정적 샘플 클론 = 결과물 동결 → 작동 지식은 **그 안에 숨음**
- 한 발만 벗어나면 다시 내부 지식 필요

---

## 배경 — '스포너 클론' 발견

- "되던" 룸이 작동한 진짜 이유:
  플레이어 스포너를 원본 `Room.unity`에서 **통째로 복제**
- 스크립트로 직접 생성 → scene id 못 받음
  → 입장 시 `"Failed to confirm the access"` 거부
- 프리팹으로 인스턴스화 → **통과**

→ 숨은 규칙을 문서·계약으로 끄집어내는 것이 **AI-ready 작업**

---

## 2) 추진 내용 — ① 절차서

- 흩어진 OXR-SDK 문서 → **작동 룸 조립 절차서 한 편**으로 정리
- 기존 출처(README·gitbook)의 빈틈:
  - 스포너는 **프리팹이어야 한다**
  - 작동 불변식 **C1~C4**
- 이 문서 하나로 → 입장 · 아바타 스폰 · 이동 가능

---

## 2) 추진 내용 — ② AI 자율 빌드

**AI + MCP만으로** 새 룸 생성 → 빌드 → 검증 전 과정 자율 수행
- 씬 생성 · 네트워크 프리팹 배치 · 스포너 인스턴스화
- 서버 빌드 · 실행 · 로그 판독 · 작동 검증

| 항목 | 값 |
|---|---|
| AI 에이전트 | Claude Code (claude-sonnet-4-6) |
| Unity 연동 | MCP → Unity 에디터, 포트 27826 |
| 대상 | XRCollabDemo, Unity 6000.3.11f1 |

---

## 3) 검증 결과 — 서버 등록

"그럴듯한 룸"이 아니라 **증명 가능하게 작동하는 룸**

- `Online Scene: BasicRoom`
- `Client 0 is successfully validated`
- 마스터 서버 `192.168.50.49:5000` listening, `Spawner successfully created`
- 룸 서버 `:7777`, `Room registered successfully`
- 등록 방: `Room-1A5C-ED20-3935` / ID 0 / 최대 10명 / Public

---

## 3) 검증 결과 — 화면 전환 & 아바타

**룸 전환 (자동)**
- 로비 씬 `Client` 언로드 → BasicRoom 로드
- 로비 UI `C-MasterCanvas` 사라짐 — 별도 처리 없이 자동

**아바타 스폰**
- `NetworkObjects=5`
- `Desktop(Clone)` + `mixamorig:Hips` + 플랫폼 루트 모두 `scene=BasicRoom`
- 아바타 카메라 활성, 게임뷰에 룸 표시

---

## 3) 검증 결과 — 콘텐츠 플러그인

- 측정 기능(Ruler)을 계약 `IToggleableContent` 위에 재구현
- 룸 코어에 자기등록 → 활성화 시 측정선 + 거리 라벨 `4.00m` 렌더
- 원본(DeepChairProject) 앱 레이어 의존 **0** — 계약 위 클린 재구현

---

## 검증 기준 — 작동 불변식 C1~C4

| | 불변식 |
|---|---|
| **C1** | 프리팹 컬렉션이 서버·클라이언트에서 일치 |
| **C2** | 플레이어 스폰은 지정 스포너 **프리팹**으로 |
| **C3** | 룸 진입/이탈 시 씬이 정확히 전환 |
| **C4** | 서버 Master+Room · 에디터 Client+Room 동시 로드 |

검증 기준 자체가 구조화 → AI가 맞/틀림을 **스스로 판단**
= "검증 가능"의 실체

---

## 4) 결론

**OXR-SDK는 AI-Ready 되었다** — 정의를 충족

근거 네 가지:
1. 작동 규칙 명시 문서화 (C1~C4, 스포너=프리팹)
2. 확장 면이 표준 계약으로 정의 (`IToggleableContent`)
   — 코어가 기능 존재를 모르는 플러그인 구조 (≈ UE5 Modular Game Features)
3. 검증 기준이 구조화됨
4. 암묵지 없이 AI가 처음부터 작동 룸 재현·검증

---

## XRCollabDemo의 위상 변화

- 기존: 받아서 그대로 실행하는 **정적 다운로드 샘플**
- 이제: 프레임워크 위에서 프롬프트로 제공해도 이 결과
  → **재생성·검증 가능한 레퍼런스**로 best sample 사용

> 경계: 오늘 완료 = "문서만 보고 작동 룸 자율 빌드·검증"
> 다음 = "문장 하나로 임의의 룸 합성"

---

## 5) 향후 계획

- DeepChairProject처럼 **기능을 자연어로 설명해 생성**
- 제너레이티브 UI — 레지스트리 → 아이콘 그리드 → 토글 **런치패드**

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 룸 베이스 (네트워크+아바타+UI 전환, C1~C4) | 완료 |
| 1 | 계약 규격 (`IToggleableContent`, 씬 계층 표준) | 완료 |
| 2 | Ruler 파일럿 (계약 위 클린 재구현·검증) | 완료 |
| 2.5 | 씬 계층 표준화 (SYSTEMS/ENVIRONMENT/UI/FEATURES/_DYNAMIC) | 완료 |
| 3 | 런치패드 UI — 레지스트리 → 아이콘 그리드 → 토글 | 예정 |
| 4 | 스킬화 — `/RoomContent-<feature>` + LLM 생성 템플릿 | 예정 |
| 5 | 합성+하네스 — `/promptscene` + C1~C4 자동 검증 | 예정 |

---

## 마무리

Phase 3 → 5가 닫히면
**자연어 한 줄 → 작동하는 협업 룸 + 자동 검증 리포트**

오늘은 그 **기반이 증명됐다**는 보고입니다.

