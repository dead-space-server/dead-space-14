using Content.Server.Revenant.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Bed.Sleep;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server.Revenant.EntitySystems;

public sealed class RevenantForcedSleepSystem : EntitySystem
{
    [ValidatePrototypeId<StatusEffectPrototype>]
    private const string StatusEffectKey = "ForcedSleep";

    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevenantForcedSleepComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, RevenantForcedSleepComponent comp, ComponentStartup args)
    {
        _popup.PopupEntity(Loc.GetString("revenant-sleep-stage"), uid, uid, PopupType.Large);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<RevenantForcedSleepComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator >= comp.StageDelay)
            {
                _popup.PopupEntity(Loc.GetString("revenant-sleep-stage"), uid, uid, PopupType.Medium);
                comp.StageDelay += 10f;
            }

            if (comp.Accumulator < comp.SleepDelay)
                continue;

            Sleep((uid, comp));
            RemCompDeferred(uid, comp);
        }
    }

    private void Sleep(Entity<RevenantForcedSleepComponent> ent)
    {
        var duration = _random.NextFloat(ent.Comp.DurationOfSleep.X, ent.Comp.DurationOfSleep.Y);

        _statusEffects.TryAddStatusEffect<ForcedSleepingComponent>(ent.Owner, StatusEffectKey,
            TimeSpan.FromSeconds(duration), false);
    }
}
