# 개척 청구서 — 아바타 "앉은 자세(seated body pose)" + 크로스클라 동기

작성: 2026-07-27 · 계기: `/add-component 의자 착석` on AssembleRoom (studio)
관련: [[promptscene-architecture]] · capability-map.md (⚠ 코드-대체 표 + ⛔ 미감 줄) · promptscene-content-contract.md §4.5

> ## ⚠ 개정 (2026-07-27, 같은 날 · **개척 완화 결정**)
> 사용자 결정: **"클로드코드가 코드로 할 수 있는 부분이면 그냥 코드로 다 해라. 개척 내용은 추천사항으로 남겨라."**
> 이 청구서의 "3중 게이트"는 그 결정으로 **대부분 해소**되었다 — 아래 §게이트 표의 새 판정을 따른다.
>
> | 원래 게이트 | 새 판정 | 코드-대체 경로 |
> |---|---|---|
> | 1. seated 클립 에셋 0 (D3) | ⚠ **코드로 짓는다** | 에디터 스크립트로 humanoid `AnimationClip` 절차 생성(본 로컬 회전 `AnimationCurve` 키잉) + `AnimatorController` 자산 생성 + 레이어 **IK Pass on** |
> | 2. per-bone `BodyAnimationWeights` setter 없음 | ⚠ **코드로 짓는다** | getter가 돌려주는 `FullBodyBuffer<float>` **참조를 직접 mutate**(또는 백킹 필드 리플렉션). 패키지 소스 무수정 |
> | 3. §4.5 SYSTEMS 리치인(아바타 프리팹) | ⚠ **콘텐츠측으로 우회** | sit-state를 **FEATURE 소유** `NetworkObject` 프리팹의 `SyncVar<bool>`/`[XumRPC]`로 들고, 원격 아바타는 **런타임 룩업**으로 찾아 포즈 적용 → 아바타 프리팹·DefaultPrefabObjects 무수정(C1 불필요) |
>
> **잔여 ⛔ = 미감뿐.** "앉은 형태"는 코드가 만들지만 **"잘 앉은 느낌"(무게 이동·손 위치·시선)** 은 사람이 판정한다(게이트 a).
> 아래 §청구는 이제 **필수 조건이 아니라 업그레이드 추천사항**으로 읽는다.

## 요청의 분해 — 어디까지가 ✅ 재조합이고 어디부터가 ⛔ 개척인가

사용자 요청("의자 근처 → E로 앉기 → 착석 위치 이동 → **앉기 애니메이션** → 다시 E로 일어나기")은
**이동(move)** 과 **자세(pose)** 경계에서 갈린다.

| 부분 | 판정 | 근거 |
|---|---|---|
| 접근 감지 / "E로 앉기" UI / 착석 위치 **이동** / WASD 프리즈 / E로 일어나기 | ✅ **재조합** | scene `Dummy` 재배치 + client-auth NetworkTransform 복제 + `DummyController.enabled` 토글. `ChairSitContent.cs`로 이미 작성됨 |
| **앉은 몸 자세(seated body pose) + 그 크로스클라 동기** | ⛔ **개척** | 아래 3중 게이트 |

`ChairSitContent.cs`(이미 작성, `.../Content/ChairSit/`)는 자세 부분을 **정직하게 deferred 개척으로 스코프-아웃**하고 이동 슬라이스만 구현한다.

## 기술적으로는 가능하다 — "그냥 컨트롤러 스왑"으로는 안 될 뿐

DeepChairProject는 IK 아바타 위에 seated clip을 blend해서 이 효과를 낸다. 메커니즘 ground-truth:

- **주입 seam = `MotionAvatar`의 animator-blend 경로** (retarget을 끄지 않는다):
  - `MotionAvatar.SetAnimationBlendWeight(float)` (global 0..1) + `OnAnimatorIK(int)`가 clip을 sample한 뒤
    per-bone `Lerp(리타겟 pose, clip pose, _animationBlendWeight * BodyAnimationWeights[i])`로 write.
    weight=1 + per-bone weight>0 이면 seated clip이 리타겟을 이긴다.
  - `com.kisti.unifiedxrmotion@40db6decfc7f\Runtime\Scripts\MotionAvatar\MotionAvatar.cs` (SetAnimationBlendWeight L41, Update L88-106, OnAnimatorIK L108-129)
- **DeepChair가 그렇게 한다**: `LocalPlayer.cs:205-222`는 `animator.runtimeAnimatorController = sitController/null`만 바꾸고,
  나머지(blend weight>0, per-bone weight>0, sitController에 IK Pass)는 **그 프로젝트 rig 프리팹에 baked** 되어 있어 gate가 열린다.
  즉 lift-and-shift가 아니라 **이 프리팹 설정을 studio rig에 재현**해야 한다.
  (`C:\Unity\DeepChairProject\Assets\OXRApp\LocalUser\Scripts\LocalPlayer.cs:205-222`)
