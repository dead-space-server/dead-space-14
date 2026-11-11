// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryMannequinComponent : Component;

public sealed class SurgeryMannequinStandSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryMannequinComponent, StandAttemptEvent>(OnStand);
        SubscribeLocalEvent<SurgeryMannequinComponent, UpdateMobStateEvent>(OnUpdateState, after: [typeof(MobThresholdSystem), typeof(LimbWoundSystem)]);
        SubscribeLocalEvent<SurgeryMannequinComponent, MobStateChangedEvent>(OnStateChanged);
    }

    private void OnStand(Entity<SurgeryMannequinComponent> ent, ref StandAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnUpdateState(Entity<SurgeryMannequinComponent> ent, ref UpdateMobStateEvent args)
    {
        if (args.State == MobState.Dead)
            args.State = MobState.Critical;
    }

    private void OnStateChanged(EntityUid uid, SurgeryMannequinComponent component, MobStateChangedEvent args)
    {
        if (_net.IsServer && args.NewMobState == MobState.Dead)
            _mobs.ChangeMobState(uid, MobState.Critical, args.Component);
    }
}
