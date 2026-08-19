---
name: multiplayer-check
description: >
  Prove a PromptScene **studio** room actually works with **two clients** — editor A (host) + a ParrelSync clone
  editor B (client) on the same machine, FishNet-direct `localhost:7770` (no MST). Runs GATE 1 (빈 룸 2인 스폰 —
  4 signals: A owns its avatar / B sees the remote avatar with IsOwner=False / A's movement propagates to B /
  objId sets cross-match) and, optionally, GATE 2 (컴포넌트 파리티 — the placed FEATURE/COMPOSITION behaves across
  both clients: e.g. Chat 양방향 4신호, Grab 핸드오버 5신호, 과녁→점수 서버권위 동기). A is judged over MCP; B has
  no MCP, so B is judged by a one-line status file the clone harness overwrites every second
  (`.promptscene-status`) plus its `-logFile` event trail. Sets up the clone once (ParrelSync, manifest untouched)
  and reuses it forever after. Use when the user wants 2-client / multiplayer / 파리티 / 동기화 verification, e.g.
  "2인으로 확인해줘", "멀티 검증", "/multiplayer-check Chat on AssembleRoom". Called as an OPTION by /add-component
  (Phase 6). Argument = optionally "<ComponentTypeOrId> on <Room>"; with no argument it runs GATE 1 only.
---

# Prove a studio room with TWO clients (A host + ParrelSync clone B)

Everything the single-editor QuickTest (`/add-component` §5) proves is **structure on one client**. This skill adds
the axis that one editor structurally cannot reach: **does it hold when a second client is actually there.**

**This skill wraps a procedure — it does NOT restate it.** SSOT =
`${CLAUDE_PLUGIN_ROOT}/docs/build-studio-room.md` **§6.6** (1회 준비 / 매 세션 절차 / 함정) and
`${CLAUDE_PLUGIN_ROOT}/docs/xumflow-migration.md` **§17** (the live session this was frozen from: gate-1 PASS,
Chat 양방향 PASS, 트랩표 9종). Read those when a *why* is unclear.

**Argument:** optionally `<ComponentTypeOrId> on <Room>`.
`/multiplayer-check` → GATE 1 only on the default room ·
`/multiplayer-check Chat on AssembleRoom` → GATE 1 + Chat 양방향 파리티.

## The model (why the procedure looks like this)

QuickTest is **not MST** — it is FishNet direct `localhost:7770`. So 2 clients = **host editor A + one more
process B**. B is a **ParrelSync clone**, and the clone's shape dictates everything else:

| 클론 경로 | 처리 | 결과 |
|---|---|---|
| `Assets` `ProjectSettings` `LocalPackages` | **심링크** | A·B가 씬·설정·소스를 **공유** → "B만 다른 씬"은 불가 |
| `Library` `Packages` | **복사** | 콜드 재임포트 회피(≈3.5 GB, 1회성) |
| `UserSettings` | 클론이 새로 만듦 | MCP 포트가 갈린다(경로 SHA256: 원본 21017 / 클론 25821) |

`startAsServer` 의 값은 **코드가 아니라 씬 에셋**(`QuickStart.unity`)에 살고 그 씬은 심링크로 공유된다 →
**역할은 런타임에 뒤집는 수밖에 없다.** 그게 `QuickTestRoleBridge` 다.

## What this proves — and what it does NOT (honesty contract)

- ✅ **증명 가능:** 같은 머신 · 에디터 2개 · 데스크톱 · A=MCP 판정 / B=상태파일·로그 판정.
  2인 스폰·소유권·위치 전파·objId 교차 일치 · 컴포넌트 파리티(RPC 왕복, 소유권 이전, 서버권위 집계) · 재조인 ·
  백필 유무 · 게스트 디스폰 정리.
- ⛔ **증명 못 함:** **실 키보드/마우스 2인 조작**(에디터 2개 중 활성창만 입력을 받는다 — 이 절차는 A를 MCP,
  B를 파일 명령으로 몰아 그 함정을 *우회*한다) · **3인+ 동시 조인** · 빌드된 클라 · 실기기(V2) · XR 실조작 ·
  미감 · 경합 뺏기(D4-2) · 배포(Smart-Deploy).