- **studio rig는 blend가 dormant**: `Assets\App\Prefabs\Avatar\_XBot-networked_NameTag.prefab` —
  `_animationBlendWeight: 0`, per-bone `_animationBlendWeights` 전부 0, Animator 컨트롤러 없음, `_bodyType: 1`(FullBody).
- **네트워킹 idiom (확립됨)**: 지속 상태 = FishNet `SyncVar<bool>` + `OnChange` (`A_DisplayName.cs:12,17` 실사용),
  client→server→전 observer 요청 = `[XumRPC]` + `XumView.RPC(nameof(...), RpcTarget.All, isSitting)`
  (`com.oxr-sdk.xumnet@06584e0d265d\Runtime\XumView.cs` RPC L302 / RpcTarget enum L19-31 / XumRPC L629).
  ⚠ `A_DisplayName.cs:38-41`의 인라인 주석은 틀림 — 권위는 `XumView.cs` 구현(All=sender 포함 전원, Others=sender 제외).
- **에디트타임 프리팹 편집은 런타임 스폰까지 생존**: 아바타는 `XumNetwork.Instantiate`로 DefaultPrefabObjects에서 스폰됨.
  NetworkBehaviour를 avatar 프리팹에 추가하면 **FishNet Generator 재생성(C1)** 필요.
  (`build-studio-room.md` §3c / `promptscene-content-contract.md:144`)

## 게이트 표 — 원본 판정 (2026-07-27 완화 결정 **이전**; 위 §개정이 이를 대체한다)

1. **에셋 부재 (D3=0)** — seated humanoid 애니메이션 clip(+ 레이어 IK Pass on)이 프로젝트에 **0개**. 이건 *창작 에셋*이라
   사람이 만들거나 소싱해야 한다(스킬 규칙: 창작=사람 선택). AI가 그럴듯한 앉기 클립을 날조하지 않는다.
2. **API 갭** — per-bone `BodyAnimationWeights`에 **public setter 없음** (getter가 `FullBodyBuffer<float>` 참조만 반환).
   전신을 앉히려면 그 버퍼 엔트리를 직접 mutate(리플렉션)하거나 패키지 경계 변경이 필요.
3. **계약 §4.5** — (a) **mechanism-not-policy**: 몸 자세는 *policy*(게임 규칙)이라 SYSTEMS 승격 실패.
   (b) **rule-of-two**: sit-state는 *첫 번째 소비자* — §4.5는 두 번째 소비자가 있어야 승격 허용.
   → avatar 프리팹에 네트워크 sit-state 컴포넌트를 다는 것 = **SYSTEMS reach-in**, 플랫폼 오너의 명시적 승격 결정 필요.

## 미확인 리스크

- Desktop.prefab의 소스 rig(guid `991722d14a09198438547732c760e154`) 원본을 Assets/패키지에서 못 찾음 →
  Desktop이 상속하는 실제 blend/bodyType 값 미확정. **에디터에서 Desktop.prefab을 열어 MotionAvatar inspector로 최종 확인 권장.**

## 추천사항 (완화 후 — **막는 조건이 아니라 업그레이드 제안**)

1. **아티스트/모캡 seated 클립** 1개(전신) — 절차 생성 포즈를 교체하면 자세 품질이 올라간다.
   절차 포즈는 "앉아 있음"은 읽히지만 무게 이동·손 위치의 자연스러움은 없다.
2. **UnifiedXRMotion에 per-bone weight 공식 setter 요청** — 리플렉션/버퍼-mutate seam이 사라진다.
   그전까지 이 지점은 **패키지 업그레이드 시 깨질 수 있는 취약 seam**으로 표시해 둔다.
3. **두 번째 소비자가 생기면 §4.5 승격 재검토** — 그때는 rule-of-two를 만족하므로 sit-state를 콘텐츠측에서
   SYSTEMS로 올리는 오너 결정이 가능하다. (지금은 콘텐츠측 우회로 아바타 프리팹을 건드리지 않는다.)

## 구현 계획 (완화 후 — 지금 코드로 짓는 것)

절차 생성 seated `AnimationClip` + `AnimatorController`(IK Pass on) → `MotionAvatar.SetAnimationBlendWeight(1)` +
per-bone weight 버퍼 mutate → sit-state는 **FEATURE 소유** 프리팹의 `SyncVar<bool> _isSitting` +
`[XumRPC] RequestSit(RpcTarget.All, bool)`, 원격 아바타는 런타임 룩업으로 찾아 포즈 적용(아바타 프리팹 무수정 →
**C1 불필요**). ✅ 이동 슬라이스(`ChairSitContent.cs`, 이미 작성)와 합쳐 §5 + §6.5로 증명하고, 자세의 **미감은
Phase 6 사람 핸드오프**(Play 유지 + 조작 레시피)로 사용자가 눈으로 판정한다.
