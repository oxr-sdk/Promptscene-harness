using UnityEngine;
using UnityEngine.UI;
using PromptScene.Core.UI;

/// <summary>
/// ⑥ KeyBadge — 월드 상호작용 프롬프트(다이제틱 프롬프트). `"E 키로 앉기"`류 월드 텍스트를 대체한다.
///
/// ── 왜 컴포넌트인가 ────────────────────────────────────────────────────────────────────────────
/// 결함이 두 개 겹쳐 있었다: ① 월드 텍스트에 크기 규율이 없었다(월드 스케일 고정이라 보는 위치에 따라
/// 벽만큼 커진다) ② HUD 컴포넌트가 아니라 다이제틱 프롬프트인데 디자인 시스템 **밖에** 있었다.
/// KeyBadge는 둘을 같이 닫는다 — HudTheme 토큰만 쓰고, 각크기를 스스로 고정한다.
///
/// ── 핵심 기계: 각크기 고정 ─────────────────────────────────────────────────────────────────────
/// 거리와 무관하게 항상 <see cref="HudTheme.BadgeTargetDeg"/>(기본 3°)의 시야각을 차지한다.
///   worldD = 2 · dist · tan(targetDeg/2)  →  localScale = baseScale · (worldD / BadgeBaseDiameterM)
/// 폭주가 **구조적으로** 불가능해진다: 멀어지면 커지고 가까워지면 작아져 각크기가 상수로 남는다.
/// 부수 효과로 글자의 캡 각크기도 상수다(<see cref="HudTheme.BadgeCapArcmin"/>) — 거리별 가독성 판정이 필요 없다.
///
/// ── 유리 스택 (F0) ────────────────────────────────────────────────────────────────────────────
/// 배지는 패널 밖(환경 위)에 뜨므로 **자기 Scrim을 들고 다녀야 한다.** 잉크를 보장 없이 환경 위 Film에
/// 직접 얹으면 밝은 배경에서 글자가 사라지고, U7이 그걸 FAIL로 잡는다. 그래서 원판은 2겹 체인이다:
///   __scrim(Scrim) > __disc(Film) > __keycap(GlyphDark + TextHi 헤일로)   +  __ring(RimTop, 형제·장식)
/// 배지는 보장을 **둘 다** 든다(Scrim + 헤일로). 패널 HUD가 유리 방향에서 Scrim을 α0으로 내렸어도
/// 배지는 환경 위에 홀로 뜨는 물건이라 Scrim을 유지한다 — 같은 규칙의 다른 답이지, 예외가 아니다.
/// 조상 체인이 곧 대비 스택이 되도록 **중첩**으로 쌓는다(형제로 깔면 게이트가 스택을 볼 수 없다).
///
/// ── 표시 규율 ─────────────────────────────────────────────────────────────────────────────────
/// · 상호작용 대상 **위쪽에 오프셋** 배치(바닥에 눕히지 않는다 — 그게 2m 텍스트가 생긴 경로다)
/// · 원거리 = 배지만(`E`) / 근거리(<see cref="HudTheme.BadgeLabelDistanceM"/> 이내) = 배지 + 라벨 알약
/// · 직렬 필드 0 (§3b) — 전부 <see cref="Attach"/>로 런타임 배선한다. 씬에 authoring하지 않는다.
/// </summary>
public class KeyBadge : MonoBehaviour
{
    /// <summary>U10이 "각크기 고정 컴포넌트를 갖는가"를 이 이름으로 찾는다.</summary>
    public const string RootName = "KeyBadge";

    private RectTransform _rt;
    private Transform _pill;
    private Text _keycapText, _labelText;
    private float _baseScale;
    private Font _font;

    /// <summary>현재 프레임에 계산된 각크기(도). 게이트가 3거리에서 이 값을 읽어 일정함을 단정한다.</summary>
    public float MeasuredDeg { get; private set; }
    /// <summary>이번 프레임의 카메라 거리(m).</summary>
    public float MeasuredDistanceM { get; private set; }

