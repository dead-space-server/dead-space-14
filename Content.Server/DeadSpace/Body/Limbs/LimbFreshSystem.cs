// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Rotting;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Examine;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Body.Limbs;

public sealed class LimbFreshSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbFreshComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LimbFreshComponent, ExaminedEvent>(OnExamine);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<LimbFreshComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var fresh, out var temp))
        {
            if (fresh.Spent || Preserved(uid, temp))
                continue;

            fresh.Left -= frameTime;
            if (fresh.Left > 0f)
                continue;

            fresh.Left = 0f;
            fresh.Spent = true;
            Dirty(uid, fresh);
        }
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

        if (Preserved(ent, null))
        {
            args.PushMarkup(Loc.GetString("limb-fresh-cold"));
            return;
        }

        args.PushMarkup(Loc.GetString("limb-fresh"));
    }

    private bool Preserved(EntityUid uid, TemperatureComponent? temp)
    {
        if (temp == null)
            TryComp(uid, out temp);

        if (temp != null && temp.CurrentTemperature < Atmospherics.T0C + 0.85f)
            return true;

        return _container.TryGetOuterContainer(uid, Transform(uid), out var container)
               && HasComp<AntiRottingContainerComponent>(container.Owner);
    }
}
