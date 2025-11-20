// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Revenant.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Shared.StatusEffectNew;

namespace Content.Server.Revenant.EntitySystems;

public sealed class RevenantForcedSleepSystem : EntitySystem
{
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

            if (comp.Accumulator >= comp.PopupDelay)
            {
                _popup.PopupEntity(Loc.GetString("revenant-sleep-stage"), uid, uid, PopupType.Medium);
                comp.PopupDelay += comp.PopupStep;
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

        _statusEffects.TryAddStatusEffectDuration(ent.Owner, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(duration));
    }
}
