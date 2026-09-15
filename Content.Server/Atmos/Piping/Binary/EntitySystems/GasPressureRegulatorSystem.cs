using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Piping;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Audio;
using JetBrains.Annotations;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Piping.Binary.EntitySystems;

/// <summary>
/// Handles serverside logic for pressure regulators. Gas will only flow through the regulator
/// if the pressure on the inlet side is over a certain pressure threshold.
/// See https://en.wikipedia.org/wiki/Pressure_regulator
/// </summary>
[UsedImplicitly]
public sealed class GasPressureRegulatorSystem : SharedGasPressureRegulatorSystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasPressureRegulatorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GasPressureRegulatorComponent, AtmosDeviceUpdateEvent>(OnPressureRegulatorUpdated);
        SubscribeLocalEvent<GasPressureRegulatorComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<GasPressureRegulatorComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUiUpdate = _timing.CurTime + ent.Comp.UpdateInterval;
    }

    /// <summary>
    /// Dirties the regulator every second or so, so that the UI can update.
    /// The UI automatically updates after an AutoHandleStateEvent.
    /// </summary>
    /// <param name="frameTime"></param>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GasPressureRegulatorComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUiUpdate > _timing.CurTime)
                continue;

            comp.NextUiUpdate += comp.UpdateInterval;

            DirtyFields(uid,
                comp,
                null,
                nameof(comp.InletPressure),
                nameof(comp.OutletPressure),
                nameof(comp.FlowRate));
        }
    }

    private void OnInit(Entity<GasPressureRegulatorComponent> ent, ref ComponentInit args)
    {
        UpdateAppearance(ent);
    }

    /// <summary>
    /// Handles the updating logic for the pressure regulator.
    /// </summary>
    /// <param name="ent"> the <see cref="Entity{T}" /> of the pressure regulator</param>
    /// <param name="args"> Args provided to us via <see cref="AtmosDeviceUpdateEvent" /></param>
    private void OnPressureRegulatorUpdated(Entity<GasPressureRegulatorComponent> ent,
        ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNodes(ent.Owner,
                ent.Comp.InletName,
                ent.Comp.OutletName,
                out PipeNode? inletPipeNode,
                out PipeNode? outletPipeNode))
        {
            ChangeStatus(false, ent, inletPipeNode, outletPipeNode, 0);
            return;
        }

        /*
        It's time for some math! :)

        Gas is simply transferred from the inlet to the outlet, restricted by flow rate and pressure.
        We want to transfer enough gas to bring the inlet pressure below the threshold,
        and only as much as our max flow rate allows.

        The equations:
        PV = nRT
        P1 = P2

        Can be used to calculate the amount of gas we need to transfer.
        */

        var p1 = inletPipeNode.Air.Pressure;

        if (p1 <= ent.Comp.Threshold) // DS14 - regulators may exhaust into a higher-pressure outlet.
        {
            ChangeStatus(false, ent, inletPipeNode, outletPipeNode, 0);
            return;
        }

        var t1 = inletPipeNode.Air.Temperature;

        // DS14-start - the setpoint controls inlet pressure; outlet pressure does not throttle the regulator.
        // Calculate the amount of gas we need to transfer to bring the inlet down to the threshold.
        var deltaMolesToPressureThreshold =
            AtmosphereSystem.MolesToPressureThreshold(inletPipeNode.Air, ent.Comp.Threshold);

        // Convert to the desired inlet volume to transfer.
        var desiredVolumeToTransfer = deltaMolesToPressureThreshold * ((Atmospherics.R * t1) / p1);
        // DS14-end

        // And finally, limit the transfer volume to the max flow rate of the valve.
        var actualVolumeToTransfer = Math.Min(desiredVolumeToTransfer,
            ent.Comp.MaxTransferRate * _atmosphere.PumpSpeedup() * args.dt);

        // We remove the gas from the inlet and merge it into the outlet.
        var removed = inletPipeNode.Air.RemoveVolume(actualVolumeToTransfer);
        _atmosphere.Merge(outletPipeNode.Air, removed);

        // Calculate the flow rate in L/s for the UI.
        var sentFlowRate = MathF.Round(actualVolumeToTransfer / args.dt, 1);

        ChangeStatus(true, ent, inletPipeNode, outletPipeNode, sentFlowRate);
    }

    /// <summary>
    /// Updates the visual appearance of the pressure regulator based on its current state.
    /// </summary>
    /// <param name="ent">The <see cref="Entity{GasPressureRegulatorComponent, AppearanceComponent}"/>
    /// representing the pressure regulator with respective components.</param>
    private void UpdateAppearance(Entity<GasPressureRegulatorComponent> ent)
    {
        _appearance.SetData(ent,
            PressureRegulatorVisuals.State,
            ent.Comp.Enabled);
    }

    /// <summary>
    /// Updates the pressure regulator's appearance and sound based on its current state, while
    /// also preventing network spamming.
    /// Also prepares data for dirtying.
    /// </summary>
    /// <param name="enabled">The new state to set</param>
    /// <param name="ent">The pressure regulator to update</param>
    /// <param name="inletNode">The inlet node of the pressure regulator</param>
    /// <param name="outletNode">The outlet node of the pressure regulator</param>
    /// <param name="flowRate">Current flow rate of the pressure regulator</param>
    private void ChangeStatus(bool enabled,
        Entity<GasPressureRegulatorComponent> ent,
        PipeNode? inletNode,
        PipeNode? outletNode,
        float flowRate)
    {
        // First, set data on the component server-side.
        ent.Comp.InletPressure = inletNode?.Air.Pressure ?? 0f;
        ent.Comp.OutletPressure = outletNode?.Air.Pressure ?? 0f;
        ent.Comp.FlowRate = flowRate;

        // We need to prevent spamming the network with updates, so only check if we've
        // switched states.
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        _ambientSound.SetAmbience(ent, enabled);
        UpdateAppearance(ent);

        // The regulator has changed state, so we need to dirty all applicable fields *right now* so the UI updates
        // at the same time as everything else.
        DirtyFields(ent.AsNullable(),
            null,
            nameof(ent.Comp.InletPressure),
            nameof(ent.Comp.OutletPressure),
            nameof(ent.Comp.FlowRate));
    }
}
