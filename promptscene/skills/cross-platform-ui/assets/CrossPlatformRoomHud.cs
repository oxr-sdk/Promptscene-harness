using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PromptScene.Core;
using PromptScene.Core.UI;      // HudTheme / HudSprites / HudIcons — the token SSOT (glass v6)

/// <summary>
/// REUSABLE cross-platform World Space HUD binder (input-source independent). Hardcodes NO feature — it walks
/// <see cref="RoomContentRegistry.Toggleable"/> and renders ONE circular icon button per toggleable content, four per
/// page, with drag/wheel paging. Drop it (with an authored canvas) into ANY room that has a RoomCore and it wires
/// itself from the registry. Placement is /cross-platform-ui's job.
///
/// Procedure/traps SSOT: build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS font + SuppressWorldClick)
/// and §6 (XRI world-click via XRWorldClicker + SubmitExternalRay). This binder only WIRES pre-authored scene objects
/// at runtime — a serialized onClick to a hot method resolves to target=null, so a hot script must AddListener at
/// runtime (contract §3b).
///
/// ── glass v6: 제목도 문구도 없다. 원 4개/페이지 + 넘길 때만 보이는 점. ───────────────────────────────────────
/// 상태를 **글자로 말하지 않는다**: ON은 원의 Accent 채움 + 라벨 강조로만 말한다(`": ON"` 0건).
/// 글리프는 OFF/ON **양쪽 모두 어둡다**(<see cref="HudTheme.GlyphDark"/>) — 그게 성립하려면 Film이 충분히
/// 불투명해야 하고, 그 알파는 대비 산술이 정했다(HudTheme 헤더 참고). 라벨은 불투명 아웃라인으로 대비를 얻는다.
/// 파괴적 액션("측정 지우기")도 같은 원형 버튼이지만 **액센트를 절대 입지 않는다**.
///
/// ── DESIGN TOKENS ────────────────────────────────────────────────────────────────────────────────────────────
/// Every colour / size / spacing / weight comes from <see cref="HudTheme"/>. This file authors NO literal colour and
/// NO literal design px, and it adds NO serialized field (contract §3b).
///
/// Authored structure this binder expects (created &amp; SAVED in the scene by /cross-platform-ui):
///   RoomHud (Canvas WorldSpace + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster] + this)
///     └── Panel (Image = Scrim) ── PanelFrame / PanelEdge / TopSpacer / Viewport(RectMask2D + HudPager) / Dots
///           Viewport └ Track └ PageTemplate (INACTIVE) ; IconButtonTemplate (INACTIVE) ; DotTemplate (INACTIVE)
///           IconButtonTemplate └ …__disc (Film) └ …__glyph / …__icon ; …__ring ; …__label
///
/// Runtime-only bits (cannot be authored/serialized): the World Space eventCamera, the Korean + icon fonts, the
/// procedural circle/ring/frame sprites (HideAndDontSave → they do not survive a scene save, so they are re-applied
/// every Start), the per-content onClick + hover bindings, the pager wiring, and the SuppressWorldClick claim.
/// Client-only — a headless/batch server skips the whole HUD.
/// </summary>
public class CrossPlatformRoomHud : MonoBehaviour
{
    private const string ClearableId = "ruler";
    private const string ClearActionId = "clear";
    private const string ClearMethod = "ClearAll";
    private const string MeasurementTypeName = "RulerMeasurementView";

    /// <summary>An entry in the grid: a toggleable feature, or a non-toggle action button.</summary>
    private struct Entry
    {
        public string Id, Display;
        public Sprite Icon;
        public IToggleableContent Content;   // null for an action
        public bool IsAction;
    }

    private Canvas _canvas;
    private bool _worldSpace;
    private Font _font;
    private RoomContentRegistry _reg;
    private RectTransform _viewport, _track, _dots;
    private CanvasGroup _dotsGroup;
    private GameObject _pageTemplate, _cellTemplate, _dotTemplate;
    private HudPager _pager;
    private bool _wired;

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly Dictionary<string, Text>  _labels = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> _discs  = new Dictionary<string, Image>();
    private readonly Dictionary<string, Text>  _glyphs = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> _rings  = new Dictionary<string, Image>();
    private readonly List<Image> _dotImages = new List<Image>();
    private readonly HashSet<string> _hovered = new HashSet<string>();

    private Type _measurementType;
    private MethodInfo _clearMethod;

    /// <summary>True when no PyeojinGothic Font asset was found and the HUD fell back to a dynamic OS font (WARN, not FAIL).</summary>
    public bool FontFallback { get; private set; }
    public int RealisedEmphWeight => FontFallback ? HudTheme.WeightBody : HudTheme.WeightEmph;
    public bool IconFontLoaded { get; private set; }
    public int PageCount => _pager != null ? _pager.Pages : 0;

