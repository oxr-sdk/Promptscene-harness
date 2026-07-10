---
name: oxr-docs-routing
description: >
  OXR Platform / PromptScene / XRCollabDemo 관련 모든 작업에 적용되는 문서·소스 참조 규약.
  다음 키워드가 등장하는 작업이면 반드시 이 스킬을 따를 것:
  PromptScene, XRCollabDemo, XumNet, XumLobby, XumBuildkit, UnifiedXRMotion, XumView, XumNetwork,
  FishNet, MST, 룸(Room) 씬 조립, 아바타 스폰, 로비, RPC, 네트워크 스폰, 소유권(ownership),
  Quest/Meta 클라 빌드, Master/Room 서버, IRoomCore, IRoomContent, 계약(contract), 하네스,
  SYSTEMS/FEATURES, 런치패드, Ruler. Unity 멀티유저 XR 작업에서 플랫폼 API를 사용하거나,
  빌드·스폰·씬 전환 문제로 막혔을 때, 추측으로 우회하지 말고 이 라우팅으로 읽고 검증한다.
---

# OXR Platform 문서·소스 참조 프로토콜

> 이 플러그인 루트 경로는 셸에서 `echo "$CLAUDE_PLUGIN_ROOT"` 로 확인한다.
> 아래에서 `<PLUGIN>/docs/…` 는 `$CLAUDE_PLUGIN_ROOT/docs/…` 를 뜻한다.
> (docs가 플러그인 레포에 없다면 `promptscene-harness/promptscene/docs/` 체크아웃 경로로 대체.)

## 0. 대원칙

1. **문서는 지도, 소스는 진실.** 플랫폼 API(XumNet 등)를 코드에 쓰기 전에 **반드시 PackageCache의 실제 소스에서 시그니처를 확인**한다. 공식 GitBook 예시 코드에 문법 오류·오타가 확인된 바 있으므로(예: Object Management 페이지의 `XumView.RPC` 예시), 문서 코드를 그대로 복붙하는 것을 금지한다.
2. **원본만 읽는다 (SSOT).** 문서 내용을 다른 파일로 요약·복사해 두지 않는다. 요약본은 낡는다.
3. **PackageCache는 읽기 전용.** `Library/PackageCache/` 하위와 `Packages/manifest.json`은 절대 수정하지 않는다 (PreToolUse 훅이 기계적으로 차단하지만, 우회 시도 자체를 하지 말 것). 로컬/임베디드 패키지(`Packages/` 하위)도 수정 금지.
4. **GitHub 원격 접근 시도 금지.** `oxr-sdk` 조직 레포들은 private이다. clone/fetch를 시도하지 말 것 — 필요한 내용은 전부 로컬 PackageCache에 이미 있다.

## 1. 참조 소스 4계층 (우선순위 순)

### 1층 — harness 검증 문서 (함정·불변식·검증된 절차) — `<PLUGIN>/docs/`

| 문서 | 언제 읽나 |
|---|---|
| `promptscene-content-contract.md` | 계약 인터페이스 불일치, SYSTEMS/FEATURES 분류 판단, 씬 계층 규약, 불변식 C1~C4 |
| `build-working-room.md` | 아바타 스폰 안 됨, 입장/씬 전환 이상, 룸 조립 절차 전반 |
| `build-xumlobby-server.md` | 서버(Master/Room exe) 빌드·런타임 문제 |
| `build-meta-client.md` | Quest 클라 빌드, **화면 깜빡임/시점 고정("2 audio listeners") → §2.4-D** |
| `promptscene-launchpad-attempt.md` | 런치패드/ContentMeta 관련 작업 전 회고 확인 |

여기 있는 함정 지식(예: FishNet 씬 네트워크 오브젝트 재배치 → Room.exe 재빌드)은 아래 어느 소스에도 없다. **플랫폼 문서보다 이 층을 먼저 본다.**

### 2층 — PackageCache 패키지 문서 (로컬, 네트워크 불필요)

