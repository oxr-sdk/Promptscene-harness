using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PromptScene.Core;
using PromptScene.Core.UI;      // HudTheme / HudSprites — the token SSOT (glass v0)

/// <summary>
/// REUSABLE cross-platform World Space HUD binder (input-source independent). Generalizes the Ruler-specific
/// RoomHudBinder into a room-agnostic part: it hardcodes NO feature — it walks <see cref="RoomContentRegistry.Toggleable"/>
/// and renders ONE ON/OFF button per toggleable content. Drop it (with an authored canvas — see below) into ANY room
/// that has a RoomCore and it wires itself from the registry. Placement is /cross-platform-ui's job (or add-component's).
///
/// Procedure/traps SSOT: build-studio-room.md §5 (World Space uGUI + billboard + dynamic OS font + SuppressWorldClick)
/// and §6 (XRI world-click via XRWorldClicker + SubmitExternalRay — the shared Near-Far interactor path). This binder
/// only WIRES pre-authored scene objects at runtime — the studio pattern proven by LeaveButton (a serialized onClick to
/// a hot method resolves to target=null, so a hot script must AddListener at runtime; contract §3b).
///
/// ── DESIGN TOKENS ────────────────────────────────────────────────────────────────────────────────────────────
/// Every colour / size / spacing / weight comes from <see cref="HudTheme"/>. This file authors NO literal colour and
/// NO literal design px, and it adds NO serialized field (contract §3b: a hot view has zero serialized fields — the
/// tokens live in code, so a hot recompile is all it takes to restyle).
/// The ACTIVE-state indicator is the accent bar and ONLY the accent bar: <see cref="HudTheme.Accent"/> appears on the
/// per-row `…__bar` Image when the feature is enabled and is alpha-0 otherwise. Nothing else in the HUD may be accent
/// coloured (verify U7 "accent = one meaning"). Emphasis on the label is COLOUR (TextHi vs TextLo), never faux-bold.
///
/// Authored structure this binder expects (created &amp; SAVED in the scene by /cross-platform-ui, editable in-editor):
///   RoomHud (Canvas WorldSpace + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster for XR] + this)
///     └── Panel (Hairline) ── PanelFill (PanelTint) / PanelEdge (HairlineLit) / Content (VerticalLayoutGroup)
///           ├── TitleCard (Card) → Title (Text)
///           ├── Buttons   (VerticalLayoutGroup) → ButtonTemplate (INACTIVE) → RowFill (Card) → Bar + Label
///           ├── CountCard (Card) → Count (Text)   — hidden unless a Ruler is present
///           └── HintCard  (Card) → Hint  (Text)
///
/// Cross-platform: the authored canvas carries BOTH GraphicRaycaster (desktop mouse via InputSystemUIInputModule) AND
/// (in XR / cross-platform mode) TrackedDeviceGraphicRaycaster (XR ray/poke via XRUIInputModule). The XR SELECT that
/// clicks a button, and the XR SELECT that measures the floor, both flow through the SAME Near-Far interactor shared by
/// controller and hand — so hand tracking is covered by the same code (verified structurally; live proof = mouse + XR
/// Interaction Simulator CONTROLLER; real-device hand/XREAL/tablet/Vision = V2, see the skill's honesty contract).
///
/// Runtime-only bits (cannot be authored/serialized): the World Space eventCamera (assigned to the active camera each
/// frame), the Korean font, the procedural rounded-corner sprites (HideAndDontSave → they do not survive a scene save,
/// so they are re-applied here every Start), the per-content onClick bindings, and the SuppressWorldClick pointer
/// enter/exit claim. Client-only — a headless/batch server skips the whole HUD.
/// </summary>
public class CrossPlatformRoomHud : MonoBehaviour
{
    // Runtime lookup key of the pilot measuring feature. Keyed by id, NOT by compile-time type, so this part carries no
    // dependency on any feature and stays portable to a room/project that has no Ruler (contract §5 / build-studio-room §5:
    // "Ruler 전용 측정 지우기는 GetById(\"ruler\") 런타임 조회로만 — 없는 룸엔 미표시").
    private const string ClearableId = "ruler";
    private const string ClearMethod = "ClearAll";
    private const string MeasurementTypeName = "RulerMeasurementView";