- 리포트에 이 경계를 반드시 다시 적는다. 침묵을 PASS로 승격하지 않는다.

## Ground rules

- 세션은 `c:\J_0`(마켓플레이스 루트)에서. MCP(`ai-game-developer` @21017)가 살아 있어야 착수.
- ⛔ **`Packages/manifest.json` · PackageCache 수정 금지.** ParrelSync는 `.unitypackage` 경로로만 넣는다.
  손대야만 진행되는 상황이면 **우회하지 말고 정지·보고**(MPPM 전환은 사람 판단).
- ⛔ **shipped `QuickTestStarter` / `QuickStart.unity` 파일 수정 금지.** 역할은 런타임 브리지가 뒤집고,
  `roomSceneKey` 는 메모리에서만 바꾼다(`mp_verify.Setup`).
- ⛔ **클론 전용 파일은 반드시 프로젝트 "루트"에.** `Assets/` 안에 두면 심링크로 **원본까지 무장·조종된다**.
- **B 상태는 `.promptscene-status`(덮어쓰기, 한 줄)로 읽는다.** `-logFile` 은 append-only라 상태 조회에 쓰면
  읽는 비용이 세션 길이에 비례해 커진다 — 로그는 시계열·사후 추적용.
- 순서 고정 **①클론 → ②게이트 1 → ③게이트 2.** 게이트 1이 FAIL이면 **인프라 문제이므로 게이트 2로 넘어가지
  않는다**(오진 방지).

## Key resources (paths stable)

| | |
|---|---|
| 하네스 소스(설치본은 studio `Assets/PromptScene/Harness/Editor/`) | `${CLAUDE_PLUGIN_ROOT}/skills/multiplayer-check/assets/QuickTestRoleBridge.cs`, `CloneHarness.cs` |
| A쪽 판정 스크립트 | `${CLAUDE_PLUGIN_ROOT}/skills/multiplayer-check/assets/mp_verify.cs` (`PS_VerifyMultiplayer`) |
| 절차 SSOT / 트랩표 | build-studio-room **§6.6** / xumflow-migration **§17.5** |
| 클론 | `c:\J_0\XumFlow-studio_clone_0` (1회성 — 있으면 재사용) |
| B 로그 | 스크래치패드에 `-logFile` 로 지정 |

---

## EXECUTE

### Phase 0 — 전제 + 스냅샷
1. `pwd` = `c:\J_0`. MCP 응답 확인(`scene-list-opened`).
2. **원본 컴파일 0 / 플레이모드 아님** 확인. `Packages/manifest.json` **md5 기록**(세션 끝에 대조).
3. 클론 존재 확인: `c:\J_0\XumFlow-studio_clone_0\.clone` 있으면 **Phase 1·2를 건너뛴다**.

### Phase 1 — 클론 확보 (1회성; 이미 있으면 skip)
build-studio-room **§6.6.1** 그대로. 요약: ParrelSync `.unitypackage`를 풀어 GUID 보존한 채
`Assets/ThirdParty/ParrelSync/`로 배치(⛔ UPM git URL 금지) → 컴파일 0 + 메뉴 노출 확인 →
`ClonesManager.CreateCloneFromCurrent()` 를 **`EditorApplication.delayCall` 로 감싸** 호출(동기 복사라 MCP가
타임아웃난다) → `.clone` 파일 생성으로 완료 판정.
⛔ **Unity 6 비호환 징후(컴파일 에러 / 메뉴 없음 / 클론 생성 실패)면 즉시 정지·보고.** 대안(MPPM)은 사람 판단.

### Phase 2 — 하네스 설치 (1회성)
`assets/QuickTestRoleBridge.cs` + `assets/CloneHarness.cs` → studio `Assets/PromptScene/Harness/Editor/`.
⚠ **그 폴더여야 한다** — asmdef 없는 `Editor` 폴더 = `Assembly-CSharp-Editor` 만이 `QuickTestStarter`
(`Assembly-CSharp`)와 `ParrelSync`(Editor asmdef)를 동시에 볼 수 있고, `App.HotUpdate`(hot-update DLL =
Smart-Deploy 배포물)를 오염시키지 않는다.
검증 2건: ① 컴파일 0 + 두 타입이 `Assembly-CSharp-Editor` 에 적재 ② **원본에서 role=`''`(불활성)**.