위치 탐색 (경로에 해시 접미사가 붙으므로 항상 glob/grep으로 찾는다):
```bash
ls -d Library/PackageCache/*/ | grep -iE "xum|unified|xr"
```
`Documentation~`는 Unity 임포트에서 숨겨질 뿐 디스크에는 존재한다.

| 패키지 | 문서 위치 | 담당 영역 |
|---|---|---|
| XumNet | `Documentation~/ai/` | FishNet 기반 연결, RPC(XumView/XumRPC/RpcTarget), 네트워크 스폰(XumNetwork), 직렬화 |
| XumLobby | `Documentation~/ai/` | 로비, 로그인 창, Client/Room 씬 프리팹(C-*/R-*) |
| UnifiedXRMotion | `Documentation` | 아바타 모션 동기화, 트래킹 |
| XumBuildkit | `Documentation` | 멀티플랫폼 빌드 파이프라인 |

### 3층 — PackageCache 소스 코드 (최종 레퍼런스)

API 시그니처·제약·에러 동작의 진실. 사용할 API마다:
```bash
grep -rn "public .* Instantiate\|public .* RPC" Library/PackageCache/*xumnet*/Runtime/
```
확인 대상 예: `XumNetwork.Instantiate` 파라미터, `XumView.RPC`의 실제 시그니처와 `RpcTarget` enum 멤버, `ObjectSerializer`가 지원하는 직렬화 타입.

### 4층 — GitBook 공식 가이드 (온라인, 사용 패턴용)

- 인덱스: `https://oxr-platform.gitbook.io/oxr-platform-docs/llms.txt`
- 모든 페이지는 URL 끝에 `.md`를 붙이면 마크다운 원문으로 받을 수 있다.
- 주요 페이지: Scene Assembly(씬 조립 프리팹 목록), Object Management(스폰·소유권·RPC 사용 패턴), Device/Avatar Setup, Build, Troubleshooting.
- 용도: "이 기능이 존재하는가, 어떤 패턴으로 쓰는가"까지만. 시그니처는 3층에서 재검증(대원칙 1).

## 2. 증상 → 소스 라우팅

| 증상/작업 | 경로 |
|---|---|
| 계약 인터페이스 컴파일 에러 | 1층 contract §2 |
| 아바타 안 뜸 / 로비 안 사라짐 / WASD 불가 | 1층 build-working-room + C1~C4 점검 |
| 네트워크 스폰·RPC·소유권 구현 | 4층 Object Management로 패턴 파악 → 3층 XumNet 소스로 시그니처 검증 → 2층 `Documentation~/ai` 보충 |
| 아바타 모션 이상 | 2층 UnifiedXRMotion → 3층 소스 |
| 로그인/로비 UI | 2층 XumLobby → 1층 build-working-room |
| 서버 빌드/실행 | 1층 build-xumlobby-server → 2층 XumBuildkit |
| Quest 클라 화면 깜빡임 | 1층 build-meta-client §2.4-D (즉답 있음) |
| 새 씬 조립 | 1층 build-working-room 우선, 4층 Scene Assembly는 교차 확인용 |

## 3. 에스컬레이션과 역기입

1. **막힘 판정**: 하네스/검증 항목 실패, 컴파일 에러 2회 이상 반복, 또는 API 동작이 예상과 다를 때 = "막힘". 추측 수정을 반복하지 말고 위 라우팅으로 읽는다.
2. **읽어도 안 풀리면**: 시도한 소스와 결과를 정리해 사용자에게 보고하고 지시를 기다린다. **SYSTEMS·패키지를 수정하는 우회는 금지.**
3. **역기입 루프**: 문서를 읽고 새로운 함정을 해결했다면, 그 해법을 `<PLUGIN>/docs/`의 해당 문서에 추가하는 **패치를 제안**한다(임의 커밋 말고 diff 제시). 검증된 절차는 이렇게만 두꺼워진다.
