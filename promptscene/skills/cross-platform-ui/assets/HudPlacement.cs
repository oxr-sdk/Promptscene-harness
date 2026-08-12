using UnityEngine;

/// <summary>
/// Where the summoned room HUD sits. Lives on the same GameObject as <see cref="CrossPlatformRoomHud"/>.
/// (Replaces the earlier HudHandAnchor — placement is no longer hand-derived by default, so the old name lied.)
///
/// Division of labour (deliberate — do not duplicate):
///   • CrossPlatformRoomHud.LateUpdate already owns ROTATION (the billboard keeping the canvas front toward the
///     camera; a back-facing World Space canvas is mirrored AND unclickable).
///   • This component writes POSITION and SCALE only. Nothing contends for the same channel, so execution order
///     between the two does not matter.
///
/// Not re-parented under the avatar: the avatar is a network-spawned object, so a despawn would take the HUD with
/// it and ownership churn would drag a scene object into a networked hierarchy. It follows by transform instead,
/// and stops following (leaving the HUD where it is) rather than snapping to the origin if it loses its reference.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class HudPlacement : MonoBehaviour
{
    public enum PlacementMode
    {
        /// <summary>Straight out in front of the face at arm's reach — a summoned control panel you look at.</summary>
        FrontOfFace,
        /// <summary>Off to the left-hand side, height taken from the eye line. Kept because it is a real option for
        /// a glanceable (rather than looked-at) HUD; measured left-hand placement put the panel at camera-local
        /// z = -0.06, i.e. level with the eyes and 0.56 m to the side, which is why it read as "not in front".</summary>
        LeftHandSide,
    }

    [Tooltip("FrontOfFace: centred in view at reachDistance. LeftHandSide: beside the left hand (glanceable).")]
    [SerializeField] private PlacementMode mode = PlacementMode.FrontOfFace;

    [Tooltip("Metres in front of the eyes, along the horizontal gaze direction. Default 0.55 m ≈ arm's reach, so the " +
             "panel sits where a hand could touch it.")]
    [SerializeField] private float reachDistance = 0.55f;

    [Tooltip("Metres BELOW eye level. Same convention as ChatWorldPanel's spawnHeightOffset (-0.25): a little under " +
             "the eye line so the panel does not sit on top of what you are looking at.")]
    [SerializeField] private float belowEyeLine = 0.15f;

    [Tooltip("LeftHandSide only: metres to push the panel away from the body so the forearm does not pierce it.")]
    [SerializeField] private float towardViewer = 0.05f;

    [Tooltip("Keep the panel's ON-SCREEN size equal to what the room-fixed HUD had at its design distance. The panel " +
             "shrinks physically as it comes closer, so every legibility figure (cap height in arcminutes) carries " +
             "over unchanged. Turn OFF to see the authored physical size — at arm's reach that fills the view.")]
    [SerializeField] private bool preserveApparentSize = true;

    [Tooltip("Distance the HUD's type ramp was designed and gate-checked for (build-studio-room §5, v6 = 1.5 m).")]
    [SerializeField] private float designDistanceM = 1.5f;

    [Tooltip("Position smoothing. 0 = snap (hard-locked to your head, which reads as glued to the screen). Higher " +
             "values let the panel settle a beat behind head movement, which is much calmer to look at.")]
    [SerializeField] private float smoothing = 12f;

    private Canvas _canvas;
    private Transform _hand;
    private Animator _animator;
    private Vector3 _authoredScale;
    private bool _placed;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _authoredScale = transform.localScale;   // authored 1/PxPerMeter — never recomputed, only multiplied
    }

    private void OnDisable() { _placed = false; }

    private void LateUpdate()
    {
        if (_canvas == null || !_canvas.enabled) { _placed = false; return; }   // hidden (summon) → don't chase
        var cam = ActiveCamera();
        if (cam == null) return;

        Vector3 target;
        if (mode == PlacementMode.FrontOfFace)
        {
            // Horizontal gaze only: using the raw forward would drive the panel into the floor or ceiling as soon as
            // you look up or down, and the billboard already handles facing.
            Vector3 fwd = cam.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; else fwd.Normalize();

            target = cam.transform.position + fwd * reachDistance;
            target.y = cam.transform.position.y - belowEyeLine;
        }
        else
        {
            if (!ResolveHand()) return;                       // no hand → leave the HUD where it is
            target = _hand.position;
            target.y = cam.transform.position.y - belowEyeLine;
            Vector3 away = cam.transform.position - target;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f) target += away.normalized * towardViewer;
        }

        // Snap the first frame after being summoned, smooth afterwards. Without the snap the panel visibly flies in
        // from wherever it was last left, which reads as a bug.
        transform.position = (!_placed || smoothing <= 0f)
            ? target
            : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        _placed = true;

        transform.localScale = preserveApparentSize
            // Same angle subtended as the room-fixed panel had at designDistanceM ⇒ identical arcminute figures,
            // so the U6 legibility verdict carries over instead of being quietly invalidated by the move.
            ? _authoredScale * Mathf.Max(0.01f, Vector3.Distance(cam.transform.position, transform.position)
                                                / Mathf.Max(0.01f, designDistanceM))
            : _authoredScale;
    }

    /// <summary>Left hand of the LOCAL avatar. "Local" = the avatar whose subtree owns the active camera; remote
    /// avatars keep their OnlyClient subtree switched off, so this is the unambiguous signal here. Rig-agnostic via
    /// the humanoid bone rather than a bone-name string.</summary>
    private bool ResolveHand()
    {
        if (_hand != null && _animator != null) return true;
        var cam = ActiveCamera();
        if (cam == null) return false;
        _animator = cam.transform.root.GetComponentInChildren<Animator>(true);
        if (_animator == null || !_animator.isHuman) { _animator = null; return false; }
        _hand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        return _hand != null;
    }

    private static Camera ActiveCamera()
    {
        Camera best = null;
        foreach (var c in Camera.allCameras)
        {
            if (!c.isActiveAndEnabled) continue;
            if (best == null || c.depth > best.depth) best = c;
        }
        return best != null ? best : Camera.main;
    }
}
