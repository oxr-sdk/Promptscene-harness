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
<div class="sub">정적 샘플에서 재생성 가능한 프레임워크로</div>

---

## 1) 주장

**Is OXR-SDK AI-ready?**

> OXR-SDK는 AI-ready한 프레임워크로 전환될 수 있는 구조를 갖고 있었고,  
> 이번 작업을 통해 그 구조가 문서 · 프롬프트 · 검증 기준으로 실제 작동함을 확인했다.

- 검증은 **XRCollabDemo 위에서 수행**
- 이유: 로비 · 룸 생성 · 네트워크 스폰 · 아바타 · 씬 전환이 모두 포함된 대표 샘플
- 질문: OXR-SDK 위에서 XRCollabDemo 수준의 협업 룸을  
  **정적 샘플 클론 없이 재생성할 수 있는가?**

---

## 2) AI-Ready 정의

AI-ready는 "AI가 알아서 다 만든다"가 아님

> SDK의 기능과 사용 절차가 문서, 패키지, 프롬프트, 검증 기준으로 정리되어  
> AI 에이전트가 사람의 암묵지 없이 작동 산출물을 만들고 검증할 수 있는 상태

AI-ready의 조건:

- AI가 읽고 실행할 수 있는 절차
- 반드시 지켜야 하는 작동 조건
- 결과를 판정할 수 있는 검증 기준
- 실행 샘플이 아니라 **재생성 가능한 레퍼런스**

---

## 3) AI-Ready 전과 후

| 구분 | 기존 샘플 방식 | AI-ready 방식 |
|---|---|---|
| 제공 형태 | XRCollabDemo 다운로드 | OXR-SDK + 문서 + 프롬프트 + 검증 기준 |
| 지식 위치 | 씬/프리팹 안의 암묵지 | 절차 · 불변식 · 씬 토폴로지 |
| AI 역할 | 기존 프로젝트를 열어봄 | 새 룸을 조립 · 빌드 · 검증 |
| 실패 대응 | 사람이 디버깅 | 조건 위반 여부로 판정 |
| 의미 | "샘플이 작동한다" | "프레임워크가 AI에게 작동한다" |

**차이:** 정적 결과물 → 재생성 가능한 프레임워크

---

## 4) OXR-SDK 구성요소

OXR-SDK는 여러 레이어가 맞물리는 프레임워크

| 구성요소 | 역할 |
|---|---|
| `com.oxr.sdk` | 인증, 에셋 스토어, 파일 스토리지, 룸 관리 |
| `XumLobby` | 로비 UI, 룸 생성, 룸 진입, 룸 씬 전환 |
| `XumNet` | 네트워크 오브젝트 생성과 스폰 |
| `UnifiedXRMotion` | 플랫폼별 아바타와 모션 |

검증 관점:

> 데모 하나가 실행되는가가 아니라,  
> 이 레이어들이 AI가 읽고 조립할 수 있는 형태인가

---

## 5) 검증 방법

세 단계로 검증

1. OXR-SDK 문서와 패키지 확인
2. 작동 조건을 절차와 불변식으로 정리
3. AI + MCP만으로 새 룸 생성 · 빌드 · 검증

| 항목 | 값 |
|---|---|
| AI 에이전트 | Claude Code (claude-sonnet-4-6) |
| Unity 연동 | MCP → Unity Editor, 포트 27826 |
| 대상 | XRCollabDemo |
| Unity | 6000.3.11f1 |

---

## 6) 작동 불변식 C1~C4

AI가 룸을 만들고 검증하기 위해 필요한 조건

| 조건 | 내용 |
|---|---|
| **C1** | 서버와 클라이언트의 네트워크 프리팹 컬렉션 일치 |
| **C2** | 플레이어 스폰은 지정 스포너 프리팹과 플랫폼별 아바타 카탈로그 사용 |
| **C3** | 룸 진입/이탈 시 로비 씬과 룸 씬이 정확히 전환 |
| **C4** | 서버는 Master+Room, 에디터는 Client+Room 흐름 기준으로 검증 |

**의미:** 암묵지를 AI가 판정 가능한 조건으로 외부화

