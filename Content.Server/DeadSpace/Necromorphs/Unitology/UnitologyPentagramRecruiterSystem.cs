// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Actions;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

/// <inheritdoc cref="UnitologyPentagramRecruiterComponent"/>
public sealed class UnitologyPentagramRecruiterSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UnitologyPentagramRecruiterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<UnitologyPentagramRecruiterComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<UnitologyPentagramRecruiterComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnShutdown(Entity<UnitologyPentagramRecruiterComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }
}
