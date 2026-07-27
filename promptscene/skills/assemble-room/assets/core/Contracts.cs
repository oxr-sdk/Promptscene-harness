using System;
using UnityEngine;

namespace PromptScene.Core
{
    /// <summary>Session/user state the core exposes to content (kept minimal for the pilot).</summary>
    public interface IRoomUserState
    {
        string MultiScaleName { get; }
    }

    /// <summary>Platform-agnostic click/raycast input. Content subscribes; the core decides how clicks are produced
    /// (desktop mouse now; XR controller later) — content never touches platform input directly.</summary>
    public interface IInteraction
    {
        void AddClick(Action<RaycastHit> onClick);
        void RemoveClick(Action<RaycastHit> onClick);
    }

    /// <summary>Spawn abstraction. Local Instantiate for the pilot; a FishNet-backed impl can replace it
    /// without content changing.</summary>
    public interface INetSpawn
    {
        bool IsNetworked { get; }
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        void Despawn(GameObject instance);
    }

    /// <summary>The thin core's service surface. Content depends ONLY on this — never on a concrete RoomManager.
    /// SYSTEMS services are looked up generically via <see cref="TryGet{T}"/> rather than enumerated as fixed
    /// properties, so adding a service never edits this interface (the core cannot grow into a god interface).</summary>
    public interface IRoomCore
    {
        RoomContentRegistry Contents { get; }

        /// <summary>등록된 SYSTEMS 서비스를 조회. 없으면 false — 호출측은 우아하게 대응할 것.</summary>
        bool TryGet<T>(out T service) where T : class;
    }

    /// <summary>Common lifecycle for every CONTENT module.</summary>
    public interface IRoomContent
    {
        string Id { get; }
        void OnRegister(IRoomCore core);
        void OnUnregister();
    }

    /// <summary>Content that can be turned on/off (shown in the launchpad).</summary>
    public interface IToggleableContent : IRoomContent
    {
        ContentMeta Meta { get; }
        bool IsEnabled { get; }
        void SetEnabled(bool on);
    }

    /// <summary>Optional cross-cutting hook — only content that cares implements it; the core never hardcodes per-feature.</summary>
    public interface IScaleScopedContent
    {
        void DespawnByScale(string multiScaleName);
    }

    /// <summary>Implemented by a networked spawned instance so a NON-server client can ask the server to despawn it.
    /// Lets <see cref="INetSpawn.Despawn"/> stay generic: SYSTEMS calls this interface, the FEATURE's own networked
    /// component provides the ServerRpc. (XumNet has no Despawn symbol — despawn is always server-authoritative.)</summary>
    public interface INetDespawnRequest
    {
        void RequestServerDespawn();
    }

    /// <summary>
    /// In-PROCESS typed event bus — the single contract addition that enables the COMPOSITIONS layer (design-directions
    /// D2). A FEATURE can announce that something happened by TYPE (<c>Publish&lt;TargetHitEvent&gt;</c>) without knowing
    /// who — if anyone — listens, and a COMPOSITION (game-mode / mediator) can orchestrate across FEATURES that don't
    /// know each other by subscribing to their events. This keeps the dependency graph one-directional
    /// (COMPOSITIONS → FEATURES → SYSTEMS): two FEATURES never reference each other, they only touch the bus.
    ///
    /// SCOPE (mechanism-not-policy, contract §4.5): the bus only routes typed messages within THIS process. It does
    /// NOT cross the network — publishing does not replicate. Anything that must be shared across clients (e.g. an
    /// authoritative score) still goes through a verified RPC pattern inside the publisher/subscriber's own networked
    /// prefab (M1 RulerMeasurementView / M3 ChatChannelView), exactly as before. The bus is the LOCAL glue; the
    /// network is the FEATURE/COMPOSITION prefab's job.
    ///
    /// Two consumers exist at introduction (TargetProps publishes, the TargetShootout COMPOSITION subscribes — and it
    /// in turn publishes to ScoreHud), satisfying the rule-of-two (§4.5). Registered as a built-in SYSTEMS service and
    /// resolved with <c>core.TryGet&lt;IEventBus&gt;()</c> like every other service, so <see cref="IRoomCore"/> itself
    /// is UNCHANGED (v0.2: adding a service never edits the core interface). Callers handle absence gracefully.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>Deliver <paramref name="evt"/> synchronously to every handler subscribed to type T. A handler that
        /// throws is logged and isolated — it does not abort delivery to the others, nor the publisher.</summary>
        void Publish<T>(T evt);

        /// <summary>Register a handler for events of type T. Idempotent per (T, handler): re-subscribing the same
        /// delegate does not double-invoke.</summary>
        void Subscribe<T>(Action<T> handler);

        /// <summary>Remove a previously-subscribed handler for type T. Safe to call for a handler that isn't subscribed.</summary>
        void Unsubscribe<T>(Action<T> handler);
    }

    /// <summary>Launchpad / harness metadata.</summary>
    [Serializable]
    public struct ContentMeta
    {
        public string DisplayName;
        public Sprite Icon;
        public string Category;
        public bool DefaultOn;
        public string[] MutuallyExclusive;
    }
}
