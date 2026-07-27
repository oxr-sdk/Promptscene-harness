using System;
using System.Collections.Generic;
using System.Linq;

namespace PromptScene.Core
{
    /// <summary>
    /// Holds the CONTENT modules present in the room. Content self-registers here.
    /// The core iterates this registry for cross-cutting work instead of referencing features directly,
    /// so adding/removing content never edits the core.
    /// </summary>
    public class RoomContentRegistry
    {
        private readonly IRoomCore _core;
        private readonly List<IRoomContent> _contents = new List<IRoomContent>();

        public event Action<IRoomContent> OnContentRegistered;
        public event Action<IRoomContent> OnContentUnregistered;
        public event Action<IToggleableContent, bool> OnContentToggled;

        public RoomContentRegistry(IRoomCore core) { _core = core; }

        public IReadOnlyList<IRoomContent> All => _contents;
        public IEnumerable<IToggleableContent> Toggleable => _contents.OfType<IToggleableContent>();

        public void Register(IRoomContent content)
        {
            if (content == null || _contents.Contains(content)) return;
            _contents.Add(content);
            content.OnRegister(_core);
            OnContentRegistered?.Invoke(content);
        }

        public void Unregister(IRoomContent content)
        {
            if (content == null || !_contents.Remove(content)) return;
            content.OnUnregister();
            OnContentUnregistered?.Invoke(content);
        }

        public IRoomContent GetById(string id) => _contents.FirstOrDefault(c => c.Id == id);
        public T Get<T>() where T : class => _contents.OfType<T>().FirstOrDefault();

        public void NotifyToggled(IToggleableContent content, bool on) => OnContentToggled?.Invoke(content, on);

        /// <summary>Cross-cutting example: tell every scale-scoped content to clean up for a scale.</summary>
        public void DespawnByScale(string multiScaleName)
        {
            foreach (var s in _contents.OfType<IScaleScopedContent>())
                s.DespawnByScale(multiScaleName);
        }
    }
}
