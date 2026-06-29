# AI-ready-OXR-SDK-presentation.md

> 발표 주제: **Is OXR SDK AI-ready?**
> From Static Sample Project to Prompt-based XR Framework
> 기준일: 2026-06-12 | 검증 환경: XRCollabDemo (Unity 6000.3.11f1)

---

# Slide 1. Is OXR SDK AI-ready?

## Key Message
XRCollabDemo를 proof case로 삼아, OXR SDK가 AI-ready 프레임워크로 발전할 수 있는 핵심 기반을 확인했다.

## Content
- **발표 질문:** Is OXR SDK AI-ready?
- **부제:** From Static Sample Project to Prompt-based XR Framework
- **접근:** XRCollabDemo를 단순 실행이 아닌, 문서와 패키지만으로 재구성하는 실험으로 다뤘다
- **결론 미리보기:** Foundation is proven. Full productization is next.

## Speaker Notes
이번 발표는 PromptScene 구현 보고가 아닙니다. 핵심 질문은 단 하나입니다. "OXR SDK는 AI가 읽고, 조립하고, 검증할 수 있는 형태인가?" XRCollabDemo를 proof case로 선택했고, 실제로 문서 기반 재현 실험을 통해 그 가능성을 확인했습니다. 오늘 발표는 그 과정과 결과, 그리고 앞으로의 방향을 공유합니다.

---

# Slide 2. Why This Question Matters

## Key Message
AI 시대에 SDK/프레임워크는 사람이 읽는 문서를 넘어, AI가 직접 조립할 수 있는 구조로 진화해야 한다.

## Content
- AI 에이전트는 문서를 읽고 실행 가능한 결과물을 직접 만들어야 한다
- 단순 API 레퍼런스나 샘플 코드만으로는 AI가 실제 프로젝트를 조립할 수 없다
- XR 프레임워크에서도 같은 전환이 필요하다
- **핵심 질문:** Can AI assemble a working XR room from our framework — without a human in the loop?

## Speaker Notes
지금 AI 에이전트는 코드를 생성하는 것을 넘어, 프레임워크의 구조를 이해하고 작동 가능한 결과물을 조립하는 단계로 가고 있습니다. 그렇다면 XR 프레임워크인 OXR SDK는 과연 AI가 다룰 수 있는 형태인가? 이 질문이 이번 실험의 출발점이었습니다.

---

# Slide 3. The Problem: Static Sample Projects

## Key Message
기존 XR 샘플 프로젝트는 사람이 직접 열고 파악하도록 설계되어 있다. AI에게는 이 암묵성이 가장 큰 장애물이다.

## Content
- 사용자가 직접 다운로드하고 Unity에서 열어야 한다
- 룸 생성 규칙, 네트워크 설정, 씬 전환 조건이 코드와 인스펙터에 암묵적으로 숨어 있다
- 패키지 구성, 프리팹 종속성, 스포너 설정 방식이 문서화되지 않은 관행에 의존한다
- **결론:** Static samples are useful for humans, but not enough for AI agents.

## Speaker Notes
기존 XRCollabDemo는 훌륭한 샘플 프로젝트입니다. 하지만 AI 에이전트 관점에서 보면 문제가 있습니다. 룸 하나가 작동하기 위한 조건들, 예를 들어 어떤 패키지가 필요한지, 스포너를 어떻게 설정해야 하는지, 씬 전환을 어떻게 구성해야 하는지가 암묵적으로 숨어 있습니다. 사람은 프로젝트를 열고 눈으로 파악할 수 있지만, AI는 명시적 규칙이 없으면 올바른 결과를 만들 수 없습니다.

---

# Slide 4. What Does "AI-ready" Mean?

## Key Message
AI-ready 프레임워크는 문서, 명시적 계약, 검증 절차, 모듈 구조, 그리고 프롬프트 기반 조립 가능성을 갖춰야 한다.

## Content

| Criterion | Meaning |
|---|---|
| Document-based reproducibility | 문서만 보고 작동 단위를 재현할 수 있음 |
| Explicit contracts | 코어/기능/씬/프리팹 규칙이 명시됨 |
| Runtime validation | 실제 실행으로 작동 여부를 검증 가능 |
| Modularity | 기능을 추가/제거해도 코어가 깨지지 않음 |
| Prompt-based assembly | 자연어 요청을 구조적 작업으로 변환 가능 |

## Speaker Notes
AI-ready는 단순히 "AI가 코드를 생성할 수 있다"는 뜻이 아닙니다. 프레임워크가 AI의 입력과 출력을 안전하게 받아들일 수 있는 구조적 기반을 갖춰야 합니다. 이 다섯 가지 기준을 바탕으로 XRCollabDemo를 실험했습니다. 이 슬라이드는 이후 실험 결과를 평가하는 기준표이기도 합니다.

---

# Slide 5. Experiment Design: XRCollabDemo as Proof Case

## Key Message
XRCollabDemo를 단순 실행 대상이 아니라, OXR SDK의 AI-ready 가능성을 검증하는 proof case로 재정의했다.