    /// <summary>Which fallback tier each entry resolved to — U11 reads this to prove the chain is deterministic.</summary>
    public readonly Dictionary<string, HudIconTier> IconTiers = new Dictionary<string, HudIconTier>();

    /// <summary>
    /// ⛔ STOP-AND-REPORT list: an id whose codepoint mapping exists but is NOT in the font atlas.
    /// Never silently swallowed into the letter fallback — U11 FAILs on a non-empty list.
    /// </summary>
    public readonly List<string> IconErrors = new List<string>();

    private void Start()
    {
        if (Application.isBatchMode) { enabled = false; return; }   // headless server: no HUD
        _canvas     = GetComponent<Canvas>();
        _worldSpace = _canvas != null && _canvas.renderMode == RenderMode.WorldSpace;
        _font       = FindFont();
        IconFontLoaded = HudIcons.Font != null;

        _viewport     = FindDeep("Viewport") as RectTransform;
        _track        = FindDeep("Track") as RectTransform;
        _dots         = FindDeep("Dots") as RectTransform;
        _pageTemplate = FindDeep("PageTemplate")?.gameObject;
        _cellTemplate = FindDeep("IconButtonTemplate")?.gameObject;
        _dotTemplate  = FindDeep("DotTemplate")?.gameObject;
        if (_pageTemplate != null) _pageTemplate.SetActive(false);
        if (_cellTemplate != null) _cellTemplate.SetActive(false);
        if (_dotTemplate  != null) _dotTemplate.SetActive(false);
        if (_dots != null) _dotsGroup = _dots.GetComponent<CanvasGroup>() ?? _dots.gameObject.AddComponent<CanvasGroup>();
        if (_viewport != null) _pager = _viewport.GetComponent<HudPager>() ?? _viewport.gameObject.AddComponent<HudPager>();

        _measurementType = FindType(MeasurementTypeName);

        ApplyFont();
        ApplySprites();                 // procedural sprites do not serialize — re-apply so the runtime has the shapes

        // SuppressWorldClick while a pointer/interactor is over the panel (mouse AND XR fire PointerEnter/Exit), so a
        // button press does not also leak through as a floor world-click. The trigger goes on the PANEL (the object
        // carrying the background Image / raycast target) — the root Canvas has no graphic.
        var panel = FindDeep("Panel")?.gameObject ?? gameObject;
        var trigger = panel.GetComponent<EventTrigger>() ?? panel.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => SimpleClickProvider.SetWorldClickSuppressed(this, true));
        AddTrigger(trigger, EventTriggerType.PointerExit,  () => SimpleClickProvider.SetWorldClickSuppressed(this, false));

