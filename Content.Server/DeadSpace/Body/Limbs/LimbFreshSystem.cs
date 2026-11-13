// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Examine;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed partial class LimbFreshSystem : EntitySystem
{
    public const float SlowRate = 0.1f;

    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbFreshComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimbFreshComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<BuckleComponent, GetRotMultiplierEvent>(OnTableRot);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LimbFreshComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var fresh, out var temp))
        {
            if (fresh.Spent || Preserved(uid, temp, fresh))
                continue;

            fresh.Left -= frameTime * Rate(uid);
            if (fresh.Left > 0f)
                continue;

            fresh.Left = 0f;
            fresh.Spent = true;
            Dirty(uid, fresh);
        }
    }

    public float Rate(EntityUid uid)
    {
        var current = uid;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            if (HasComp<AntiRottingContainerComponent>(container.Owner))
                return SlowRate;

            current = container.Owner;
        }

        var ev = new GetRotMultiplierEvent();
        RaiseLocalEvent(current, ref ev);
        return ev.Multiplier;
    }

    private void OnTableRot(Entity<BuckleComponent> ent, ref GetRotMultiplierEvent args)
    {
        if (ent.Comp.BuckledTo is { } table && HasComp<SurgeryXrayTableComponent>(table))
            args.Multiplier *= SlowRate;
    }

    private void OnStartup(Entity<LimbFreshComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<AtmosExposedComponent>(ent);
        EnsureComp<TemperatureComponent>(ent);
    }

    private void OnExamine(Entity<LimbFreshComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Spent)
        {
            args.PushMarkup(Loc.GetString("limb-fresh-spent"));
            return;
        }

        if (Preserved(ent, null, ent.Comp))
        {
            args.PushMarkup(Loc.GetString("limb-fresh-cold"));
            return;
        }

        args.PushMarkup(Loc.GetString("limb-fresh"));
    }

    private bool Preserved(EntityUid uid, TemperatureComponent? temp, LimbFreshComponent fresh)
    {
        var current = uid;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            if (HasComp<LimbFreshLockComponent>(container.Owner))
                return true;

            if (fresh.ColdKeeps && HasComp<AntiRottingContainerComponent>(container.Owner))
                return true;

            current = container.Owner;
        }

        if (!fresh.ColdKeeps)
            return false;

        if (temp == null)
            TryComp(uid, out temp);

        return temp != null && temp.CurrentTemperature < Atmospherics.T0C + 0.85f;
    }
}
