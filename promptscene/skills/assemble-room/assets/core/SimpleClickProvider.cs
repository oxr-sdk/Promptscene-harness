using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PromptScene.Core
{
    /// <summary>
    /// Desktop implementation of IInteraction: on left-mouse-down, raycast from the active rendering camera
    /// through the cursor and invoke subscribed handlers with the hit. (XR controller variant can replace this
    /// behind the same interface later.) Legacy Input is fine — project uses Both input handling.
    /// </summary>
    public class SimpleClickProvider : MonoBehaviour, IInteraction
    {
        [SerializeField] private float maxDistance = 1000f;
        [SerializeField] private LayerMask mask = ~0;

        /// <summary>Generic world-click suppression. An on-screen UI claims suppression while the cursor is over it,
        /// so an IMGUI/overlay click doesn't also raycast the world (the uGUI EventSystem guard below only covers
        /// uGUI). Mechanism, not policy — any UI can use it. Claim-based (generalized in M3): with a single writable
        /// bool, two panels writing every frame let the LAST writer win by script-execution order, silently breaking
        /// the other panel's suppression. Now each panel claims/releases independently and the world click is
        /// suppressed while ANY claim is held.</summary>
        public static bool SuppressWorldClick => _suppressors.Count > 0;

        private static readonly HashSet<object> _suppressors = new HashSet<object>();

        public static void SetWorldClickSuppressed(object claimant, bool on)
        {
            if (claimant == null) return;
            if (on) _suppressors.Add(claimant);
            else _suppressors.Remove(claimant);
        }

        private readonly List<Action<RaycastHit>> _handlers = new List<Action<RaycastHit>>();

        public void AddClick(Action<RaycastHit> onClick)
        {
            if (onClick != null && !_handlers.Contains(onClick)) _handlers.Add(onClick);
        }

        public void RemoveClick(Action<RaycastHit> onClick) => _handlers.Remove(onClick);

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

        private void Update()
        {
            if (_handlers.Count == 0) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (SuppressWorldClick) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            var cam = ActiveCamera();
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask))
            {
                foreach (var h in _handlers.ToArray()) h?.Invoke(hit);
            }
        }

        /// <summary>Cross-platform world-click entry (mechanism, not policy — contract §4.5): an XR world-input helper
        /// (e.g. a controller ray on trigger) submits a Ray and the same subscribed handlers fire with the physics hit,
        /// exactly like a desktop mouse click. Respects SuppressWorldClick (so aiming at the HUD panel doesn't also
        /// drop a measurement). Content still only ever calls AddClick/RemoveClick — IInteraction is unchanged.</summary>
        public void SubmitExternalRay(Ray ray)
        {
            if (_handlers.Count == 0) return;
            if (SuppressWorldClick) return;
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask))
            {
                foreach (var h in _handlers.ToArray()) h?.Invoke(hit);
            }
        }
    }
}
