using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Popups;
using Content.Shared.Alert;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Internals;
using Content.Shared.Inventory;
using Content.Shared.Roles;

namespace Content.Server.Body.Systems;

public sealed partial class InternalsSystem : SharedInternalsSystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private RespiratorSystem _respirator = default!;

    private EntityQuery<InternalsComponent> _internalsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _internalsQuery = GetEntityQuery<InternalsComponent>();

        SubscribeLocalEvent<InternalsComponent, InhaleLocationEvent>(OnInhaleLocation);
        SubscribeLocalEvent<InternalsComponent, StartingGearEquippedEvent>(OnStartingGear);
    }

    private void OnStartingGear(EntityUid uid, InternalsComponent component, ref StartingGearEquippedEvent args)
    {
        if (component.BreathTools.Count == 0)
            return;

        if (component.GasTankEntity != null)
            return; // already connected

        // Can the entity breathe the air it is currently exposed to?
        if (_respirator.CanMetabolizeInhaledAir(uid))
            return;

        var tank = FindBestGasTank(uid);
        if (tank == null)
            return;

        // Could the entity metabolise the air in the linked gas tank?
        if (!_respirator.CanMetabolizeInhaledAir(uid, tank.Value.Comp.Air))
            return;

        ToggleInternals(uid, uid, force: false, component, ToggleMode.On);
    }

    private void OnInhaleLocation(Entity<InternalsComponent> ent, ref InhaleLocationEvent args)
    {
        if (AreInternalsWorking(ent))
        {
            var tankEnt = ent.Comp.GasTankEntity!.Value;
            var gasTank = Comp<GasTankComponent>(tankEnt);
            //DS-14 start
            if (!_respirator.Saturates(ent.Owner, gasTank.Air))
            {
                var better = FindBestGasTank(ent.Owner);
                if (better != null && _respirator.Saturates(ent.Owner, better.Value.Comp.Air))
                {
                    if (better.Value.Owner != tankEnt && !_gasTank.ConnectToInternals(better.Value, user: ent.Owner))
                        return;
                }
                else
                {
                    _gasTank.DisconnectFromInternals((tankEnt, gasTank), user: ent.Owner);
                    return;
                }

                if (!AreInternalsWorking(ent))
                    return;

                tankEnt = ent.Comp.GasTankEntity!.Value;
                gasTank = Comp<GasTankComponent>(tankEnt);
            }
            //DS-14 end
            args.Gas = _gasTank.RemoveAirVolume((tankEnt, gasTank), args.Respirator.BreathVolume);
            // TODO: Should listen to gas tank updates instead I guess?
            _alerts.ShowAlert(ent.Owner, ent.Comp.InternalsAlert, GetSeverity(ent));
        }
    }
}
