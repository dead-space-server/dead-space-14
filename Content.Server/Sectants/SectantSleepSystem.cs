using Content.Shared.Sectants;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Server.Sectants;

public sealed class SectantSleepSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    private static readonly EntProtoId SleepEffect = "StatusEffectForcedSleeping";

    private readonly HashSet<EntityUid> _entitySet = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantSleepActionEvent>(OnSleep);
    }

    private void OnSleep(SectantSleepActionEvent args)
    {
        var coords = Transform(args.Performer).Coordinates;
        _entitySet.Clear();
        _lookup.GetEntitiesInRange(coords, 5f, _entitySet);
        var duration = TimeSpan.FromSeconds(8);

        foreach (var ent in _entitySet)
        {
            if (ent == args.Performer) continue;
            if (!HasComp<StatusEffectsComponent>(ent)) continue;
            _status.TryAddStatusEffect(ent, SleepEffect, duration, refresh: true);
        }
        args.Handled = true;
    }
}