### Phase 3 — A를 host로
`scene-open QuickStart.unity Single` → `PS_VerifyMultiplayer.Setup`(ROOM 채우고) → `isPlaying=true` → 12~15s 대기.
⚠ **Play를 끊었다 다시 들어갈 때마다 Setup을 다시 부른다**(Play 종료 시 씬이 진입 전 스냅샷으로 되돌아간다).

### Phase 4 — B 기동 + 무장 (순서가 중요)
1. `Unity.exe -projectPath <clone> -logFile <scratch>/clone_b.log` — **무장하지 말고** 먼저 띄운다.
   콜드 오픈+컴파일 ≈5분이고, 그동안 A가 host로 떠 있어야 한다(**QuickTest 클라는 재시도가 없다**).
2. 로그에서 `role-detect role='client' via ClonesManager.IsClone()=True` 확인.
3. **A가 host인 것 확인 후** 클론 **루트**에 `.promptscene-autoplay` 생성 → B가 Play 진입.
4. 로그에서 `apply role=client applied=True startAsServer:True->false` 확인 = 브리지 성립.

### Phase 5 — GATE 1 (빈 룸 2인 스폰, 4신호) — **여기서 FAIL이면 인프라 문제. §6으로 가지 말 것.**
`PS_VerifyMultiplayer.Check`(A측) + 클론 루트 `.promptscene-status`(B측)를 교차 대조:

| 신호 | A쪽 (MCP) | B쪽 (`.promptscene-status`) |
|---|---|---|
| ① A 자기 아바타 `IsOwner=True` | `M1 … PASS` | — |
| ② B에 원격 아바타 `IsOwner=False` | `M2(A측)` 게스트 아바타 존재 | `id=<n> Desktop(Clone) owner=False ownerCid=0` **+ 자기 것 `owner=True`** |
| ③ 위치 전파 | `PS_VerifyMultiplayer.Nudge` → `(5,0,3)` | 같은 objId 의 `pos=5.00,0.00,3.00` |
| ④ objId 교차 일치 | `M4(A측) objIds=…` | `nobs`/`id=` 집합이 **동일**, 소유만 반전 |

⚠ 이름은 달라도 된다 — 같은 objId 라도 네임태그 텍스트가 A/B에서 다르게 보인다(실측). **objId로 대조한다.**

### Phase 6 (옵션) — GATE 2 컴포넌트 파리티
인자로 컴포넌트를 받았을 때만. `PARITY_TYPE` 을 채워 A측 스폰을 확인하고, 컴포넌트 종류에 맞는 안무를 돌린다.
**공통 판정 골격 = 공유 에폭 + 역할 안무 + 양측 교차대조.** B 조종은 클론 루트 `.promptscene-cmd`
(한 줄=한 명령, 소비 후 삭제): `probe` / `chat <문구>` / `move <dx> <dy> <dz>` / `stop` / `quit`.

| 종류 | 신호 |
|---|---|
| **Chat 양방향**(4) | A→B 수신 / B→A 회신 / 연속 발신 순서 보존 / **발신자 id 교차 일치**(서버 주입, 위조 아님) |
| **Grab 핸드오버**(5) | A잡기 Owner=A / A놓기 위치전파+Owner 유지 / B탈취 Owner=B / B놓기 전파 / A재탈취 |
| **과녁→점수**(서버권위) | A 명중 → 서버 집계 → **B가 동일 스코어보드·승자·리셋 수신**(B는 한 발도 안 쏨) |

**부분 PASS를 그대로 기록한다** — 3종 중 일부만 되어도 실패가 아니다. **각각 어디서 멈췄는지가 산출물.**
대상 컴포넌트가 룸에 배치돼 있지 않으면 그건 **인프라 실패가 아니라 배치 부재** → 그렇게 적고
`/add-component` 를 다음 단계로 제안한다.

