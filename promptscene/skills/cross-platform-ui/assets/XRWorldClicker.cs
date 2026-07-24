using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Interactors;   // NearFarInteractor, IXRRayProvider
using PromptScene.Core;

/// <summary>
/// XR world-click bridge (REUSABLE, room-agnostic input plumbing): makes any IInteraction consumer (Ruler, target
/// clickers, …) usable with an XR controller/hand, not just the desktop mouse. On an XR interactor's SELECT (trigger/
/// pinch) edge, if the interactor is NOT pointing at UI, it casts the interactor's ray into the world and submits it to
/// SimpleClickProvider.SubmitExternalRay — the SAME handler a desktop mouse click fires. So features are unchanged
/// (still subscribe via IInteraction); only the input SOURCE is generalized (mechanism, not policy — contract §4.5).
///
/// Cross-platform coexistence: desktop mouse still works via SimpleClickProvider.Update; this adds the XR path. On a
/// desktop client (no NearFarInteractor present) the loop is a no-op. UI clicks are excluded (TryGetCurrentUIRaycast
/// hit → the TrackedDeviceGraphicRaycaster/XRUIInputModule handles the button, we skip the world click), and
/// SuppressWorldClick (HUD-panel hover) is a second guard inside SubmitExternalRay. Because the SAME Near-Far interactor
/// is shared by controller AND hand (under {Left,Right} Hand/), hand tracking is covered by this code with 0 additions.
///
/// Authored as a scene object under ===== SYSTEMS ===== (input plumbing). Client-only (no interactors on a headless
/// server). Only /cross-platform-ui's XR / cross-platform modes add this; the PC-only mode does not.
/// SSOT: build-studio-room.md §6.
/// </summary>
public class XRWorldClicker : MonoBehaviour
{
    private SimpleClickProvider _click;

    private void Update()
    {
        if (_click == null)
        {
            if (RoomCore.Instance != null && RoomCore.Instance.TryGet<IInteraction>(out var i))
                _click = i as SimpleClickProvider;
            if (_click == null) return;
        }

        foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None))
        {
            if (nf == null || !nf.isActiveAndEnabled) continue;
            if (!nf.logicalSelectState.wasPerformedThisFrame) continue;   // trigger just pressed this frame
            if (nf.TryGetCurrentUIRaycastResult(out RaycastResult _)) continue; // over UI → let the UI raycaster click the button

            var origin = ((IXRRayProvider)nf).GetOrCreateRayOrigin();
            if (origin == null) continue;
            _click.SubmitExternalRay(new Ray(origin.position, origin.forward));
        }
    }
}
