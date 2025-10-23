using Content.Shared.DeadSpace.Abilities.Bloodsucker.Components;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Abilities.Bloodsucker;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Abilities.Bloodsucker;

public sealed partial class BloodIncubatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly BloodsuckerSystem _bloodsucker = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodIncubatorComponent, ModBloodEvent>(OnModBlood);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<BloodIncubatorComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (curTime > component.TimeUntilUpdate)
            {
                component.TimeUntilUpdate = _timing.CurTime + TimeSpan.FromSeconds(component.UpdateDuration);

                if (!TryComp(uid, out BloodstreamComponent? bloodstreamComponent))
                    return;

                _bloodsucker.SetReagentCount(uid, (float)_solutionContainer.GetTotalPrototypeQuantity(uid, bloodstreamComponent.BloodSolutionName));
            }

        }

    }

    private void OnModBlood(EntityUid uid, BloodIncubatorComponent component, ModBloodEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(uid, out BloodstreamComponent? bloodstreamComponent))
            return;

        if (!_bloodsucker.TryModifyBloodLevel(uid, args.Quantity, bloodstreamComponent))
            return;

        args.Handled = true;
    }


}
