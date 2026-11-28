// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RuneWalkPaceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Sprint = 0.55f;

    [DataField, AutoNetworkedField]
    public TimeSpan End;
}

public sealed class RuneWalkPaceSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneWalkPaceComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<RuneWalkPaceComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1f, ent.Comp.Sprint);
    }
}