### Phase 7 — Teardown (§9 원상복구)
1. B: `.promptscene-autoplay` 삭제 → `.promptscene-cmd` 에 `quit`.
2. A: `isPlaying=false` **확인 후** `PS_VerifyMultiplayer.Teardown`(Play 중 복원은 Play 종료 스냅샷에 덮인다).
3. `Packages/manifest.json` md5가 Phase 0과 **동일**한지 대조. `Temp/ps_mp_*.txt` 삭제.
4. **클론 폴더는 지우지 않는다** — 다음 세션의 1회성 비용을 없애는 자산이다.

⚠ **Teardown 후 확인:** 클론 에디터를 끄면 **원본의 `unity-mcp-server` 가 같이 죽는다**(에디터 자체는 멀쩡).
MCP가 끊기면 되살린다:
`Library/mcp-server/win-x64/unity-mcp-server.exe port=21017 plugin-timeout=10000 client-transport=streamableHttp authorization=none`

---

## VERIFY — acceptance

| # | Pass condition | Where |
|---|---|---|
| P1 | 원본 컴파일 0 · **manifest md5 세션 전후 동일** | Phase 0 / Phase 7 |
| P2 | 원본에서 하네스 **불활성**(role=`''`, `applied=False`) = shipped 동작 무변화 | Phase 2 / A 로그 |
| M1 | A 자기 아바타 `IsOwner=True` | `ps_mp_result.txt` M1 |
| M2 | B에 원격 아바타 `IsOwner=False` + B 자기 것 `owner=True` | `.promptscene-status` |
| M3 | A 이동 `(5,0,3)` 이 B에 관측 | Nudge + `.promptscene-status` |
| M4 | objId 집합 양측 동일(소유만 반전) | M4(A측) vs `.promptscene-status` |
| G2 | (옵션) 컴포넌트 파리티 신호 — **부분 PASS도 그대로 기록** | 종류별 표 |
| T1 | `QuickTestStarter` 값 복원 · **플레이모드 아님** · 클론 에디터 종료 | Phase 7 |
| — | `=== 2CLIENT GATE-1: PASS ===` (+ 있으면 GATE-2 판정) | 리포트 |

**Failure map:**
`role-detect role=''` in the clone → `.clone` 파일 부재(ParrelSync가 클론으로 안 만든 것) 또는 하네스 미설치.
`applied=False reason=QuickTestStarter-not-found` → 클론이 QuickStart 를 안 열었다(`Library` 사본의 마지막 씬).
B가 접속했다 **끊긴다** → 로그에서 `[Unity-MCP DependencyResolver] Restoring` 을 찾아라. 클론 MCP의 NuGet
재복원이 AssetDatabase refresh → 도메인 리로드 → Play 종료를 일으킨다(**넷코드 아님**). 복원이 끝난 뒤 다시 Play.
B가 아예 접속 못 함 → A가 host로 뜨기 전에 B가 Play에 들어갔다(재시도 없음). 순서를 다시.
양측 objId가 어긋남 → 같은 룸이 아니다(`roomSceneKey`/Addressables 주소 확인).

## Cleanup
Play 종료 · `Temp/ps_mp_*.txt` 삭제 · 클론 무장 해제(`.promptscene-autoplay`, `.promptscene-cmd`, `.promptscene-status`).
**클론 폴더·하네스·ParrelSync 는 남긴다.**

## Report
VERIFY 표를 실제 값으로 채워 PASS/FAIL 을 그대로 말한다. 그리고 항상:
- **정직 계약 재기술** — 증명 범위 = 같은 머신·에디터 2인·데스크톱까지. **실입력 2인 조작·3인+·실기기·배포는 밖.**
- **부분 PASS 명시** — 게이트 2에서 일부만 됐으면 각각 **어디서 멈췄는지**. 배치 부재면 인프라 실패가 아니라고 적고
  `/add-component` 를 제안한다.
- **신규 트랩** — 겪은 것만. 기존 9종은 xumflow-migration §17.5 에 있으니 **중복 기재하지 말고** 새것만 추가 제안.