## Content
- **목표:** 문서와 패키지만으로 작동 룸을 재구성할 수 있는가?
- **대상:** XRCollabDemo (Unity 6000.3.11f1, MST + FishNet + XumNet)
- **방법:** `build-working-room.md` 한 파일만 보고 `BasicRoom`을 처음부터 조립 → 빌드 → 실행 → 검증
- **판단 기준:** 서버 등록, 클라이언트 검증, 아바타 스폰, UI 전환까지 전 구간 실측

## Speaker Notes
중요한 것은 실험 조건입니다. "XRCollabDemo를 다운로드해서 실행했다"가 아닙니다. 기존 프로젝트를 참고하지 않고, 문서 파일 하나만을 기반으로 새 룸을 처음부터 조립했습니다. 이것이 AI-ready 검증의 핵심 조건입니다. AI도 동일한 방식으로 작업하기 때문입니다.

---

# Slide 6. Key Evidence: Document-Only Room Reconstruction

## Key Message
`build-working-room.md` 하나만으로 `BasicRoom`을 조립하고, 전 구간 작동을 실측으로 확인했다.

## Content
**실측 결과 (BasicRoom):**
- ✓ 서버: `Online Scene: BasicRoom` 등록 + `Client 0 is successfully validated`
- ✓ 클라이언트: Client 로비 씬 언로드 / BasicRoom 로드 / C-MasterCanvas 소멸 (UI 자동 전환)
- ✓ NetworkObjects = 5 | Desktop(Clone) + mixamorig:Hips 아바타 룸에 스폰
- ✓ 아바타 카메라 활성 | 게임뷰에 룸 표시

**결론:** "문서/패키지만으로 작동 룸 재현 가능"이 성립

## Speaker Notes
이 결과가 이번 실험에서 가장 중요한 증거입니다. 단순히 룸이 열렸다는 것이 아니라, 서버-클라이언트 연결, 씬 전환, 아바타 스폰, UI 전환까지 전 구간을 실측으로 확인했습니다. 문서 기반 재현성이라는 AI-ready의 첫 번째 기준이 충족됐습니다.

---

# Slide 7. Making It AI-readable: C1~C4 Invariants

## Key Message
룸이 작동하기 위한 암묵적 규칙을 C1~C4 명시적 불변식으로 정리했다. 이것이 AI가 안전하게 조립하기 위한 프레임워크 계약이다.

## Content

| 불변식 | 내용 | 위반 시 증상 |
|---|---|---|
| **C1** 컬렉션 일치 | 서버·클라 NM 모두 `DefaultPrefabObjects` 동일 | 입장은 되나 아바타 안 보임 |
| **C2** 플레이어 스포너 | `XumPlayerSpawner` + prefab 인스턴스 필수 | 입장 검증 실패 |
| **C3** 씬 전환 | `_offlineScene=Client` 명시 필수 | 로비가 룸 위에 남음 |
| **C4** 토폴로지 | 서버=Master+Room exe / 클라=Client+Room 동시 로드 | 연결 불가 |

## Speaker Notes
이 네 가지 조건은 XRCollabDemo 프로젝트를 실제로 실험하면서 발굴한 것들입니다. 코드에 주석으로 달려 있지 않고, 문서에도 명시되어 있지 않았습니다. 사람은 디버깅을 통해 파악할 수 있지만 AI는 그럴 수 없습니다. 이 조건들을 명시적 계약으로 정리한 것 자체가 AI-ready를 위한 중요한 진전입니다.

---

# Slide 8. Critical Finding: Spawner Must Be a Prefab

## Key Message
스포너를 AddComponent 방식으로 만들면 AI가 생성한 룸은 항상 입장 실패한다. 명시적 규칙이 없으면 AI는 잘못된 방식을 선택한다.

## Content
- **문제:** 스포너를 스크립트로 `AddComponent` 시 NetworkObject가 유효 scene id를 못 받음
- **증상:** 클라이언트 입장 시 `"Failed to confirm the access"` 거부 (ReproRoom 실측)
- **해결:** `Room-PlayerSpawner.prefab`으로 패키지화 → prefab 인스턴스화로만 허용
- **의미:** AI 조립 과정에서 반드시 따라야 하는 명시적 프레임워크 규칙

> AI does not need more code generation; it needs correct framework rules.

## Speaker Notes
이 발견이 중요한 이유가 있습니다. AI에게 "룸을 만들어"라고 시키면, AI는 가장 자연스러운 방식으로 스포너를 코드로 생성하려 합니다. 하지만 그 방식은 실제로 작동하지 않습니다. 이 규칙이 명시화되어 있지 않으면 AI는 반복해서 실패합니다. 그래서 `Room-PlayerSpawner.prefab`으로 패키지화하고 이 규칙을 계약으로 만들었습니다. AI-ready는 더 많은 코드 생성 능력이 아니라, 더 명확한 프레임워크 규칙에서 시작합니다.

---

# Slide 9. Content Modularity: RoomCore + Features

## Key Message
코어는 얇게, 기능은 자기등록으로. 이 구조 덕분에 기능 하나하나를 프롬프트 기반 모듈로 만들 수 있다.

