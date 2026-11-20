// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared.DeadSpace.Surgery;

public sealed partial class SurgeryBusySystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryBusyComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SurgeryBusyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SurgeryBusyComponent, UpdateCanMoveEvent>(OnMove);
    }

    private void OnStartup(Entity<SurgeryBusyComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnShutdown(Entity<SurgeryBusyComponent> ent, ref ComponentShutdown args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnMove(Entity<SurgeryBusyComponent> ent, ref UpdateCanMoveEvent args)
    {
    }
}