        if (FontFallback)
            Debug.LogWarning("[CrossPlatformRoomHud] WARN font fallback: no PyeojinGothic Font asset — using a dynamic OS font. " +
                             "Only one weight is available, so HudTheme.WeightEmph(" + HudTheme.WeightEmph + ") renders as " +
                             HudTheme.WeightBody + " and emphasis is colour-only. Bundling the 400/600 pair is a baked-base item.");
        if (!IconFontLoaded)
            Debug.LogWarning("[CrossPlatformRoomHud] WARN icon font missing (Resources/" + HudIcons.FontResourcePath +
                             ") — every icon falls back to the first letter of DisplayName (tier ③).");
    }

    private void OnDisable()
    {
        SimpleClickProvider.SetWorldClickSuppressed(this, false);
        if (_reg != null)
        {
            _reg.OnContentToggled    -= OnToggled;
            _reg.OnContentRegistered -= OnRegistered;
        }
    }

    private void Update()
    {
        if (_worldSpace && _canvas != null && _canvas.worldCamera == null)  // Screen Space Overlay needs no eventCamera
        {
            var cam = Cam();
            if (cam != null) _canvas.worldCamera = cam;
        }

        if (!_wired && RoomCore.Instance != null)
        {
            _reg = RoomCore.Instance.Contents;
            Rebuild();
            _reg.OnContentToggled    += OnToggled;
            _reg.OnContentRegistered += OnRegistered;   // features that self-register a frame late get a button too
            _wired = true;
        }

        // 점은 레이아웃을 차지한 채 alpha만 바뀐다 — 나타날 때 패널이 흔들리지 않게(v6 규칙).
        if (_dotsGroup != null && _pager != null)
        {
            float want = (_pager.Pages > 1 && _pager.DotsVisible) ? 1f : 0f;
            _dotsGroup.alpha = Mathf.MoveTowards(_dotsGroup.alpha, want, Time.unscaledDeltaTime / 0.18f);
        }
    }

    // Billboard: face the canvas FRONT at the active camera every frame — a World Space GraphicRaycaster ignores
    // reversed (back-facing) graphics, so a fixed rotation that turned the back to the camera made the panel both
    // mirrored AND unclickable (build-studio-room §5).
    private void LateUpdate()
    {
        if (!_worldSpace) return;
        var cam = Cam();
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    // ─── registry-driven build ───────────────────────────────────────────
    private void CollectEntries()
    {
        _entries.Clear();
        foreach (var c in _reg.Toggleable.ToList())
            _entries.Add(new Entry { Id = c.Id, Display = c.Meta.DisplayName, Icon = c.Meta.Icon, Content = c });

        // Ruler-only destructive action, resolved by runtime lookup so a Ruler-less room simply doesn't show it.
        var ruler = _reg.GetById(ClearableId);
        if (ruler != null)
        {
            _clearMethod = ruler.GetType().GetMethod(ClearMethod, BindingFlags.Instance | BindingFlags.Public);
            if (_clearMethod != null)
                _entries.Add(new Entry { Id = ClearActionId, Display = "측정 지우기", IsAction = true });
        }
    }

    private void Rebuild()
    {
        if (_track == null || _cellTemplate == null || _pageTemplate == null) return;
        CollectEntries();

        foreach (Transform child in _track) if (child.gameObject.activeSelf) Destroy(child.gameObject);
        if (_dots != null) foreach (Transform child in _dots) if (child.gameObject.activeSelf) Destroy(child.gameObject);
        _labels.Clear(); _discs.Clear(); _glyphs.Clear(); _rings.Clear(); _dotImages.Clear(); IconTiers.Clear(); IconErrors.Clear();

        int pages = Mathf.Max(1, Mathf.CeilToInt(_entries.Count / (float)HudTheme.PageSize));
        for (int p = 0; p < pages; p++)
        {
            var page = Instantiate(_pageTemplate, _track);
            page.name = "Page_" + p;
            page.SetActive(true);
            for (int i = p * HudTheme.PageSize; i < Mathf.Min(_entries.Count, (p + 1) * HudTheme.PageSize); i++)
                BuildCell(_entries[i], page.transform);
        }

        if (_dots != null && _dotTemplate != null)
            for (int p = 0; p < pages; p++)
            {
                var dot = Instantiate(_dotTemplate, _dots);
                dot.name = "Page" + p + HudTheme.Roles.Dot;
                dot.SetActive(true);
                var img = dot.GetComponent<Image>();
                if (img != null) { img.color = p == 0 ? HudTheme.DotOn : HudTheme.Dot; _dotImages.Add(img); }
            }

        if (_pager != null && _viewport != null)
            _pager.Configure(_viewport, _track, pages, _viewport.rect.width, OnPageChanged);

        RefreshAll();
    }

    private void OnPageChanged(int page)
    {
        for (int i = 0; i < _dotImages.Count; i++)
            if (_dotImages[i] != null) _dotImages[i].color = i == page ? HudTheme.DotOn : HudTheme.Dot;
    }

    private void BuildCell(Entry e, Transform page)
    {
        string row = (e.IsAction ? "Act_" : "Btn_") + e.Id;
        var go = Instantiate(_cellTemplate, page);
        go.name = row;
        // 템플릿 부품은 이미 역할 접미사로 authoring 돼 있다(`Tmpl__disc` …) → 접미사로 찾아 행 이름만 갈아끼운다.
        foreach (var suffix in new[] { HudTheme.Roles.Disc, HudTheme.Roles.Ring, HudTheme.Roles.Glyph,
                                       HudTheme.Roles.Icon, HudTheme.Roles.Label })
        {
            var t = FindDeepBySuffix(go.transform, suffix);
            if (t != null) t.name = row + suffix;
        }
        go.SetActive(true);
        if (_font != null) foreach (var t in go.GetComponentsInChildren<Text>(true)) t.font = _font;
        ApplySprites(go.transform);

        var btn   = go.GetComponent<Button>();
        var disc  = Part<Image>(go, row + HudTheme.Roles.Disc);
        var ring  = Part<Image>(go, row + HudTheme.Roles.Ring);
        var glyph = Part<Text>(go,  row + HudTheme.Roles.Glyph);
        var icon  = Part<Image>(go, row + HudTheme.Roles.Icon);
        var label = Part<Text>(go,  row + HudTheme.Roles.Label);

        _discs[e.Id] = disc; _rings[e.Id] = ring; _glyphs[e.Id] = glyph; _labels[e.Id] = label;
        if (btn != null && disc != null) btn.targetGraphic = disc;
        if (label != null) label.text = string.IsNullOrEmpty(e.Display) ? e.Id : e.Display;

        // ⭐ ContentMeta.Icon finally gets a consumer (contract change: zero — the field was always there).
        ApplyIcon(e, glyph, icon);

        string id = e.Id;
        bool isAction = e.IsAction;
        if (btn != null) btn.onClick.AddListener(() =>
        {
            if (_pager != null && _pager.ConsumedDrag) return;     // 드래그의 꼬리로 들어온 클릭은 버린다
            if (isAction) InvokeClear();
            else
            {
                var entry = _entries.FirstOrDefault(x => x.Id == id);
                if (entry.Content != null) { entry.Content.SetEnabled(!entry.Content.IsEnabled); RefreshCell(id); }
            }
        });

        // hover = 레이 조준 피드백. 장식이 아니라 "지금 이걸 겨누고 있다"는 유일한 신호다(마우스·XR 공통).
        var trig = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
        AddTrigger(trig, EventTriggerType.PointerEnter, () => { _hovered.Add(id); RefreshCell(id); });
        AddTrigger(trig, EventTriggerType.PointerExit,  () => { _hovered.Remove(id); RefreshCell(id); });

        RefreshCell(id);
    }

    private void ApplyIcon(Entry e, Text glyph, Image icon)
    {
        var table = e.IsAction ? HudIcons.ByActionId : HudIcons.ByContentId;
        var pick = HudIcons.Resolve(e.Icon, e.Display, e.Id, table);
        IconTiers[e.Id] = pick.Tier;

        if (pick.Error != null)
        {
            IconErrors.Add(e.Id + ": " + pick.Error);
            Debug.LogError("[CrossPlatformRoomHud] ICON " + pick.Error);   // 조용히 넘기지 않는다
        }

        bool useSprite = pick.Tier == HudIconTier.Sprite && pick.Sprite != null;
        if (icon != null)
        {
            icon.gameObject.SetActive(useSprite);
            if (useSprite)
            {
                icon.sprite = pick.Sprite;
                icon.color  = HudTheme.GlyphDark;              // 스프라이트도 같은 잉크로 틴트한다
                icon.preserveAspect = true;
            }
        }
        if (glyph != null)
        {
            glyph.gameObject.SetActive(!useSprite);
            if (!useSprite)
            {
                glyph.text = pick.Text;
                // ② 글리프는 아이콘 폰트로, ③ 첫글자는 본문 폰트로. 폰트를 바꾸는 건 이 한 곳뿐이다.
                if (pick.Tier == HudIconTier.Glyph && HudIcons.Font != null) glyph.font = HudIcons.Font;
                else if (_font != null) glyph.font = _font;
            }
        }
    }

    private void InvokeClear()
    {
        var r = _reg != null ? _reg.GetById(ClearableId) : null;
        if (r == null || _clearMethod == null) return;
        try { _clearMethod.Invoke(r, null); }
        catch (Exception e) { Debug.LogWarning("[CrossPlatformRoomHud] clear failed: " + e.Message); }
    }

    private static T Part<T>(GameObject row, string name) where T : Component
    {
        var t = FindDeepIn(row.transform, name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private void OnRegistered(IRoomContent c)
    {
        if (_wired && _reg != null) Rebuild();       // 페이지 구성이 바뀔 수 있으므로 통째로 다시 만든다
    }

    private void OnToggled(IToggleableContent c, bool on) { if (c != null) RefreshCell(c.Id); }

    private void RefreshAll() { foreach (var e in _entries) RefreshCell(e.Id); }

    /// <summary>
    /// State is told by FILL, not by text: ON = <see cref="HudTheme.Accent"/> disc; OFF = <see cref="HudTheme.Film"/>.
    /// The glyph stays <see cref="HudTheme.GlyphDark"/> in BOTH states (v6) — which is exactly why Film has to be
    /// opaque enough; see the arithmetic in HudTheme's header. There is no `": ON"` string anywhere, and the size
    /// never changes between states.
    /// </summary>
    private void RefreshCell(string id)
    {
        var entry = _entries.FirstOrDefault(x => x.Id == id);
        if (entry.Id == null) return;
        bool on = entry.Content != null && entry.Content.IsEnabled;   // an action is never "on"
        bool hover = _hovered.Contains(id);

        if (_discs.TryGetValue(id, out var disc) && disc != null)
            disc.color = on ? HudTheme.Accent : (hover ? HudTheme.FilmHover : HudTheme.Film);

        if (_rings.TryGetValue(id, out var ring) && ring != null)
            ring.color = HudTheme.RimTop;      // 링은 상태를 말하지 않는다 — 액센트는 채움 한 곳뿐

        if (_glyphs.TryGetValue(id, out var glyph) && glyph != null)
            glyph.color = HudTheme.GlyphDark;

        if (_labels.TryGetValue(id, out var label) && label != null)
        {
            label.color     = on ? HudTheme.TextHi : HudTheme.TextLo;
            label.fontSize  = HudTheme.FontFoot;
            label.fontStyle = FontStyle.Normal;      // never faux-bold: emphasis is weight (600) or colour
        }
    }

    // ─── helpers ─────────────────────────────────────────────────────────
    private Transform FindDeep(string childName) => FindDeepIn(transform, childName);

    /// <summary>역할 접미사로 부품을 찾는다. 이름 앞부분(Tmpl/행 이름)이 무엇이든 역할은 접미사가 말한다.</summary>
    private static Transform FindDeepBySuffix(Transform root, string suffix)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.EndsWith(suffix)) return t;
        return null;
    }

    private static Transform FindDeepIn(Transform root, string childName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    private void ApplyFont()
    {
        if (_font == null) return;
        foreach (var t in GetComponentsInChildren<Text>(true)) t.font = _font;
    }

    /// <summary>
    /// Re-apply the procedural sprites. They are HideFlags.HideAndDontSave (deliberately — it keeps the shapes tokens
    /// instead of assets, so no Addressables/baked-base question arises), which means a saved scene loses the
    /// reference. Re-applying at Start guarantees the RUNTIME always matches the theme.
    /// Shape is chosen by ROLE SUFFIX, so exactly one place knows which part is a circle and which a box.
    /// </summary>
    private void ApplySprites() => ApplySprites(transform);

    private static void ApplySprites(Transform root)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            string n = img.name;
            if (n == "PanelEdge") continue;                                   // the specular strip stays a plain rect
            if (n.EndsWith(HudTheme.Roles.Icon)) continue;                    // Meta.Icon supplies its own sprite

            if (n == "Panel")           Set(img, HudSprites.RoundedRect(HudTheme.Radius), Image.Type.Sliced);
            else if (n == "PanelFrame") Set(img, HudSprites.RoundedFrame(HudTheme.Radius, HudTheme.BorderW), Image.Type.Sliced);
            else if (n.EndsWith(HudTheme.Roles.Dot))
                Set(img, HudSprites.Circle(HudTheme.Space2), Image.Type.Simple);
            else if (n.EndsWith(HudTheme.Roles.Ring))
                // 위 밝고 아래 어두운 2톤 테두리를 **한 장**으로: 색은 RimTop, 스프라이트가 아래로 알파를 깎는다.
                Set(img, HudSprites.RingGraded(HudTheme.CircleD, HudTheme.RimW, HudTheme.RimBot.a / HudTheme.RimTop.a), Image.Type.Simple);
            else if (n.EndsWith(HudTheme.Roles.Disc))
                Set(img, HudSprites.Circle(HudTheme.CircleD), Image.Type.Simple);
        }
    }

    static void Set(Image img, Sprite s, Image.Type t) { img.sprite = s; img.type = t; }

    private static Type FindType(string simpleOrFull)
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType(simpleOrFull);
            if (t != null) return t;
            foreach (var tt in SafeTypes(a)) if (tt.Name == simpleOrFull) return tt;
        }
        return null;
    }
    private static IEnumerable<Type> SafeTypes(Assembly a)
    { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } }

    private static Camera Cam()
    {
        if (Camera.main != null) return Camera.main;
        foreach (var c in Camera.allCameras) if (c.isActiveAndEnabled) return c;
        return null;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    /// <summary>
    /// The theme's font is PyeojinGothic 400/600. studio ships no such asset yet, so we look for one and fall back to a
    /// dynamic OS font (build-studio-room §5). The fallback is a WARN, not a failure — see <see cref="FontFallback"/>.
    /// </summary>
    private Font FindFont()
    {
        foreach (var path in new[] { "Fonts/PyeojinGothic-Regular", "PyeojinGothic-Regular", "Fonts/PyeojinGothic" })
        {
            var bundled = Resources.Load<Font>(path);
            if (bundled != null) { FontFallback = false; return bundled; }
        }
        FontFallback = true;
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "NanumGothic", "Gulim", "Batang", "Arial" },
                HudTheme.FontBody);
            if (f != null) return f;
        }
        catch { }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