---

## 7) 검증 결과 — 서버와 입장

검증 결과는 서버 로그와 Unity 런타임 상태에서 확인

- `Online Scene: BasicRoom`
- `Client 0 is successfully validated`
- 마스터 서버 `192.168.50.49:5000` listening
- 룸 서버 `:7777`에서 방 등록
- 등록 방: `Room-1A5C-ED20-3935`
- ID 0 / 최대 10명 / Public

**의미:** 룸 생성, 서버 등록, 클라이언트 입장 흐름이 실제 동작

---

## 8) 검증 결과 — 씬 전환과 아바타

**씬 전환**

- `Client` 씬 언로드
- `BasicRoom` 로드
- 로비 UI `C-MasterCanvas` 제거

**아바타 스폰**

- `NetworkObjects=5`
- `Desktop(Clone)`이 `scene=BasicRoom`에 위치
- `mixamorig:Hips`와 플랫폼 루트도 `scene=BasicRoom`
- 아바타 카메라 활성, 게임뷰에 룸 표시

---

## 9) 증명 매핑

| AI-ready 판단 기준 | 실제 증거 | 의미 |
|---|---|---|
| AI가 룸을 만들 수 있는가 | MCP로 BasicRoom 생성/빌드 | 절차가 실행 가능 |
| 서버에 등록되는가 | `Online Scene`, `Room registered` | XumLobby 흐름 동작 |
| 입장이 검증되는가 | `Client 0 validated` | 인증/입장 연결 |
| 씬 전환되는가 | Client 언로드, BasicRoom 로드 | 로비→룸 전환 성공 |
| 아바타가 스폰되는가 | `Desktop(Clone)` in BasicRoom | XumNet/Motion 연결 |
| AI가 판정 가능한가 | C1~C4로 확인 | 검증 가능한 절차 |

---

## 10) 결론

**OXR-SDK는 AI-ready한 방식으로 제공될 수 있다**

근거:

- OXR-SDK 계열 프레임워크는 조립 가능한 레이어를 갖고 있음
- 작동 조건을 문서 · 프롬프트 · 검증 기준으로 외부화함
- AI가 MCP로 룸 생성 · 빌드 · 서버 등록 · 아바타 스폰까지 검증함

> XRCollabDemo는 OXR-SDK 검증의 대표 샘플이며,  
> AI-ready 가능성을 보여주는 best sample이다.

---

## 11) 경계와 해석

이번 발표가 말하는 것:

- 룸 생성 · 입장 · 아바타 스폰 범위에서 AI-ready 가능성 검증
- 기존 프레임워크를 AX 관점에서 절차화
- XRCollabDemo를 재생성 가능한 레퍼런스로 전환

이번 발표가 말하지 않는 것:

- 기존 문서가 처음부터 완벽했다
- 문장 하나로 임의의 룸을 완전 합성한다
- 콘텐츠 기능 생성까지 이미 끝났다

---

## 12) 향후 계획

다음 단계: 자연어로 기능을 붙이는 OXR-SDK 워크플로우

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 룸 베이스 검증: 네트워크, 아바타, UI 전환 | 완료 |
| 1 | 룸 조립 계약: C1~C4, 스포너 규칙, 씬 토폴로지 | 완료 |
| 2 | AI + MCP 기반 룸 생성 · 빌드 · 검증 | 완료 |
| 3 | 콘텐츠 확장 파일럿: `IToggleableContent`, Ruler | 완료 |
| 4 | 런치패드 UI: 레지스트리 → 아이콘 그리드 → 토글 | 예정 |
| 5 | `/RoomContent-<feature>` 스킬화 | 예정 |
| 6 | `/promptscene` + 자동 검증 하네스 | 예정 |

---

## 마무리

**AI-ready 전**

> "XRCollabDemo 샘플은 작동한다"

**AI-ready 후**

> "OXR-SDK 프레임워크 위에서  
> AI가 협업 룸을 재생성하고 검증할 수 있다"

XRCollabDemo는 이제 정적 다운로드 샘플이 아니라  
**OXR-SDK의 재생성 가능한 best sample**이다.
