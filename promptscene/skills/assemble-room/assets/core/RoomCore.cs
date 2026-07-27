using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Object;
using XumNet;

namespace PromptScene.Core
{
    /// <summary>
    /// Thin room core. Lives under [CORE]. Knows NO specific feature — it only holds shared services
    /// (interaction / spawn / user state) in a generic container and the content registry. Content finds it
    /// via RoomCore.Instance, resolves services with TryGet, and self-registers. Adding a service is a one-line
    /// RegisterService call in Awake — IRoomCore never changes. Removing a content module touches nothing here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomCore : MonoBehaviour, IRoomCore
    {
        public static RoomCore Instance { get; private set; }

        public RoomContentRegistry Contents { get; private set; }

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        // NOTE: RegisterService lives on the concrete class, NOT on IRoomCore. A FEATURE only ever sees
        // IRoomCore, so it CANNOT register a service — the compiler enforces "dependencies flow FEATURE→SYSTEMS
        // one direction". SYSTEMS wires services here; FEATURES only read them via TryGet.
        public void RegisterService<T>(T service) where T : class => _services[typeof(T)] = service;

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var o)) { service = (T)o; return true; }
            service = null; return false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            Contents = new RoomContentRegistry(this);

            // Built-in SYSTEMS services. Registered in Awake so they exist before any FEATURE self-registers
            // (FEATURE self-registration happens in Start — see the content lifecycle §3 of the contract).
            var click = GetComponent<SimpleClickProvider>();
            if (click == null) click = gameObject.AddComponent<SimpleClickProvider>();
            RegisterService<IInteraction>(click);
            RegisterService<INetSpawn>(new FishNetSpawn());
            RegisterService<IRoomUserState>(new LocalUserState());
            RegisterService<IEventBus>(new EventBus());   // in-process typed bus — enables the COMPOSITIONS layer (D2)
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Minimal pilot implementations; swap for FishNet-backed versions without touching content.
        private sealed class LocalUserState : IRoomUserState
        {
            public string MultiScaleName => string.Empty;
        }

        // In-process typed event bus (contract IEventBus). Mechanism-not-policy (§4.5): routes typed messages within
        // this process only — it knows nothing about any FEATURE or COMPOSITION and never crosses the network.
        // Unity's game loop is single-threaded, so no locking is needed. Delivery snapshots the invocation list, so a
        // handler that (un)subscribes during dispatch affects only future publishes; a handler that throws is logged
        // and isolated so it cannot abort delivery to the others (a bad FEATURE subscriber must not break a
        // COMPOSITION's scoring).
        private sealed class EventBus : IEventBus
        {
            private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

            public void Subscribe<T>(Action<T> handler)
            {
                if (handler == null) return;
                _handlers.TryGetValue(typeof(T), out var existing);
                var current = (Action<T>)existing;
                // Idempotent per (T, handler): re-subscribing the same delegate must not double-invoke (matches the
                // Contains-guard style of SimpleClickProvider.AddClick / RoomContentRegistry.Register).
                if (current != null && Array.IndexOf(current.GetInvocationList(), (Delegate)handler) >= 0) return;
                _handlers[typeof(T)] = current + handler;               // null-safe combine
            }

            public void Unsubscribe<T>(Action<T> handler)
            {
                if (handler == null || !_handlers.TryGetValue(typeof(T), out var existing)) return;
                var next = (Action<T>)existing - handler;
                if (next == null) _handlers.Remove(typeof(T));
                else _handlers[typeof(T)] = next;
            }

            public void Publish<T>(T evt)
            {
                if (!_handlers.TryGetValue(typeof(T), out var existing) || !(existing is Action<T> a)) return;
                foreach (var d in a.GetInvocationList())   // snapshot: safe against (un)subscribe during dispatch
                {
                    try { ((Action<T>)d)(evt); }
                    catch (Exception ex) { Debug.LogException(ex); }
                }
            }
        }

        // FishNet-backed spawn. This is the anticipated "swap for a FishNet-backed impl without touching content"
        // (Contracts.cs INetSpawn note). Mechanism-not-policy (contract §4.5): it only maps the generic spawn/despawn
        // to the network transport — it knows nothing about any FEATURE. Falls back to local Instantiate when no
        // network is running (e.g. an offline editor), so content still works uncoupled.
        private sealed class FishNetSpawn : INetSpawn
        {
            public bool IsNetworked =>
                InstanceFinder.NetworkManager != null && (InstanceFinder.IsClientStarted || InstanceFinder.IsServerStarted);

            public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
            {
                if (prefab == null) return null;
                if (!IsNetworked) return UnityEngine.Object.Instantiate(prefab, position, rotation);
                var nob = prefab.GetComponent<NetworkObject>();
                if (nob == null)
                {
                    Debug.LogWarning("[FishNetSpawn] prefab has no NetworkObject — local Instantiate fallback (won't replicate)");
                    return UnityEngine.Object.Instantiate(prefab, position, rotation);
                }
                var owner = InstanceFinder.ClientManager != null ? InstanceFinder.ClientManager.Connection : null;
                // On a client this round-trips through the server (XumNet ServerRpc) and returns null; on server/host
                // it returns the spawned instance. Either way the clone appears on all observers.
                return XumNetwork.Instantiate(nob, position, rotation, owner);
            }

            public void Despawn(GameObject instance)
            {
                if (instance == null) return;
                var nob = instance.GetComponent<NetworkObject>();
                if (nob == null || !nob.IsSpawned) { UnityEngine.Object.Destroy(instance); return; }
                if (InstanceFinder.IsServerStarted)
                {
                    InstanceFinder.ServerManager.Despawn(instance);      // XumNet has no Despawn — go through FishNet
                }
                else
                {
                    var req = instance.GetComponent<INetDespawnRequest>(); // FEATURE component provides the ServerRpc
                    if (req != null) req.RequestServerDespawn();
                    else Debug.LogWarning("[FishNetSpawn] networked instance lacks INetDespawnRequest — cannot despawn from client");
                }
            }
        }
    }
}