## Content
- **SYSTEMS (Core):** `RoomCore`는 특정 기능을 컴파일타임에 모름. `IRoomCore` 서비스 + 레지스트리 제공
- **FEATURES (Content):** `IToggleableContent`로 `RoomCore.Instance`에 자기등록. 빼도 코어가 깨지지 않음
- **파일럿:** `RulerContent` — 두 점 측정선 + 거리 라벨(4.00m) 렌더 확인. DeepChairProject 의존 0
- **씬 계층:** `SYSTEMS / ENVIRONMENT / UI / FEATURES / _DYNAMIC`
- **의미:** Features can become prompt-generated modules, not hardcoded sample code.

## Speaker Notes
이 구조는 Unreal Engine 5의 Modular Game Features와 동일한 패턴입니다. 코어는 기능의 존재를 모르고, 기능은 코어에 자기등록합니다. 이렇게 되면 향후 "/RoomContent-Ruler" 같은 스킬 명령으로 새 기능을 생성하고 룸에 추가하는 것이 구조적으로 가능해집니다. RulerContent 파일럿이 이 구조가 실제로 작동함을 확인했습니다.

---

# Slide 10. Before vs. After: Redefining XRCollabDemo

## Key Message
XRCollabDemo는 "다운로드하는 샘플"에서 "프롬프트로 조립 가능한 best sample"로 재정의될 수 있다.

## Content

| | **Before** | **After** |
|---|---|---|
| 접근 방식 | 다운로드하고 Unity에서 실행 | 프롬프트로 룸 조립 요청 |
| 구조 파악 | 사람이 씬을 직접 열고 확인 | 문서·계약·불변식이 명시됨 |
| 기능 추가 | 코드 직접 수정 | Content 모듈 자기등록 |
| 검증 | 수동 실행 확인 | C1~C4 자동 검증 가능 |
| 샘플의 역할 | 정적 참고 프로젝트 | OXR SDK의 AI-ready best sample |

## Speaker Notes
이것이 이번 실험이 보여주는 가장 큰 전환입니다. XRCollabDemo가 단지 코드 샘플이 아니라, OXR SDK가 AI-ready 프레임워크로 갈 수 있음을 보여주는 best sample이 될 수 있다는 것입니다. 이 전환은 단번에 이뤄지지 않습니다. Phase 3, 4, 5를 거쳐 점진적으로 완성됩니다.

---

# Slide 11. Roadmap

## Key Message
다음 단계는 이 증명된 기반을 AI 저작 워크플로우로 완성하는 것이다.

## Content

| Phase | 목표 | 핵심 산출물 |
|---|---|---|
| **Phase 3** Launchpad UI | 레지스트리 기반 기능 토글 UI | 아이콘 그리드 → `SetEnabled` |
| **Phase 4** Skills | LLM 기반 기능 생성 스킬화 | `/RoomContent-<feature>` |
| **Phase 5** PromptScene | 자연어 → 룸 조립 + 자동 검증 | `/promptscene` + C1~C4 하네스 |

**현재 완료:**
- ✓ 문서 기반 재현성 (BasicRoom)
- ✓ C1~C4 명시적 불변식
- ✓ RoomCore + IToggleableContent 구조
- ✓ RulerContent 파일럿

## Speaker Notes
현재까지 Phase 1, 2에 해당하는 핵심 기반이 증명됐습니다. Phase 3는 런치패드 UI로, 사용자가 기능을 직접 토글할 수 있게 합니다. Phase 4는 스킬화로, AI가 새 기능을 생성하는 명령을 정의합니다. Phase 5가 최종 목표인 자연어 프롬프트 기반 룸 조립과 자동 검증입니다. 이 로드맵은 XRCollabDemo를 점진적으로 OXR SDK의 AI-ready best sample로 전환하는 경로입니다.

---

# Slide 12. Conclusion: Foundation Is Proven

## Key Message
아직 완전히 제품화된 AI-ready SDK라고 말하기는 어렵다. 하지만 AI-ready 프레임워크로 갈 수 있는 핵심 기반은 증명됐다.

## Content

**Is OXR SDK AI-ready?**

> Not fully productized yet — but the foundation is proven.

**증명된 기반:**
- 문서 기반 재현성 → BasicRoom 실측 확인
- 명시적 계약과 불변식 → C1~C4 정리
- 모듈형 콘텐츠 구조 → RoomCore + IToggleableContent
- 런타임 검증 가능성 → 서버·클라이언트 전 구간 실측

**다음 전환:**
> Static sample project → Prompt-based AI-ready framework sample

## Speaker Notes
이번 실험의 결론을 정리하면 이렇습니다. XRCollabDemo를 통해 문서 기반 재현성, 명시적 계약, 모듈형 콘텐츠, 런타임 검증이라는 AI-ready의 핵심 기반을 확인했습니다. OXR SDK가 AI-ready 프레임워크로 발전할 수 있다는 가능성이 증명됐습니다. 아직 완전히 제품화된 단계는 아닙니다. 하지만 XRCollabDemo는 이제 "다운로드해서 실행하는 샘플"이 아니라, "OXR SDK의 AI-ready best sample 후보"로 부를 수 있습니다. 감사합니다.
