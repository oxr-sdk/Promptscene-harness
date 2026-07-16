# 그랩/소유권 기존 구현 조사 (G0a) — SSOT

> 2026-07-16 조사. **산출물 = 조사 기록**(구현·수정 없음). 원본은 파일 원문 스니펫으로 검증했으나, **이 문서는 공개 레포에 있으므로 private SDK(`com.oxr-sdk.xumnet`)·상용 에셋(FishNet)·앱 소스의 코드 원문을 싣지 않는다** — 대신 **동작 서술 + `파일:라인` 포인터**로 기록한다. 원문은 각 포인터의 로컬 PackageCache/Assets에서 직접 확인할 것.
>
> **이 문서의 용도:** D4 "1단계(잡기 소유권)"에 대한 판정 근거의 단일 출처(SSOT). 두 번째 소비자가 "소유권을 계약으로 승격할지" 검토할 때 여기서부터 읽는다.

---

## 판정 (결론 먼저)

**감싸기(wrap) — 단, 계약 승격 없이 FEATURE 지역 사용으로 충분.**

근거:
1. 소유권 코어 `XumView`는 **이미 패키지 레이어**(`com.oxr-sdk.xumnet`)에 있다 — SYSTEMS를 확장할 필요가 없다.
2. FEATURE와의 접점은 **`xumView.RequestOwnership()` 한 줄**.
3. 씬 기본 모드가 **Takeover** — 승인 절차 불요(즉시 탈취).
4. 놓기 후 위치 전파는 **client-authoritative NetworkTransform**이 담당(새 오너 authority 자동 승계).
5. **M1의 `RulerMeasurementView`-내부-RPC 패턴과 동형** — 네트워크 기계를 FEATURE 프리팹 내부 NetworkBehaviour에 두고, 계약(IRoomCore)엔 손대지 않는다. 두 소비자 규칙(계약 승격은 두 번째 소비자가 실제로 생겼을 때) 준수 → **지금은 승격 안 함**.

→ 두 번째 잡기-소유권 소비자가 실제로 생기면, 그때 이 문서를 근거로 계약 승격(예: `IRoomCore`에 소유권 헬퍼)을 재검토한다.

---

## 조사 방법 정정 (기록용)

- 요청 경로 `Assets/Scenes/Room`은 없음. **실제 주 룸 씬 = `XRCollabDemo/Assets/App/Scenes/Room.unity`**.
- 프리팹 파일만 보면 NetworkTransform 부재로 오판할 수 있음: `Assets/App/Drone/Drone To Assemble.prefab`은 `m_AddedComponents: []`(FBX만 중첩). 그러나 **씬은 NetworkTransform을 그랩 오브젝트에 씬 레벨로 직접 부착**(`m_PrefabInstance: 0`, script guid `a2836e36774ca1c4bbbee976e17b649c`, 씬 내 18개). 판정은 씬 직접 확인 기준.

## 확정된 그랩 스택 (그랩 대상 예: 드론 파트 GameObject `1680015954`)

`NetworkObject` + `XumView`(ownershipMode=**Takeover**) + `NetworkTransform`(client-auth) + `XRGrabInteractable` + `GestureHandler` + `AssemblySnapPart`. ToInteract(`986845158`)도 NetworkTransform 부착.

관련 파일(포인터):
- `XRCollabDemo/Assets/App/Scripts/C_GestureHandler.cs` (class `GestureHandler`)
- `XRCollabDemo/Assets/App/Scripts/AssemblySnapPart.cs`
- `XRCollabDemo/Assets/App/Scripts/DesktopMouseGrabInteractor.cs` (씬 미배치, 데스크톱 경로)
- `XRCollabDemo/Assets/App/Scripts/XRInteractionWrapper.cs`, `InteractionManager.cs` (에디터 셋업 헬퍼)
- 소유권 코어: `Library/PackageCache/com.oxr-sdk.xumnet@335d5509bd86/Runtime/XumView.Ownership.cs`, `XumView.cs`
- FishNet: `Library/PackageCache/com.firstgeargames.fishnet@ad1814df5059/Runtime/Object/NetworkObject/NetworkObject.cs`, `.../Runtime/Generated/Component/NetworkTransform/NetworkTransform.cs`

