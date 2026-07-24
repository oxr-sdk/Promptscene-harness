using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PromptScene.Core;

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
/// Authored structure this binder expects (created & SAVED in the scene by /cross-platform-ui, editable in-editor):
///   RoomHud (Canvas WorldSpace + CanvasScaler + GraphicRaycaster [+ TrackedDeviceGraphicRaycaster for XR] +
///            Image bg + VerticalLayoutGroup + ContentSizeFitter + this + EventTrigger)
///     ├── Title          (Text)
///     ├── Buttons        (empty container, VerticalLayoutGroup) — runtime rows are cloned in here
///     │     └── ButtonTemplate (Image + Button + LayoutElement, INACTIVE) → Label (Text)
///     ├── Count          (Text)   — optional; shows shared-measurement count only if a Ruler is present
///     └── Hint           (Text)
///
/// Cross-platform: the authored canvas carries BOTH GraphicRaycaster (desktop mouse via InputSystemUIInputModule) AND
/// (in XR / cross-platform mode) TrackedDeviceGraphicRaycaster (XR ray/poke via XRUIInputModule). The XR SELECT that
/// clicks a button, and the XR SELECT that measures the floor, both flow through the SAME Near-Far interactor shared by
/// controller and hand — so hand tracking is covered by the same code (verified structurally; live proof = mouse + XR
/// Interaction Simulator CONTROLLER; real-device hand/XREAL/tablet/Vision = V2, see the skill's honesty contract).
///
/// Runtime-only bits (cannot be authored/serialized): the World Space eventCamera (assigned to the active camera each
/// frame), the dynamic OS Korean font (studio ships no Korean TMP/font asset), the per-content onClick bindings, and the
/// SuppressWorldClick pointer enter/exit claim. Client-only — a headless/batch server skips the whole HUD.
/// </summary>
public class CrossPlatformRoomHud : MonoBehaviour
{
    // Runtime lookup key of the pilot measuring feature. Keyed by id, NOT by compile-time type, so this part carries no
    // dependency on any feature and stays portable to a room/project that has no Ruler (contract §5 / build-studio-room §5:
    // "Ruler 전용 측정 지우기는 GetById(\"ruler\") 런타임 조회로만 — 없는 룸엔 미표시").
    private const string ClearableId = "ruler";
    private const string ClearMethod = "ClearAll";
    private const string MeasurementTypeName = "RulerMeasurementView";

    private Canvas _canvas;
    private bool _worldSpace;       // billboard + eventCamera only apply to a World Space canvas (Screen Space Overlay skips them)
    private Font _font;
    private RoomContentRegistry _reg;
    private Transform _buttons;
    private GameObject _template;
    private Text _title, _count, _hint;
    private bool _wired;

    // one row per toggleable content id
    private readonly Dictionary<string, Text> _labels = new Dictionary<string, Text>();
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
        if (_template != null) _template.SetActive(false);         // template stays hidden; instances are cloned from it

        _measurementType = FindType(MeasurementTypeName);

        ApplyFont();
        if (_title != null) _title.text = "PromptScene — 도구";
        if (_hint  != null) _hint.text  = "도구 ON → 포인팅/클릭으로 사용하고 다른 참가자와 공유됩니다.";
        if (_count != null) _count.gameObject.SetActive(false);    // shown only if a Ruler turns up

        // SuppressWorldClick while a pointer/interactor is over the panel (mouse AND XR fire PointerEnter/Exit),
        // so a button press does not also leak through as a floor world-click. Attach the trigger to the PANEL (the
        // object that actually carries the background Image / raycast target) — the root Canvas has no graphic when the
        // panel is a child (required so a Screen Space Overlay canvas isn't itself a full-screen background).
        var panel = FindDeep("Panel")?.gameObject ?? gameObject;
        var trigger = panel.GetComponent<EventTrigger>() ?? panel.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => SimpleClickProvider.SetWorldClickSuppressed(this, true));
        AddTrigger(trigger, EventTriggerType.PointerExit,  () => SimpleClickProvider.SetWorldClickSuppressed(this, false));
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

        var go = Instantiate(_template, _buttons);
        go.name = "Btn_" + c.Id;
        go.SetActive(true);
        var btn   = go.GetComponent<Button>();
        var label = go.GetComponentInChildren<Text>(true);
        if (_font != null && label != null) label.font = _font;

        _content[c.Id] = c;
        _labels[c.Id]  = label;

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

        var go = Instantiate(_template, _buttons);
        go.name = "Btn_clear";
        go.SetActive(true);
        var btn   = go.GetComponent<Button>();
        var label = go.GetComponentInChildren<Text>(true);
        if (_font != null && label != null) label.font = _font;
        if (label != null) label.text = "측정 지우기";
        if (_count != null) _count.gameObject.SetActive(true);      // a Ruler exists → surface the count line

        if (btn != null) btn.onClick.AddListener(() =>
        {
            var r = _reg.GetById(ClearableId);
            if (r != null) { try { _clearMethod.Invoke(r, null); } catch (Exception e) { Debug.LogWarning("[CrossPlatformRoomHud] clear failed: " + e.Message); } }
            RefreshCount();
        });
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

    private void RefreshRow(string id)
    {
        if (!_labels.TryGetValue(id, out var label) || label == null) return;
        if (!_content.TryGetValue(id, out var c) || c == null) return;
        string name = string.IsNullOrEmpty(c.Meta.DisplayName) ? c.Id : c.Meta.DisplayName;
        label.text = $"{name} : {(c.IsEnabled ? "ON" : "OFF")}";
    }

    private void RefreshCount()
    {
        if (_count == null || !_count.gameObject.activeSelf || _measurementType == null) return;
        int shared = UnityEngine.Object.FindObjectsByType(_measurementType, FindObjectsSortMode.None).Length;
        _count.text = $"공유 측정: {shared} 개";
    }

    // ─── helpers ─────────────────────────────────────────────────────────
    private Transform FindDeep(string childName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == childName) return t;
        return null;
    }

    private void ApplyFont()
    {
        if (_font == null) return;
        foreach (var t in GetComponentsInChildren<Text>(true)) t.font = _font;
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

    private static Font FindFont()
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "NanumGothic", "Gulim", "Batang", "Arial" }, 24);
            if (f != null) return f;
        }
        catch { }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