    /// <summary>Suffix that marks the ONE object allowed to carry <see cref="HudTheme.Accent"/> (verify U7).</summary>
    public const string AccentBarSuffix = "__bar";

    private Canvas _canvas;
    private bool _worldSpace;       // billboard + eventCamera only apply to a World Space canvas (Screen Space Overlay skips them)
    private Font _font;
    private RoomContentRegistry _reg;
    private Transform _buttons;
    private GameObject _template;
    private Text _title, _count, _hint;
    private GameObject _countCard;
    private bool _wired;

    /// <summary>
    /// True when no PyeojinGothic Font asset was found and the HUD fell back to a dynamic OS font.
    /// Verify records this as a WARN, never a FAIL: bundling the font (and building the real 400/600 weight pair) is a
    /// baked-base job outside this skill. Under fallback there is exactly ONE weight available, so the theme's
    /// WeightBody/WeightEmph pair collapses to regular and emphasis is carried by colour alone.
    /// </summary>
    public bool FontFallback { get; private set; }

    /// <summary>Weight actually realised for body/emphasis text. Under font fallback both are <see cref="HudTheme.WeightBody"/>.</summary>
    public int RealisedEmphWeight => FontFallback ? HudTheme.WeightBody : HudTheme.WeightEmph;

    // one row per toggleable content id
    private readonly Dictionary<string, Text> _labels = new Dictionary<string, Text>();
    private readonly Dictionary<string, Image> _bars = new Dictionary<string, Image>();
    private readonly Dictionary<string, IToggleableContent> _content = new Dictionary<string, IToggleableContent>();

    // reflection cache for the optional Ruler "clear" affordance (no compile-time dependency on RulerContent)
    private Type _measurementType;
    private MethodInfo _clearMethod;

    private void Start()
    {
        if (Application.isBatchMode) { enabled = false; return; }   // headless server: no HUD
        _canvas     = GetComponent<Canvas>();
        _worldSpace = _canvas != null && _canvas.renderMode == RenderMode.WorldSpace;
        _font       = FindFont();
        _title    = FindDeep("Title")?.GetComponent<Text>();
        _count    = FindDeep("Count")?.GetComponent<Text>();
        _hint     = FindDeep("Hint")?.GetComponent<Text>();
        _buttons  = FindDeep("Buttons");
        _template = FindDeep("ButtonTemplate")?.gameObject;
        _countCard = FindDeep("CountCard")?.gameObject;
        if (_template != null) _template.SetActive(false);         // template stays hidden; instances are cloned from it

        _measurementType = FindType(MeasurementTypeName);

        ApplyFont();
        ApplyRoundedSprites();          // procedural sprites do not serialize — re-apply so the runtime has the radius
        if (_title != null) _title.text = "PromptScene — 도구";
        if (_hint  != null) _hint.text  = "도구 ON → 포인팅/클릭으로 사용하고 다른 참가자와 공유됩니다.";
        // no Ruler → hide the whole Card, not just the Text (an empty card would still eat a layout row)
        if (_countCard != null) _countCard.SetActive(false);
        else if (_count != null) _count.gameObject.SetActive(false);

        // SuppressWorldClick while a pointer/interactor is over the panel (mouse AND XR fire PointerEnter/Exit),
        // so a button press does not also leak through as a floor world-click. Attach the trigger to the PANEL (the
        // object that actually carries the background Image / raycast target) — the root Canvas has no graphic when the
        // panel is a child (required so a Screen Space Overlay canvas isn't itself a full-screen background).
        var panel = FindDeep("Panel")?.gameObject ?? gameObject;
        var trigger = panel.GetComponent<EventTrigger>() ?? panel.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => SimpleClickProvider.SetWorldClickSuppressed(this, true));
        AddTrigger(trigger, EventTriggerType.PointerExit,  () => SimpleClickProvider.SetWorldClickSuppressed(this, false));

