// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Audio;
using Content.Shared.DeadSpace.Atmos.Piping;
using Content.Shared.Power;
using JetBrains.Annotations;
using GasEntry = Content.Shared.Atmos.Components.GasAnalyzerComponent.GasEntry;
using GasMixEntry = Content.Shared.Atmos.Components.GasAnalyzerComponent.GasMixEntry;

namespace Content.Server.DeadSpace.Atmos.Piping;

[UsedImplicitly]
public sealed class GasElectrolyzerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GasElectrolyzerComponent, AtmosDeviceUpdateEvent>(OnUpdate);
        SubscribeLocalEvent<GasElectrolyzerComponent, GasElectrolyzerToggleMessage>(OnToggle);
    }

    private void OnUpdate(Entity<GasElectrolyzerComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        var powered = _power.IsPowered(ent.Owner);
        var hasNodes = _nodeContainer.TryGetNodes(ent.Owner, ent.Comp.InletName, ent.Comp.OutletName, out PipeNode? inlet, out PipeNode? outlet);

        var working = false;
        if (ent.Comp.Enabled && powered && hasNodes && inlet!.Air.TotalMoles > 0f
            && outlet!.Air.Pressure < ent.Comp.MaxOutletPressure)
        {
            var transferVolume = ent.Comp.TransferRate * args.dt;
            var removed = inlet.Air.RemoveVolume(transferVolume);

            var releasedEnergy = 0f;
            foreach (var (input, reaction) in ent.Comp.Reactions)
            {
                var amount = removed.GetMoles(input);
                if (amount <= 0f)
                    continue;

                removed.AdjustMoles(input, -amount);
                foreach (var (product, ratio) in reaction.Products)
                    removed.AdjustMoles(product, amount * ratio);

                releasedEnergy += reaction.Energy * amount;
                working = true;
            }

            if (releasedEnergy != 0f)
            {
                var heatCapacity = _atmosphereSystem.GetHeatCapacity(removed, true);
                if (heatCapacity > Atmospherics.MinimumHeatCapacity)
                    removed.Temperature = MathF.Max(removed.Temperature + releasedEnergy / heatCapacity, Atmospherics.TCMB);
            }

            _atmosphereSystem.Merge(outlet!.Air, removed);
        }

        _ambientSound.SetAmbience(ent.Owner, working);
        _appearance.SetData(ent.Owner, PowerDeviceVisuals.Powered, working);

        UpdateUi(ent, powered, hasNodes ? inlet : null, hasNodes ? outlet : null);
    }

    private void OnToggle(Entity<GasElectrolyzerComponent> ent, ref GasElectrolyzerToggleMessage args)
    {
        ent.Comp.Enabled = !ent.Comp.Enabled;

        if (!ent.Comp.Enabled)
        {
            _ambientSound.SetAmbience(ent.Owner, false);
            _appearance.SetData(ent.Owner, PowerDeviceVisuals.Powered, false);
        }

        var powered = _power.IsPowered(ent.Owner);
        _nodeContainer.TryGetNodes(ent.Owner, ent.Comp.InletName, ent.Comp.OutletName, out PipeNode? inlet, out PipeNode? outlet);
        UpdateUi(ent, powered, inlet, outlet);
    }

    private void UpdateUi(Entity<GasElectrolyzerComponent> ent, bool powered, PipeNode? inlet, PipeNode? outlet)
    {
        if (!_ui.IsUiOpen(ent.Owner, GasElectrolyzerUiKey.Key))
            return;

        var input = BuildMixEntry("gas-electrolyzer-ui-input", inlet?.Air);
        var output = BuildMixEntry("gas-electrolyzer-ui-output", outlet?.Air);

        _ui.SetUiState(ent.Owner, GasElectrolyzerUiKey.Key,
            new GasElectrolyzerBoundUserInterfaceState(ent.Comp.Enabled, powered, input, output));
    }
    private GasMixEntry BuildMixEntry(string nameLoc, GasMixture? mix)
    {
        if (mix == null)
            return new GasMixEntry(Loc.GetString(nameLoc), 0f, 0f, 0f, Array.Empty<GasEntry>());

        return new GasMixEntry(Loc.GetString(nameLoc), mix.Volume, mix.Pressure, mix.Temperature, BuildGasEntries(mix));
    }

    private GasEntry[] BuildGasEntries(GasMixture mix)
    {
        var list = new List<GasEntry>();
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mix.GetMoles(i);
            if (moles <= 0.01f)
                continue;

            var gas = _atmosphereSystem.GetGas(i);
            list.Add(new GasEntry(Loc.GetString(gas.Name), moles, gas.Color));
        }

        return list.ToArray();
    }
}
