// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared._CM14.Attachable;
using Content.Shared._CM14.Attachable.Components;
using Content.Shared._CM14.Attachable.Events;
using Content.Shared.DeadSpace.Attachable;
using System.Linq;

namespace Content.Server.DeadSpace.Attachable;

/// <summary>
/// Maintains components provided to holders by installed attachments.
/// Components originally present on a holder are never overwritten or removed.
/// </summary>
public sealed class AttachableComponentProviderSystem : EntitySystem
{
    [Dependency] private readonly AttachableHolderSystem _holderSystem = default!;

    private readonly Dictionary<(EntityUid Holder, Type Type), EntityUid> _provided = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AttachableComponentProviderComponent, AttachableAlteredEvent>(OnAttachableAltered);
        SubscribeLocalEvent<AttachableHolderComponent, EntityTerminatingEvent>(OnHolderTerminating);
    }

    private void OnAttachableAltered(Entity<AttachableComponentProviderComponent> ent,
        ref AttachableAlteredEvent args)
    {
        if (args.Alteration is not (AttachableAlteredType.Attached or AttachableAlteredType.Detached))
            return;

        if (!TryComp(args.Holder, out AttachableHolderComponent? holder))
            return;

        RefreshProvidedComponents((args.Holder, holder));
    }

    private void RefreshProvidedComponents(Entity<AttachableHolderComponent> holder)
    {
        var desired = new Dictionary<Type, EntityUid>();

        foreach (var slot in holder.Comp.Slots.Keys)
        {
            if (!_holderSystem.TryGetAttachable(holder, slot, out var attachable) ||
                !TryComp(attachable, out AttachableComponentProviderComponent? provider))
            {
                continue;
            }

            foreach (var componentName in provider.Components)
            {
                if (!EntityManager.ComponentFactory.TryGetRegistration(componentName, out var registration) ||
                    registration.Type == typeof(TransformComponent) ||
                    registration.Type == typeof(MetaDataComponent) ||
                    !EntityManager.TryGetComponent(attachable, registration.Type, out _))
                {
                    continue;
                }

                desired.TryAdd(registration.Type, attachable);
            }
        }

        foreach (var (type, source) in desired)
        {
            var key = (holder.Owner, type);
            if (!_provided.TryGetValue(key, out var previousSource))
            {
                // Preserve components that belong to the holder's own prototype or another system.
                if (EntityManager.HasComponent(holder, EntityManager.ComponentFactory.GetRegistration(type)))
                    continue;

                CopyComponent(source, holder, type);
                _provided[key] = source;
                continue;
            }

            if (previousSource == source)
                continue;

            CopyComponent(source, holder, type);
            _provided[key] = source;
        }

        var obsolete = _provided.Keys
            .Where(key => key.Holder == holder.Owner && !desired.ContainsKey(key.Type))
            .ToList();

        foreach (var key in obsolete)
        {
            RemComp(holder, key.Type);
            _provided.Remove(key);
        }
    }

    private void CopyComponent(EntityUid source, EntityUid holder, Type type)
    {
        if (!EntityManager.TryGetComponent(source, type, out var component))
            return;

        CopyComp(source, holder, component);
    }

    private void OnHolderTerminating(Entity<AttachableHolderComponent> holder, ref EntityTerminatingEvent args)
    {
        foreach (var key in _provided.Keys.Where(key => key.Holder == holder.Owner).ToList())
        {
            _provided.Remove(key);
        }
    }
}