---

## Q1. 잡을 때 무엇이 호출되나

**체인:** `XRGrabInteractable.selectEntered`(XR Toolkit 내장) → `AssemblySnapPart.OnSelectEntered → RequestOwnershipIfNeeded()` → `GestureHandler.OnHandsGrab()` → `XumView.RequestOwnership()` → `CmdTakeOwnership`(ServerRpc) → 서버에서 `base.NetworkObject.GiveOwnership(sender)`(FishNet).

- **패턴 = 서버-권위 ServerRpc.** 클라가 FishNet을 직접 부르지 않음 — `CmdTakeOwnership`은 `[ServerRpc(RequireOwnership = false)]`로 서버에 위임하고, `GiveOwnership`은 서버 시작 상태 가드(`NetworkManager.IsServerStarted`)로 서버에서만 실행.
- **클라 권한 처리:** `RequireOwnership=false`라 비소유자도 호출 가능. 게이팅은 `if (xumView.IsMine) return;` — `IsMine`은 FishNet `base.IsOwner` 래핑.
- **모드:** 씬값 `ownershipMode:1` = **Takeover**(enum 정의 `OwnershipMode { Fixed, Takeover, Request }` → 0/1/2) → 요청/승인 절차 없이 즉시 탈취. (Request 모드였다면 `CmdRequestOwnership` → 현 오너에게 `TargetRpc` → `ApproveOwnership` 경로.)
- **승인 정책(주의):** `GestureHandler.OnOwnershipRequest`는 수신 즉시 `view.ApproveOwnership(...)`를 **무조건** 호출 — 승인 정책이 껍데기에 하드코딩.

포인터: `XumView.Ownership.cs`의 `RequestOwnership()`, `[ServerRpc] CmdTakeOwnership()`; `C_GestureHandler.cs`의 `OnHandsGrab()`/`OnOwnershipRequest()`.

## Q2. 놓을 때 확정 위치 전파

- **놓기 처리:** `selectExited → AssemblySnapPart.OnSelectExited`는 인디케이터 끄기 + 스냅 임계 내면 `SnapToTarget()`뿐.
- **소유권 반납 없음:** 앱 코드(`Assets/App/Scripts`) 전체에 `ReleaseOwnership`/`RemoveOwnership`/`OnHandsRelease` 호출부 **0건**(grep 확인). `XumView`에 `CmdReleaseOwnership → RemoveOwnership` 경로가 존재하나 **호출자 없음**. ⇒ 놓아도 **소유권 유지**(Takeover 모델). 다음 사람이 잡으면 다시 `CmdTakeOwnership`으로 탈취.
- **위치 전파:** 별도 "확정 위치 RPC" 없음. **NetworkTransform 지속 동기화**(`_interval:1`, position/rotation on)가 최종 정지 위치를 전파. `_clientAuthoritative:1`이라 놓은 사람(오너)이 계속 송신자.
- **주의(기존 동작상 한계):** `SnapToTarget`은 오너 로컬 transform을 이동시키고 그 결과가 NetworkTransform으로 전파되는 구조. **스냅/조립 "상태" 자체의 네트워크 확정은 이 경로에 없음**(AssemblySnapPart는 Assembly-CSharp 로컬 로직) — 위치값만 동기화.

포인터: `AssemblySnapPart.cs`의 `OnSelectEntered/OnSelectExited/RequestOwnershipIfNeeded/SnapToTarget`.

## Q3. NetworkTransform — 소유권 이전 시 새 오너 authority 추종

**추종함 (YES, 확정).**

그랩 대상 인스턴스 필드값(`Room.unity` `&38272194`, GameObject `1680015954`, `FishNet.Component.Transforming.NetworkTransform`):
- `_addedNetworkObject`: 연결됨(`fileID 761078190`)
- `_clientAuthoritative: 1`, `_sendToOwner: 1`, `_interval: 1`
- `_synchronizePosition: 1`, `_synchronizeRotation: 1`