    /// <summary>
    /// 배지를 만들어 붙인다. `localOffset`은 상호작용 대상 기준 **위쪽** 오프셋이어야 한다.
    /// </summary>
    public static KeyBadge Attach(Transform parent, Vector3 localOffset, string keycap, string label, Font koreanFont = null)
    {
        var go = new GameObject(RootName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        var badge = go.AddComponent<KeyBadge>();
        badge.Build(keycap, label, koreanFont);
        return badge;
    }

    private void Build(string keycap, string label, Font koreanFont)
    {
        _font = koreanFont;
        _rt = (RectTransform)transform;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // 캔버스 박스는 배지 + 라벨이 들어갈 넉넉한 크기. 실제 크기는 아래 Row가 결정하고,
        // 캔버스 자체는 각크기 고정 스케일만 담당한다.
        _rt.sizeDelta = new Vector2(HudTheme.CircleD * 6f, HudTheme.CircleD);
        _baseScale = 1f / HudTheme.Legibility.PxPerMeter;   // 캔버스 1px = 실측 밀도의 1px
        _rt.localScale = Vector3.one * _baseScale;

        // 가로 한 줄: [원판] [라벨 알약]. 라벨이 꺼지면 원판만 남고 가운데 정렬이 유지된다.
        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(transform, false);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = HudTheme.Space3;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var fit = row.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        var rowRT = (RectTransform)row.transform;
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.pivot = new Vector2(0.5f, 0.5f);

        // ── 원판: Scrim > Film > 키캡. 조상 체인이 그대로 대비 스택이 된다(F0) ──
        var scrim = MkImage(RootName + "__scrim", row.transform, HudTheme.Scrim, HudSprites.Circle(HudTheme.CircleD));
        var scrimLE = scrim.gameObject.AddComponent<LayoutElement>();
        scrimLE.preferredWidth = HudTheme.CircleD; scrimLE.preferredHeight = HudTheme.CircleD;

        var disc = MkImage(RootName + HudTheme.Roles.Disc, scrim.transform, HudTheme.Film, HudSprites.Circle(HudTheme.CircleD));
        Stretch((RectTransform)disc.transform);

        // 링은 형제·장식(대비 판정 대상 아님). 월드에서는 1px이 사라지므로 RimW(2px). 위 밝고 아래 어두운 2톤.
        var ring = MkImage(RootName + HudTheme.Roles.Ring, scrim.transform, HudTheme.RimTop,
                           HudSprites.RingGraded(HudTheme.CircleD, HudTheme.RimW, HudTheme.RimBot.a / HudTheme.RimTop.a));
        Stretch((RectTransform)ring.transform);

        // 키캡 잉크는 아이콘 글리프와 같다: **어두운 색** + 밝은 헤일로. 잉크를 Film 불투명도에 기대게 두면
        // 유리 방향(Film α↓)에서 그대로 무너진다 — 보장을 판이 아니라 잉크가 들게 한다(Halo 주석의 산술).
        _keycapText = MkText(RootName + HudTheme.Roles.Keycap, disc.transform, HudTheme.KeycapPx, HudTheme.GlyphDark, TextAnchor.MiddleCenter);
        _keycapText.text = keycap;
        Stretch((RectTransform)_keycapText.transform);
        Halo(_keycapText);

        // ── 라벨 알약: Scrim 위의 TextHi. 근거리에서만 켠다 ──
        var pill = MkImage(RootName + "__pill", row.transform, HudTheme.Scrim, HudSprites.RoundedRect(HudTheme.Radius));
        pill.type = Image.Type.Sliced;
        _pill = pill.transform;
        var pillVlg = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
        pillVlg.padding = new RectOffset(HudTheme.Space3, HudTheme.Space3, HudTheme.Space2, HudTheme.Space2);
        pillVlg.childControlWidth = true; pillVlg.childControlHeight = true;
        pillVlg.childForceExpandWidth = false; pillVlg.childForceExpandHeight = false;
        var pillFit = pill.gameObject.AddComponent<ContentSizeFitter>();
        pillFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        pillFit.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        _labelText = MkText(RootName + HudTheme.Roles.Label, pill.transform, HudTheme.FontFoot, HudTheme.TextHi, TextAnchor.MiddleCenter);
        _labelText.text = label ?? string.Empty;
        _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;


        SetLabel(string.IsNullOrEmpty(label) ? false : true);
    }

    /// <summary>라벨(예: `앉기`) 텍스트를 바꾼다. 빈 문자열이면 알약을 끈다.</summary>
    public void SetText(string keycap, string label)
    {
        if (_keycapText != null && keycap != null) _keycapText.text = keycap;
        if (_labelText != null && label != null) _labelText.text = label;
    }

    private void SetLabel(bool on) { if (_pill != null && _pill.gameObject.activeSelf != on) _pill.gameObject.SetActive(on); }

    /// <summary>
    /// 각크기 고정 + 빌보드 + 근거리 라벨 게이팅. LateUpdate에서 하는 이유는 카메라가 이동을 끝낸 뒤의
    /// 거리를 써야 각크기가 한 프레임 늦게 흔들리지 않기 때문이다.
    /// </summary>
    private void LateUpdate() => Tick(Cam());

    /// <summary>
    /// 한 프레임 분의 각크기 고정을 수행한다. **public인 이유는 게이트가 3거리(1/3/8m)에서 이걸 직접 불러
    /// 각크기 불변을 단정하기 때문이다** — 플레이 중 실카메라를 강제로 옮기지 않고 배지를 옮겨서 잰다(U10/C6).
    /// </summary>
    public void Tick(Camera cam)
    {
        if (cam == null || _rt == null) return;
        var camT = cam.transform;

        float dist = Vector3.Distance(camT.position, transform.position);
        MeasuredDistanceM = dist;

        // 목표 각크기 targetDeg를 유지하는 월드 지름
        float worldD = 2f * dist * Mathf.Tan(0.5f * HudTheme.BadgeTargetDeg * Mathf.Deg2Rad);
        float k = HudTheme.BadgeBaseDiameterM > 0f ? worldD / HudTheme.BadgeBaseDiameterM : 1f;
        transform.localScale = Vector3.one * (_baseScale * k);

        // 실제로 몇 도를 차지하는지 되짚어 기록한다(게이트가 이 값을 3거리에서 비교한다)
        float actualWorldD = HudTheme.CircleD * transform.lossyScale.x;
        MeasuredDeg = dist > 0.0001f ? 2f * Mathf.Atan2(0.5f * actualWorldD, dist) * Mathf.Rad2Deg : 0f;

        transform.rotation = Quaternion.LookRotation(transform.position - camT.position);   // 빌보드

        SetLabel(_labelText != null && !string.IsNullOrEmpty(_labelText.text) && dist <= HudTheme.BadgeLabelDistanceM);
    }

    // ── 토큰만 쓰는 작은 빌더들 (리터럴 색/px 0) ─────────────────────────────────────────
    private static Image MkImage(string name, Transform parent, Color tint, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = tint; img.sprite = sprite; img.type = Image.Type.Simple;
        img.raycastTarget = false;                       // 프롬프트는 클릭 대상이 아니다
        return img;
    }

    private Text MkText(string name, Transform parent, int fontPx, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontPx;
        txt.fontStyle = FontStyle.Normal;                // faux-bold 금지: 강조는 웨이트/색으로만
        txt.color = color;
        txt.alignment = anchor;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    /// <summary>
    /// 밝은 헤일로. 반투명 판 위의 글자는 배경이 무엇이냐에 따라 대비가 흔들리지만, 글자 둘레가 항상
    /// 같은 색이면 그 둘레를 배경 삼아 읽힌다. U7이 이 조건(불투명 + 두께)을 따로 단정한다.
    ///
    /// ⛔ 색은 <see cref="HudTheme.TextOutline"/>이 아니라 <see cref="HudTheme.TextHi"/>다 — 이 함수가 감싸는
    /// 잉크(<see cref="HudTheme.GlyphDark"/>)와 TextOutline은 **같은 #0A0D12**라 자기대비 1.00:1, 보강이 0이다.
    /// (CrossPlatformRoomHud.EnsureGlyphHalo와 같은 판정. 두 곳이 같은 기계를 쓴다.)
    ///
    /// 왜 지금 필요한가 — Film α를 .60에서 .30으로 낮추면(유리 방향) 키캡이 게이트 아래로 떨어진다.
    /// 실측(Scrim .78 위 Film, 최악 환경):
    ///     Film .60 → 어두운 키캡 7.17:1 PASS  (헤일로 없이 버텼다)
    ///     Film .30 → 어두운 키캡 **2.55:1 FAIL** (검은 환경) / 5.45:1 (흰 환경)
    ///     Film .30 + TextHi 헤일로 → 자기 헤일로 17.67:1 PASS, 헤일로 대 원판 6.93:1(보인다)
    /// 즉 아이콘 글리프가 밟은 것과 **같은 벼랑**이고, 같은 해법을 쓴다. 2px / KeycapPx 48 = 1/24 —
    /// v6가 버린 아웃라인(2px / 16px 한글 = 1/8, 획이 서로 먹었다)과는 상대 두께가 다른 사안이지만,
    /// 그래도 **미감 판정은 사람 몫**이다: U8 캡처를 보고 믿어라.
    /// </summary>
    private static void Halo(Text t)
    {
        var o = t.gameObject.GetComponent<UnityEngine.UI.Outline>() ?? t.gameObject.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = HudTheme.TextHi;
        o.effectDistance = new Vector2(HudTheme.OutlineW, HudTheme.OutlineW);
        o.useGraphicAlpha = false;      // 헤일로가 글자 알파를 물려받으면 같이 사라진다
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Camera Cam()
    {
        if (Camera.main != null) return Camera.main;
        foreach (var c in Camera.allCameras) if (c.isActiveAndEnabled) return c;
        return null;
    }
}
