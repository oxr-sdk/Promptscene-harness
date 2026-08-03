# 개척 청구서 — 채팅 + 메타 시스템 키보드 (AssembleRoom)

> **작성:** 2026-07-27 · **요청:** "채팅기능을 넣고, 채팅창 입력하면 메타에서 채팅키보드 나오게" (`/add-component … #AssembleRoom`)
> **결과:** 4개 슬라이스 중 3개를 **지었고**(✅ 1 + ⚠ 2), 잔여 1개만 청구서로 남는다.
> SSOT: [add-component SKILL](../skills/add-component/SKILL.md) Phase 0 step 2 (3분 등급), [capability-map](capability-map.md).

---

## 슬라이스별 판정

| # | 슬라이스 | 등급 | 처리 |
|---|---|---|---|
| 1 | AssembleRoom에 텍스트 채팅 | ✅ 재조합 | `ChatContent`(id=`chat`) + `ChatChannel.prefab`(C1 기등록) 배치·배선. §5 PASS |
| 2 | 헤드셋에서 **보이는** 채팅창 | ⚠ 코드-대체 | 기존 패널이 IMGUI(`OnGUI`)라 HMD 아이 버퍼에 안 그려짐 → World Space uGUI 면(`ChatWorldPanel`)을 런타임 생성 |
| 3 | 입력창 포커스 시 **메타 시스템 키보드** | ⚠ 코드-대체 | Meta XR Core SDK 부재 → OVRManager가 하던 일을 매니페스트 1줄 + `TouchScreenKeyboard` 직접 구동(`SystemKeyboardBinder`)으로 대체 |
| 4 | 실기기(Quest)에서 키보드 오버레이가 **실제로 뜨는 것** | ⛔ **(c) 없는 인프라** | 아래 청구 |

---

## 잔여 ⛔ — 게이트 (c) 없는 인프라(실기기)

**막는 것.** 에디터(Windows)는 `TouchScreenKeyboard.isSupported == false`다. 단일 에디터 QuickTest로 증명 가능한 것은
**"선택 → 키보드 요청 코드 경로가 실행된다"**까지이고(로그 `[chat-keyboard] system keyboard NOT supported on this platform`으로 확인됨),
**"Horizon OS 오버레이 키보드가 눈앞에 실제로 뜬다"**는 Quest 실기기에서만 확인된다.

**추가 불확실성 — SDK 없이 되는가.** Meta 공식 문서는 키보드 오버레이가
[Meta XR Core SDK 설치를 요구한다](https://developers.meta.com/horizon/documentation/unity/unity-keyboard-overlay/)고 적는다
(`OVRManager → Quest Features → Requires System Keyboard`). 다만 그 토글의 **빌드타임 산출물은 매니페스트 한 줄**
(`oculus.software.overlay_keyboard`)이고, 런타임 호출은 Unity 표준 `TouchScreenKeyboard`다. 그래서 이 프로젝트는
매니페스트를 직접 선언해 SDK 없이 같은 조건을 만들었다. **OVRPlugin 로드 없이도 OS가 오버레이를 띄우는지**는 실기기 판정 사항이다.

**개척 비용(견적).**
1. Quest 클라 APK 빌드 → 설치 → 룸 입장 → 채팅 ON → 입력창 조준·선택 (기존 `docs/build-meta-client.md` 절차 그대로, 신규 인프라 0).
2. 오버레이가 뜨면 → 슬라이스 3·4가 ✅로 승격, 이 청구서는 닫힌다.
3. 안 뜨면 → 게이트가 **(c)에서 (b) 플랫폼 오너 결정**으로 바뀐다: `com.meta.xr.sdk.core` 패키지 추가 여부(= `manifest.json` 변경 = PackageCache 영역)를 오너가 결정해야 한다. 대안(오너 결정 불필요)은 **콘텐츠측 인앱 가상 키보드**(월드 스페이스 uGUI 키 버튼 격자 + XR 레이 타격)로, 이는 ⚠ 코드-대체로 지을 수 있으나 OS 오버레이의 음성 받아쓰기·다국어는 못 준다.

**무엇이 열어주나.** Quest 1대 + 15분. 사람이 헤드셋을 쓰고 입력창을 한 번 누르면 끝난다.

---

## 함께 나가는 추천사항 (블로커 아님)

| 코드-대체물 | 무엇의 대체인가 | 업그레이드 |
|---|---|---|
| `ChatWorldPanel` — 런타임 생성 World Space uGUI 면 | 헤드셋에 안 보이는 IMGUI 패널 | 디자이너가 authoring한 캔버스 프리팹으로 교체하면 레이아웃·미감을 사람이 직접 조정 가능(현재는 코드가 배치) |
| 동적 OS 폰트(`Font.CreateDynamicFontFromOSFont`) | 프로젝트에 없는 한글 폰트 에셋 | **한글 TMP/폰트 에셋 1개를 프로젝트에 넣으면** Quest 한글 글리프가 OS 폰트 운에 안 걸린다. 데스크톱은 Malgun Gothic으로 라이브 확인됨, **Quest의 Noto CJK 폴백은 미검증** |
| `SystemKeyboardBinder` — 손수 구동하는 `TouchScreenKeyboard` | Meta XR Core SDK의 `OVRManager` 시스템 키보드 토글 | SDK를 도입하면 바인더가 필요 없어지고 Meta의 `OVRVirtualKeyboard`(앱 내 렌더링, 손 추적 타이핑) 선택지도 열린다 — 단 §4.5 오너 결정 |
| 매니페스트 직접 선언 `oculus.software.overlay_keyboard` | OVRManager 체크박스의 빌드타임 산출물 | 동일 — SDK 도입 시 자동 생성으로 대체 |

---

## 이번 실행에서 라이브로 증명된 것 (참고)

`§5/§6.5 ADD-COMPONENT VERDICT (FEATURE): PASS` · 채널 1개만 스폰(`IsSpawned=True`) · World Space 패널의
입력창→전송 버튼 경로로 보낸 메시지가 `ServerRpc → ObserversRpc`를 돌아 패널 로그에 한글로 렌더 ·
`GraphicRaycaster` + `TrackedDeviceGraphicRaycaster` 동시 부착(데스크톱 마우스 + XR 레이) · Error 0.
증명되지 **않은** 것: 실기기 키보드(위), XRI 손/컨트롤러 실조작 손맛, 2클라 동시 채팅, Smart-Deploy.
