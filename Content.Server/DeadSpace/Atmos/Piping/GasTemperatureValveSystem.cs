// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Piping;
using Content.Shared.Audio;
using Content.Shared.DeadSpace.Atmos.Piping;
using JetBrains.Annotations;

namespace Content.Server.DeadSpace.Atmos.Piping;

[UsedImplicitly]
public sealed class GasTemperatureValveSystem : SharedGasTemperatureValveSystem
{
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTemperatureValveComponent, AtmosDeviceUpdateEvent>(OnUpdate);
    }

    private void OnUpdate(Entity<GasTemperatureValveComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNodes(ent.Owner, ent.Comp.InletName, ent.Comp.OutletName, out PipeNode? inlet, out PipeNode? outlet))
        {
            SetOpen(ent, null, null, false);
            return;
        }

        var inletHasGas = inlet.Air.TotalMoles > 0f;
        var outletHasGas = outlet.Air.TotalMoles > 0f;
        if (!inletHasGas && !outletHasGas)
        {
            SetOpen(ent, inlet, outlet, false);
            return;
        }

        var temperature = inletHasGas ? inlet.Air.Temperature : outlet.Air.Temperature;
        if (inletHasGas && outletHasGas)
            temperature = MathF.Max(inlet.Air.Temperature, outlet.Air.Temperature);
        var threshold = MathF.Min(ent.Comp.Threshold, GasTemperatureValveComponent.MaxThreshold);
        var shouldOpen = ent.Comp.PassWhenBelow ? temperature < threshold : temperature > threshold;

        SetOpen(ent, inlet, outlet, shouldOpen);
    }

    private void SetOpen(Entity<GasTemperatureValveComponent> ent, PipeNode? inlet, PipeNode? outlet, bool open)
    {
        if (ent.Comp.Open == open)
            return;

        ent.Comp.Open = open;

        if (inlet != null && outlet != null)
        {
            if (open)
            {
                inlet.AddAlwaysReachable(outlet);
                outlet.AddAlwaysReachable(inlet);
            }
            else
            {
                inlet.RemoveAlwaysReachable(outlet);
                outlet.RemoveAlwaysReachable(inlet);
            }
        }

        _ambientSound.SetAmbience(ent.Owner, open);
        _appearance.SetData(ent.Owner, FilterVisuals.Enabled, open);
    }
}
