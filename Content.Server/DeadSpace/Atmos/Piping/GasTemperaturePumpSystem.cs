// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Piping;
using Content.Shared.Audio;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Piping;

[UsedImplicitly]
public sealed class GasTemperaturePumpSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GasTemperaturePumpComponent, AtmosDeviceUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(Entity<GasTemperaturePumpComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!_power.IsPowered(ent.Owner)
            || !_nodeContainer.TryGetNodes(ent.Owner, ent.Comp.InletName, ent.Comp.OutletName, out PipeNode? inlet, out PipeNode? outlet))
        {
            SetActive(ent, false);
            return;
        }

        var inAir = inlet.Air;
        var outAir = outlet.Air;

        var inHeatCapacity = _atmosphereSystem.GetHeatCapacity(inAir, true);
        var outHeatCapacity = _atmosphereSystem.GetHeatCapacity(outAir, true);
        if (inHeatCapacity < Atmospherics.MinimumHeatCapacity || outHeatCapacity < Atmospherics.MinimumHeatCapacity)
        {
            SetActive(ent, false);
            return;
        }

        if (inAir.Temperature <= outAir.Temperature)
        {
            SetActive(ent, false);
            return;
        }

        var equilibrium = (inAir.Temperature * inHeatCapacity + outAir.Temperature * outHeatCapacity)
                          / (inHeatCapacity + outHeatCapacity);
        var f = Math.Clamp(ent.Comp.TransferFraction, 0f, 1f);

        inAir.Temperature += (equilibrium - inAir.Temperature) * f;
        outAir.Temperature += (equilibrium - outAir.Temperature) * f;
        SetActive(ent, true);
    }

    private void SetActive(Entity<GasTemperaturePumpComponent> ent, bool active)
    {
        _ambientSound.SetAmbience(ent.Owner, active);
        _appearance.SetData(ent.Owner, FilterVisuals.Enabled, active);
    }
}