        if (FontFallback)
            Debug.LogWarning("[CrossPlatformRoomHud] WARN font fallback: no PyeojinGothic Font asset — using a dynamic OS font. " +
                             "Only one weight is available, so HudTheme.WeightEmph(" + HudTheme.WeightEmph + ") renders as " +
                             HudTheme.WeightBody + " and emphasis is colour-only. Bundling the 400/600 pair is a baked-base item.");
    }

    private void OnDisable()
    {
        SimpleClickProvider.SetWorldClickSuppressed(this, false);
        if (_reg != null)
        {
            _reg.OnContentToggled     -= OnToggled;
            _reg.OnContentRegistered  -= OnRegistered;
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

            foreach (var c in _reg.Toggleable.ToList()) AddRow(c);   // one ON/OFF button per registered toggleable
            AddClearRowIfPresent();                                  // optional Ruler-only "측정 지우기"

            _reg.OnContentToggled    += OnToggled;
            _reg.OnContentRegistered += OnRegistered;                // features that self-register a frame late get a row too
            RefreshCount();
            _wired = true;
        }
    }

    // Billboard: face the canvas FRONT at the active camera every frame — a World Space GraphicRaycaster ignores
    // reversed (back-facing) graphics by default, so a fixed rotation that turned the back to the camera made the panel
    // both mirrored AND unclickable. Facing the camera fixes readability and clickability at once (build-studio-room §5).
    private void LateUpdate()
    {
        if (!_worldSpace) return;   // a Screen Space Overlay canvas is screen-locked; do not billboard it
        var cam = Cam();
        if (cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }

    // ─── registry-driven rows ────────────────────────────────────────────
    private void AddRow(IToggleableContent c)
    {
        if (c == null || _template == null || _buttons == null) return;
        if (_labels.ContainsKey(c.Id)) return;

        var go = CloneRow("Btn_" + c.Id);
        var btn   = go.GetComponent<Button>();
        var label = go.GetComponentInChildren<Text>(true);

        _content[c.Id] = c;
        _labels[c.Id]  = label;
        _bars[c.Id]    = FindBar(go);

        string id = c.Id;
        if (btn != null) btn.onClick.AddListener(() =>
        {
            if (_content.TryGetValue(id, out var content) && content != null)
            {
                content.SetEnabled(!content.IsEnabled);
                RefreshRow(id);
                RefreshCount();
            }
        });
        RefreshRow(id);
    }

    private void AddClearRowIfPresent()
    {
        var ruler = _reg.GetById(ClearableId);
        if (ruler == null || _template == null || _buttons == null) return;
        _clearMethod = ruler.GetType().GetMethod(ClearMethod, BindingFlags.Instance | BindingFlags.Public);
        if (_clearMethod == null) return;   // present but no ClearAll() — skip rather than guess

        var go = CloneRow("Btn_clear");
        var btn   = go.GetComponent<Button>();
        var label = go.GetComponentInChildren<Text>(true);
        if (label != null) { label.text = "측정 지우기"; label.color = HudTheme.TextLo; }

        // "측정 지우기" is DESTRUCTIVE, so it never wears the accent — it stays a ghost button: the Hairline rim it
        // inherits from the template, a TextLo label, and an alpha-0 bar. (Accent means exactly one thing: "active".)
        var bar = FindBar(go);
        if (bar != null) { var t = HudTheme.Accent; t.a = 0f; bar.color = t; }

        if (_countCard != null) _countCard.SetActive(true);          // a Ruler exists → surface the count line
        else if (_count != null) _count.gameObject.SetActive(true);

        if (btn != null) btn.onClick.AddListener(() =>
        {
            var r = _reg.GetById(ClearableId);
            if (r != null) { try { _clearMethod.Invoke(r, null); } catch (Exception e) { Debug.LogWarning("[CrossPlatformRoomHud] clear failed: " + e.Message); } }
            RefreshCount();
        });
    }

    /// <summary>Clone the authored template row and give its accent bar the U7-checkable `…__bar` name.</summary>
    private GameObject CloneRow(string rowName)
    {
        var go = Instantiate(_template, _buttons);
        go.name = rowName;
        go.SetActive(true);
        var bar = FindDeepIn(go.transform, "Bar");
        if (bar != null) bar.name = rowName + AccentBarSuffix;
        var label = go.GetComponentInChildren<Text>(true);
        if (_font != null && label != null) label.font = _font;
        ApplyRoundedSprites(go.transform);
        return go;
    }

    private Image FindBar(GameObject row)
    {
        var t = FindDeepIn(row.transform, row.name + AccentBarSuffix) ?? FindDeepIn(row.transform, "Bar");
        return t != null ? t.GetComponent<Image>() : null;
    }

    private void OnRegistered(IRoomContent c)
    {
        if (c is IToggleableContent t) AddRow(t);
        if (c.Id == ClearableId) AddClearRowIfPresent();
    }

    private void OnToggled(IToggleableContent c, bool on)
    {
        if (c != null) RefreshRow(c.Id);
        RefreshCount();
    }

    /// <summary>
    /// Active state is shown by the accent bar (alpha 0 ↔ <see cref="HudTheme.Accent"/>) plus label brightness
    /// (TextLo ↔ TextHi). Size never changes between states and faux-bold is never used — the theme forbids both.
    /// </summary>
    private void RefreshRow(string id)
    {
        if (!_content.TryGetValue(id, out var c) || c == null) return;
        bool on = c.IsEnabled;

        if (_labels.TryGetValue(id, out var label) && label != null)
        {
            string name = string.IsNullOrEmpty(c.Meta.DisplayName) ? c.Id : c.Meta.DisplayName;
            label.text  = $"{name} : {(on ? "ON" : "OFF")}";
            label.color = on ? HudTheme.TextHi : HudTheme.TextLo;
            label.fontSize  = HudTheme.FontSm;
            label.fontStyle = FontStyle.Normal;      // never faux-bold: emphasis is weight (600) or colour, not Bold
        }

        if (_bars.TryGetValue(id, out var bar) && bar != null)
        {
            var col = HudTheme.Accent;               // the ONE accent meaning: this feature is active
            col.a = on ? HudTheme.Accent.a : 0f;
            bar.color = col;
        }
    }

    private void RefreshCount()
    {
        if (_count == null || !_count.gameObject.activeInHierarchy || _measurementType == null) return;
        int shared = UnityEngine.Object.FindObjectsByType(_measurementType, FindObjectsSortMode.None).Length;
        _count.text = $"공유 측정: {shared} 개";
    }

    // ─── helpers ─────────────────────────────────────────────────────────
    private Transform FindDeep(string childName) => FindDeepIn(transform, childName);

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
    /// Re-apply the procedural rounded-rect sprites. They are created with HideFlags.HideAndDontSave (deliberately —
    /// it keeps the radius a token instead of an asset, so no Addressables/baked-base question ever arises), which
    /// means a saved scene loses the reference. Re-applying at Start guarantees the RUNTIME — the thing the QuickTest
    /// verifies and the human looks at — always matches the theme's radius.
    /// </summary>
    private void ApplyRoundedSprites() => ApplyRoundedSprites(transform);

    private static void ApplyRoundedSprites(Transform root)
    {
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "PanelEdge") continue;                       // the 1px specular strip stays square
            int radius = img.name.EndsWith(AccentBarSuffix) || img.name == "Bar"
                ? HudTheme.BarW
                : HudTheme.Radius;
            img.sprite = HudSprites.RoundedRect(radius);
            img.type   = Image.Type.Sliced;
        }
    }

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
    /// dynamic OS font (build-studio-room §5: studio has no Korean TMP/font asset, and the legacy-Text + OS-font path is
    /// the one proven to render Korean). The fallback is a WARN, not a failure — see <see cref="FontFallback"/>.
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
                HudTheme.FontSm);
            if (f != null) return f;
        }
        catch { }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
