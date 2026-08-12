using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 소환 런치패드 — show/hide the room HUD with one key. Sits on the SAME GameObject as
/// <see cref="CrossPlatformRoomHud"/> (the root Canvas) and toggles the Canvas + its raycasters, never the
/// GameObject: disabling the GameObject would disable THIS component too and the HUD could never be summoned back.
///
/// Bindings (chosen from the live input inventory — ui-surface-survey.md §12-B, measured 2026-08-05):
///   Desktop : F1. The ONLY key verified empty in BOTH contexts (shipping room AND studio QuickTest, where the XR
///             Interaction Simulator's device emulation occupies most letters). It also never mixes into text entry,
///             so summoning works while chat has focus. ⛔ M was the earlier pick and is WRONG — the live enumeration
///             found XR Interaction Controller Controls › Menu and XR Interaction Hand Controls › Pinch on it.
///   XR      : LEFT controller secondaryButton (Y). Free in the action asset; `menu` is avoided because the Quest
///             runtime takes it, and the right-hand primaryButton (A) is taken by XRI's JumpProvider.
///             Read through UnityEngine.XR.InputDevices rather than an InputAction so this component needs no
///             action-asset wiring and no serialized fields (contract §3b: serialized refs don't survive the
///             prefab-asset loader).
/// </summary>
[RequireComponent(typeof(Canvas))]
public class HudSummon : MonoBehaviour
{
    [Tooltip("Start with the HUD hidden, so it is summoned rather than always-on. That IS the launchpad behaviour; " +
             "turn it off to keep the old always-visible HUD and use the key only to dismiss it.")]
    [SerializeField] private bool startHidden = true;

    [Tooltip("Desktop summon key. F1 is the measured-empty choice (see class doc).")]
    [SerializeField] private KeyCode desktopKey = KeyCode.F1;

    private Canvas _canvas;
    private Behaviour[] _raycasters;
    private bool _shown;
    private bool _xrHeldLastFrame;

    /// <summary>Current visibility — read by verification and by anything that wants to mirror the state.</summary>
    public bool IsShown => _shown;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        // GraphicRaycaster and (XR modes) TrackedDeviceGraphicRaycaster both derive from Behaviour. Hiding the Canvas
        // alone leaves them raycasting, so an invisible HUD would still swallow clicks/rays over its old footprint.
        _raycasters = GetComponents<UnityEngine.EventSystems.BaseRaycaster>();
        SetShown(!startHidden);
    }

    private void Update()
    {
        if (DesktopPressed() || XrPressed()) Toggle();
    }

    public void Toggle() => SetShown(!_shown);

    public void SetShown(bool on)
    {
        _shown = on;
        if (_canvas != null) _canvas.enabled = on;
        if (_raycasters != null)
            foreach (var r in _raycasters) if (r != null) r.enabled = on;
    }

    private bool DesktopPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && desktopKey == KeyCode.F1) return kb.f1Key.wasPressedThisFrame;
#endif
        try { return Input.GetKeyDown(desktopKey); } catch { return false; }
    }

    /// <summary>Left-hand secondaryButton (Y), edge-detected by hand since InputDevices reports level, not edges.</summary>
    private bool XrPressed()
    {
        var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        bool held = dev.isValid
                    && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool v)
                    && v;
        bool pressed = held && !_xrHeldLastFrame;
        _xrHeldLastFrame = held;
        return pressed;
    }
}