FishNet 로직(동작 서술):
- `CanControl()` — client-auth이면 `IsController`(= `IsOwner || (서버 && !Owner.IsValid)`)만 송신 허용.
- `SendToServer(...)` — 서버가 아니고 `!_clientAuthoritative || !IsOwner`면 즉시 리턴 → **오너만 서버로 송신**.
- `IsOwner`/`Owner`는 베이크값이 아니라 매 틱 재평가. `GiveOwnership`이 `PacketId.OwnershipChange`를 전파해 각 클라의 `Owner`를 갱신 → **새 오너가 자동으로 송신 authority를 승계**.

포인터: `NetworkTransform.cs`의 `CanControl()/SendToServer()/SendToClients()/MoveToTarget()`; `NetworkObject.QOL.cs`의 `IsController`.

## Q4. 네트워크가 걸려 있긴 한가

**실재함 (로컬 전용 아님).**
- 그랩 대상에 `NetworkObject`(스폰) + `XumView`(FishNet `NetworkBehaviour`) + `NetworkTransform`(FishNet) 실물 부착(씬 확인).
- 소유권 이전이 `ServerRpc/TargetRpc` 경유 + FishNet `GiveOwnership`이 소유권 변경 패킷(`PacketId.OwnershipChange`)을 Reliable 채널로 전송 + observer 재빌드(`RebuildObservers`).
- `XumView.IsMine`이 `nob.IsSpawned && base.IsOwner`를 요구 → 스폰된 네트워크 오브젝트 전제.

포인터: `NetworkObject.cs`의 `GiveOwnership(...)`/`RemoveOwnership(...)` — 서버에서 `SetOwner` 후 소유권 변경 패킷을 `ShareIds`면 전체 observer, 아니면 prev/new 오너에게만 전송.

## Q5. 앱레이어 결합도 — 추출 가능한 소유권 코어 vs UX 껍데기

| 계층 | 구성요소 | 위치 | 추출성 |
|---|---|---|---|
| **소유권 코어** | `XumView`(+`XumView.Ownership.cs`): `RequestOwnership`/`ApproveOwnership`/`OwnershipMode`/`IOwnershipCallbacks`, FishNet `GiveOwnership` 래핑 | 패키지 `com.oxr-sdk.xumnet` | **이미 앱 밖 코어** |
| **동기화 코어** | `NetworkTransform`(client-auth) | FishNet 패키지 | 코어. 부착만 하면 됨 |
| **UX 껍데기(앱)** | `GestureHandler`(그랩→RequestOwnership 브리지 + 자동승인), `AssemblySnapPart`(XR 이벤트↔소유권+스냅/조립), `XRInteractionWrapper`/`InteractionManager`(에디터 셋업), `Desktop/Keyboard` 입력 껍데기 | `Assembly-CSharp` | 얇은 브리지는 재사용 쉬움, 스냅/조립은 드론 특화 |

- **코어/껍데기 접점 = 딱 한 줄: `xumView.RequestOwnership()`.**
- **재사용 시 걸림돌 2가지(정책이 껍데기에 하드코딩):**
  1. **자동승인**이 `GestureHandler.OnOwnershipRequest`에 무조건 `ApproveOwnership`로 박힘 — 승인 정책 주입 불가.
  2. **놓기 반납 정책 부재**(Takeover 유지) — 반납형이 필요하면 껍데기에서 `CmdReleaseOwnership` 호출을 새로 걸어야 함(코어엔 이미 존재).

---

## 요약 한 줄

그랩 = XR 이벤트 → `XumView.RequestOwnership()`(Takeover, ServerRpc) → FishNet `GiveOwnership`; 위치는 client-auth `NetworkTransform`이 새 오너 authority를 따라 자동 전파; 네트워크 실재; 소유권 코어(XumView)는 패키지로 이미 분리돼 추출성 높고, 앱엔 자동승인·비반납 정책만 하드코딩. **→ SYSTEMS/계약 확장 불필요, FEATURE 지역 감싸기로 충분.**
