// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Charges.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Actions;

public sealed partial class ActionGateSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public void Set(EntityUid user, string id, IReadOnlyList<EntProtoId> prototypes, bool open)
    {
        var group = Group(user, id);
        Grant(user, group, prototypes);
        Apply(group, open);
    }

    public void Set(EntityUid user, string id, IReadOnlyList<EntityUid> actions, bool open)
    {
        var group = Group(user, id);
        foreach (var action in actions)
        {
            if (TerminatingOrDeleted(action) || group.Granted.Contains(action))
                continue;

            group.Granted.Add(action);
        }

        Apply(group, open);
    }

    private ActionGateGroup Group(EntityUid user, string id)
    {
        var gate = EnsureComp<ActionGateComponent>(user);
        foreach (var group in gate.Groups)
        {
            if (group.Id == id)
                return group;
        }

        var created = new ActionGateGroup { Id = id };
        gate.Groups.Add(created);
        return created;
    }

    private void Grant(EntityUid user, ActionGateGroup group, IReadOnlyList<EntProtoId> prototypes)
    {
        for (var i = group.Granted.Count - 1; i >= 0; i--)
        {
            if (!TerminatingOrDeleted(group.Granted[i]))
                continue;

            group.Granted.RemoveAt(i);
        }

        foreach (var proto in prototypes)
        {
            var present = false;
            foreach (var granted in group.Granted)
            {
                if (MetaData(granted).EntityPrototype?.ID != proto.Id)
                    continue;

                present = true;
                break;
            }

            if (present)
                continue;

            EntityUid? action = null;
            _actions.AddAction(user, ref action, proto);
            if (action != null)
                group.Granted.Add(action.Value);
        }
    }

    private void Apply(ActionGateGroup group, bool open)
    {
        foreach (var action in group.Granted)
            _actions.SetEnabled(action, open);
    }
}

public sealed partial class ActionAvailabilitySystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = default!;

    public bool IsAvailable(Entity<ActionComponent> action)
    {
        var charges = _charges.GetCurrentCharges(action.Owner);
        var ev = new ActionAvailabilityEvent(action, action.Comp.Enabled && charges != 0);
        RaiseLocalEvent(action, ref ev);
        if (action.Comp.AttachedEntity is { } user)
            RaiseLocalEvent(user, ref ev);

        return ev.Available;
    }
}
