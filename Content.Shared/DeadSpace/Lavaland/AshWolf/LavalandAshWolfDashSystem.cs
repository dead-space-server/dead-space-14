// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Lavaland.AshWolf;

/// <summary>
/// The tamed Ash Wolf is on a dash of its own (a rush, a leap, a jump back): its player's input does not move it
/// meanwhile, the server carries it along.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandAshWolfDashingComponent : Component;

public sealed class LavalandAshWolfDashSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandAshWolfDashingComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<LavalandAshWolfDashingComponent, ComponentStartup>(OnChanged);
        SubscribeLocalEvent<LavalandAshWolfDashingComponent, ComponentShutdown>(OnChanged);
    }

    private void OnCanMove(Entity<LavalandAshWolfDashingComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnChanged<T>(Entity<LavalandAshWolfDashingComponent> ent, ref T args)
    {
        _blocker.UpdateCanMove(ent);
    }
}
