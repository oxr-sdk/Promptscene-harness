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

> **버전 고정(IvanMurzak/Unity-MCP 사용 시 권장):** **`0.66.0`**으로 고정하세요 — studio에서 실제로 설치·5층 검증된 값입니다. studio의 UnifiedXRMotion 1.8.5 MCP 어댑터가 `…Runtime.Data.GameObjectRef`를 참조하는데, 이 심볼은 **0.66.0–0.67.3** 구간에만 존재합니다(`0.68.0`부터 네임스페이스가 `AIGD`로 리네임).
>
> **노트(신버전):** UnifiedXRMotion 1.8.5의 `versionDefine` 참조가 이후 담당자에 의해 **완화**되어, `latest`(현재 `0.86.1`)도 동작할 *가능성*이 생겼습니다 — 단 **studio 실측 미완**입니다. `0.86.1` 승격은 별도 작업(MCP 재설치 + 컴파일 검증, NuGet DLL 동기화 포함)이라, 이 README는 **실측값 = `0.66.0`**을 기재합니다. 검증 후 승격 예정 — 바꾸기 전 `../xumflow-migration.md` §7의 현재 상태를 확인하세요.

대체 가능한 다른 살아있는 Unity MCP 서버(여기서 검증하진 않음):

- [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) — 에셋·씬·스크립트 관리 브리지, 활발히 유지보수(구 `justinpbarnett/unity-mcp`가 이관됨).

---

## 빠른 시작

### 1단계: 플러그인 설치

정식 설치(마켓플레이스 경유):

```
/plugin marketplace add oxr-sdk/Promptscene-harness
/plugin install promptscene@promptscene-harness
```

개발/임시 로드(이 세션 한정):

```
claude --plugin-dir .
```

스킬 편집 후에는 `/reload-plugins`로 변경을 반영합니다.

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
