using Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;
using Content.Shared.Actions;

namespace Content.Server.DeadSpace.Armutant.Abilities.Blade;

public sealed partial class SporeBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _action = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<SporeBladeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SporeBladeComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnComponentInit(EntityUid uid, SporeBladeComponent component, ComponentInit args)
    {
        _action.AddAction(uid, ref component.ActionSporeBladeEntity, component.ActionToggleSporeBlade, uid);
    }

    private void OnComponentShutdown(EntityUid uid, SporeBladeComponent component, ComponentShutdown args)
    {
        _action.RemoveAction(uid, component.ActionSporeBladeEntity);
    }
}
