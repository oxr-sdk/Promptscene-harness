# promptscene-harness

**XumFlow:studio**에서 **자연어 프롬프트로 룸을 합성하고 검증하는** Claude Code 플러그인입니다. 검증된 문서(규격)와 스킬 묶음이며, Unity 프로젝트 자체는 이 레포에 **포함되지 않습니다**(직접 준비).

> 영문판: [../../../README.md](../../../README.md)

---

## 요구사항

- **Claude Code** — CLI/IDE 에이전트(claude.ai 아님), 플러그인 지원.
- **XumFlow:studio** — 로컬 Unity 콘텐츠 저작 프로젝트, **Unity 6**(`6000.3.11f1`에서 검증). 스킬이 이걸 타깃으로 구동합니다.
- **Unity MCP 서버** — 실행 중인 Unity 에디터에 연결. 스킬이 이걸 통해 에디터를 조작하므로(씬 빌드·플레이 모드·리플렉션 점검), 스킬 실행 전 반드시 살아있어야 합니다. 아무 Unity MCP 서버나 가능하며, 아래는 우리가 실제로 쓰는 것입니다.

### Unity MCP 서버

우리가 사용·라이브 검증한 것은 **[IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)** ⭐ (`com.ivanmurzak.unity.mcp`)입니다.

> **버전 고정(IvanMurzak/Unity-MCP 사용 시 권장):** **`0.86.1`**(현재 `latest`)로 고정하세요 — **UnifiedXRMotion 1.9.0** 조합에서 studio 라이브 검증 완료된 값입니다. 검증 범위 = MCP 재설치 + 컴파일 검증(NuGet DLL 동기화 포함) + 5층 QuickTest, 전부 통과.

대체 가능한 다른 살아있는 Unity MCP 서버(여기서 검증하진 않음):

- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) — 에셋·씬·스크립트 관리 브리지, 활발히 유지보수(구 `justinpbarnett/unity-mcp`가 이관됨).

---

## 빠른 시작

### 1단계: 플러그인 설치

로드 방법은 두 가지입니다. 스킬을 **쓰기만** 할 거면 **A**, 스킬을 **고칠** 거면 **B**를 고르세요.

#### A. 정식 설치 — 마켓플레이스 경유(영구)

Claude Code 안에서 슬래시 명령 두 개를 실행합니다:

```
/plugin marketplace add oxr-sdk/Promptscene-harness
/plugin install promptscene@promptscene-harness
```

| 명령 | 실제로 하는 일 |
|---|---|
| `/plugin marketplace add oxr-sdk/Promptscene-harness` | **플러그인 소스(카탈로그)를 등록**합니다. `oxr-sdk/Promptscene-harness`는 GitHub `owner/repo` 축약형 — Claude Code가 그 레포를 받아 [`.claude-plugin/marketplace.json`](../../../.claude-plugin/marketplace.json)을 읽습니다. 이 파일이 "이 레포가 어떤 플러그인을 배포하는지"의 목록(여기선 `promptscene` 하나)입니다. 이 단계는 **아무것도 설치하지 않습니다** — 목록만 알려줄 뿐. |
| `/plugin install promptscene@promptscene-harness` | 플러그인 본체를 설치합니다. 문법은 `<플러그인>@<마켓플레이스>`: `promptscene`은 카탈로그에 선언된 플러그인 이름, `promptscene-harness`는 방금 추가한 마켓플레이스 이름입니다(레포 이름 표기가 아니라 `marketplace.json`의 `name` 필드). 설치되면 스킬을 `/promptscene:<skill>`로 호출할 수 있습니다. |

설치된 플러그인은 세션이 바뀌어도 유지됩니다. `/plugin`만 치면 대화형 관리자(탐색/활성화/비활성화/제거)가 열립니다.

#### B. 개발·임시 로드 — 로컬 체크아웃 직접 사용(이 세션 한정)

레포 루트에서:

```
claude --plugin-dir .
```

**로컬 디렉터리**를 플러그인 소스로 지정합니다 — 마켓플레이스도, 설치 단계도, git 왕복도 없습니다. 해당 세션에서만 유효하고 종료하면 사라집니다. 디스크의 수정본이 그대로 로드되므로, 스킬을 작성·수정 중일 때 쓰세요.

스킬 편집 후에는 `/reload-plugins`로 재시작 없이 변경을 반영합니다.

> **뭘 써야 하나?** A = 배포된 스킬을 쓸 때. B = 스킬을 개발할 때. 둘을 동시에 켜지 마세요 — 같은 플러그인이 두 벌 로드됩니다.

### 2단계: 스킬 실행

스킬은 플러그인 네임스페이스로 호출합니다: `/promptscene:<skill>`.

| 스킬 | 하는 일 |
|---|---|
| `/promptscene:assemble-room <RoomName>` | 빈 룸 **골격**을 조립하고 **라이브 증명** — 샘플룸 복제·등록 후 5개 층(SYSTEMS + ENVIRONMENT + UI + 빈 FEATURES + 빈 COMPOSITIONS)을 세우고, **§6.5** 런타임 신호(아바타 스폰, RoomCore 4서비스+빈 레지스트리, WASD 가능)를 QuickTest로 검증. 골격만 — 기능 콘텐츠는 안 넣음. |
| `/promptscene:add-component <요청> [on <Room>]` | **컴포넌트(FEATURE 또는 COMPOSITION)를 룸에 얹고** 완결 사이클 수행 — 상담(FEATURE/COMPOSITION 분류, 구현 가능성 판단) → 컴포넌트 확보(재사용 / Ruler 템플릿 AI 생성 / 사람 작성 스크립트 배선) → 알맞은 층에 프리팹 배선과 함께 배치 → **§5**(자기등록 + `SetEnabled` 예외 없음 + 메타 유효) **및 §6.5**(아바타 여전히 스폰 = SYSTEMS 무손상)를 QuickTest로 증명. |

순서대로: **먼저 룸 골격을 조립**하고, 그 위에 **컴포넌트를 얹습니다**.

---

## 더 알아보기

- **설계·아키텍처:** [../ARCHITECTURE.md](../ARCHITECTURE.md)
- **규격(SSOT):** [../promptscene-content-contract.md](../promptscene-content-contract.md)
