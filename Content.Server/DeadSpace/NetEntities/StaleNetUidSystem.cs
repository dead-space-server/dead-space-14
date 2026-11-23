// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.NetEntities;

public sealed partial class StaleNetUidSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;

    private readonly Dictionary<Type, MemberBinding[]> _bindings = new();
    private readonly Dictionary<EntityUid, List<Slot>> _byOwner = new();
    private readonly Dictionary<EntityUid, List<Slot>> _byTarget = new();
    private readonly HashSet<EntityUid> _found = new();

    private HashSet<EntityUid> _pending = new();
    private HashSet<EntityUid> _spare = new();
    private bool _running;

    public override void Initialize()
    {
        base.Initialize();

        foreach (var registration in _factory.GetAllRegistrations())
        {
            var type = registration.Type;
            if (!type.IsClass || type.IsAbstract)
                continue;

            if (type.GetCustomAttribute<AutoGenerateComponentStateAttribute>() == null)
                continue;

            var bindings = CreateBindings(type);
            if (bindings.Length == 0 || !_bindings.TryAdd(type, bindings))
                continue;
        }

        EntityManager.EntityInitialized += OnEntityInitialized;
        EntityManager.EntityDeleted += OnEntityDeleted;
        EntityManager.EntityDirtied += OnEntityDirtied;
        _running = true;
    }

    public override void Shutdown()
    {
        _running = false;
        EntityManager.EntityInitialized -= OnEntityInitialized;
        EntityManager.EntityDeleted -= OnEntityDeleted;
        EntityManager.EntityDirtied -= OnEntityDirtied;
        base.Shutdown();
    }

    public void Flush()
    {
        if (!_running || _pending.Count == 0)
            return;

        var batch = _pending;
        _pending = _spare;
        _spare = batch;

        foreach (var uid in batch)
            Reindex(uid);

        batch.Clear();
    }

    private void OnEntityInitialized(Entity<MetaDataComponent> ent)
    {
        _pending.Add(ent.Owner);
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> ent)
    {
        if (_byTarget.TryGetValue(ent.Owner, out var slots))
        {
            foreach (var slot in slots)
                _pending.Add(slot.Owner);
        }

        Unlink(ent.Owner, null);
    }

    private void OnEntityDirtied(Entity<MetaDataComponent> ent)
    {
        if (ent.Comp.EntityLifeStage < EntityLifeStage.Terminating)
            _pending.Add(ent.Owner);
    }

    private void Reindex(EntityUid uid)
    {
        if (!IsLive(uid))
        {
            Unlink(uid, null);
            return;
        }

        Unlink(uid, null);

        foreach (var comp in AllComps(uid))
        {
            var type = comp.GetType();
            if (!_bindings.TryGetValue(type, out var bindings) || comp.LifeStage >= ComponentLifeStage.Stopping)
                continue;

            foreach (var binding in bindings)
                binding.Collect(comp, _found);

            var changed = false;
            foreach (var target in _found)
            {
                if (IsLive(target))
                    Link(uid, target, type);
                else
                    changed |= Clear(bindings, comp, target);
            }

            _found.Clear();

            if (changed)
                Dirty(uid, comp);
        }
    }

    private static bool Clear(MemberBinding[] bindings, IComponent comp, EntityUid target)
    {
        var changed = false;
        foreach (var binding in bindings)
            changed |= binding.Clear(comp, target);

        return changed;
    }

    private void Link(EntityUid owner, EntityUid target, Type compType)
    {
        var slot = new Slot(owner, target, compType);

        if (!_byOwner.TryGetValue(owner, out var owned))
        {
            owned = new List<Slot>();
            _byOwner.Add(owner, owned);
        }

        owned.Add(slot);

        if (!_byTarget.TryGetValue(target, out var incoming))
        {
            incoming = new List<Slot>();
            _byTarget.Add(target, incoming);
        }

        incoming.Add(slot);
    }

    private void Unlink(EntityUid owner, Type? compType)
    {
        if (!_byOwner.TryGetValue(owner, out var owned))
            return;

        for (var i = owned.Count - 1; i >= 0; i--)
        {
            var slot = owned[i];
            if (compType != null && slot.CompType != compType)
                continue;

            owned.RemoveAt(i);
            if (!_byTarget.TryGetValue(slot.Target, out var incoming))
                continue;

            incoming.Remove(slot);
            if (incoming.Count == 0)
                _byTarget.Remove(slot.Target);
        }

        if (owned.Count == 0)
            _byOwner.Remove(owner);
    }

    private bool IsLive(EntityUid uid)
    {
        return TryComp(uid, out MetaDataComponent? meta) && meta.EntityLifeStage < EntityLifeStage.Terminating;
    }

    private static MemberBinding[] CreateBindings(Type type)
    {
        var list = new List<MemberBinding>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(flags))
            {
                if (field.IsStatic || field.GetCustomAttribute<AutoNetworkedFieldAttribute>() == null)
                    continue;

                if (TryCreate(field.FieldType, field.GetValue, (component, value) => field.SetValue(component, value), out var binding))
                    list.Add(binding);
            }

            foreach (var property in current.GetProperties(flags))
            {
                if (property.GetCustomAttribute<AutoNetworkedFieldAttribute>() == null ||
                    property.GetMethod == null ||
                    property.SetMethod == null ||
                    property.GetIndexParameters().Length != 0)
                    continue;

                if (TryCreate(property.PropertyType, component => property.GetValue(component), (component, value) => property.SetValue(component, value), out var binding))
                    list.Add(binding);
            }
        }

        return list.ToArray();
    }

    private static bool TryCreate(Type memberType, Func<object?, object?> get, Action<object?, object?> set, [NotNullWhen(true)] out MemberBinding? binding)
    {
        if (memberType == typeof(EntityUid))
        {
            binding = new UidBinding(get, set, false);
            return true;
        }

        if (memberType == typeof(EntityUid?))
        {
            binding = new UidBinding(get, set, true);
            return true;
        }

        if (memberType == typeof(EntityCoordinates))
        {
            binding = new CoordinatesBinding(get, set, false);
            return true;
        }

        if (memberType == typeof(EntityCoordinates?))
        {
            binding = new CoordinatesBinding(get, set, true);
            return true;
        }

        if (!memberType.IsGenericType)
        {
            binding = null;
            return false;
        }

        var definition = memberType.GetGenericTypeDefinition();
        var args = memberType.GetGenericArguments();

        if (definition == typeof(HashSet<>) && args[0] == typeof(EntityUid))
        {
            binding = new HashSetBinding(get);
            return true;
        }

        if (definition == typeof(List<>) && args[0] == typeof(EntityUid))
        {
            binding = new ListBinding(get, false);
            return true;
        }

        if (definition == typeof(List<>) && args[0] == typeof(EntityUid?))
        {
            binding = new ListBinding(get, true);
            return true;
        }

        if (definition == typeof(Dictionary<,>))
        {
            var keyIsUid = args[0] == typeof(EntityUid) || args[0] == typeof(EntityUid?);
            var valueIsUid = args[1] == typeof(EntityUid) || args[1] == typeof(EntityUid?);
            if (keyIsUid || valueIsUid)
            {
                binding = new DictionaryBinding(get);
                return true;
            }
        }

        binding = null;
        return false;
    }

    private sealed partial class Slot
    {
        public readonly EntityUid Owner;
        public readonly EntityUid Target;
        public readonly Type CompType;

        public Slot(EntityUid owner, EntityUid target, Type compType)
        {
            Owner = owner;
            Target = target;
            CompType = compType;
        }
    }

    private abstract partial class MemberBinding
    {
        public abstract void Collect(object component, HashSet<EntityUid> into);
        public abstract bool Clear(object component, EntityUid deleted);
    }

    private sealed partial class UidBinding : MemberBinding
    {
        private readonly Func<object?, object?> _get;
        private readonly Action<object?, object?> _set;
        private readonly bool _nullable;

        public UidBinding(Func<object?, object?> get, Action<object?, object?> set, bool nullable)
        {
            _get = get;
            _set = set;
            _nullable = nullable;
        }

        public override void Collect(object component, HashSet<EntityUid> into)
        {
            if (_get(component) is EntityUid uid && uid.IsValid())
                into.Add(uid);
        }

        public override bool Clear(object component, EntityUid deleted)
        {
            if (_get(component) is not EntityUid uid || uid != deleted)
                return false;

            _set(component, _nullable ? null : EntityUid.Invalid);
            return true;
        }
    }

    private sealed partial class CoordinatesBinding : MemberBinding
    {
        private readonly Func<object?, object?> _get;
        private readonly Action<object?, object?> _set;
        private readonly bool _nullable;

        public CoordinatesBinding(Func<object?, object?> get, Action<object?, object?> set, bool nullable)
        {
            _get = get;
            _set = set;
            _nullable = nullable;
        }

        public override void Collect(object component, HashSet<EntityUid> into)
        {
            if (_get(component) is EntityCoordinates coords && coords.EntityId.IsValid())
                into.Add(coords.EntityId);
        }

        public override bool Clear(object component, EntityUid deleted)
        {
            if (_get(component) is not EntityCoordinates coords || coords.EntityId != deleted)
                return false;

            _set(component, _nullable ? null : EntityCoordinates.Invalid);
            return true;
        }
    }

    private sealed partial class HashSetBinding : MemberBinding
    {
        private readonly Func<object?, object?> _get;

        public HashSetBinding(Func<object?, object?> get)
        {
            _get = get;
        }

        public override void Collect(object component, HashSet<EntityUid> into)
        {
            if (_get(component) is not HashSet<EntityUid> set)
                return;

            foreach (var uid in set)
            {
                if (uid.IsValid())
                    into.Add(uid);
            }
        }

        public override bool Clear(object component, EntityUid deleted)
        {
            return _get(component) is HashSet<EntityUid> set && set.Remove(deleted);
        }
    }

    private sealed partial class ListBinding : MemberBinding
    {
        private readonly Func<object?, object?> _get;
        private readonly bool _nullable;

        public ListBinding(Func<object?, object?> get, bool nullable)
        {
            _get = get;
            _nullable = nullable;
        }

        public override void Collect(object component, HashSet<EntityUid> into)
        {
            if (_nullable)
            {
                if (_get(component) is not List<EntityUid?> list)
                    return;

                foreach (var uid in list)
                {
                    if (uid is { } id && id.IsValid())
                        into.Add(id);
                }

                return;
            }

            if (_get(component) is not List<EntityUid> ids)
                return;

            foreach (var uid in ids)
            {
                if (uid.IsValid())
                    into.Add(uid);
            }
        }

        public override bool Clear(object component, EntityUid deleted)
        {
            if (_nullable)
            {
                return _get(component) is List<EntityUid?> list &&
                       list.RemoveAll(uid => uid == deleted) > 0;
            }

            return _get(component) is List<EntityUid> ids &&
                   ids.RemoveAll(uid => uid == deleted) > 0;
        }
    }

    private sealed partial class DictionaryBinding : MemberBinding
    {
        private readonly Func<object?, object?> _get;

        public DictionaryBinding(Func<object?, object?> get)
        {
            _get = get;
        }

        public override void Collect(object component, HashSet<EntityUid> into)
        {
            if (_get(component) is not IDictionary dictionary)
                return;

            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is EntityUid key && key.IsValid())
                    into.Add(key);

                if (entry.Value is EntityUid value && value.IsValid())
                    into.Add(value);
            }
        }

        public override bool Clear(object component, EntityUid deleted)
        {
            if (_get(component) is not IDictionary dictionary)
                return false;

            List<object>? remove = null;
            foreach (DictionaryEntry entry in dictionary)
            {
                if ((entry.Key is EntityUid key && key == deleted) ||
                    (entry.Value is EntityUid value && value == deleted))
                {
                    remove ??= new List<object>();
                    remove.Add(entry.Key);
                }
            }

            if (remove == null)
                return false;

            foreach (var key in remove)
                dictionary.Remove(key);

            return true;
        }
    }
}
