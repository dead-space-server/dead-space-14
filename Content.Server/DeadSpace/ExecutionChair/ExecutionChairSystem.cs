// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Electrocution;
using Content.Server.Power.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.ExecutionChair;

public sealed class ExecutionChairSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const float VolumeVariationMin = 0.8f;
    private const float VolumeVariationMax = 1.2f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExecutionChairComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ExecutionChairComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnMapInit(Entity<ExecutionChairComponent> chair, ref MapInitEvent args)
    {
        _deviceLink.EnsureSinkPorts(chair, chair.Comp.TriggerPort);
    }

    private void OnSignalReceived(Entity<ExecutionChairComponent> chair, ref SignalReceivedEvent args)
    {
        if (args.Port != chair.Comp.TriggerPort)
            return;

        TryShock(chair);
    }

    private void TryShock(Entity<ExecutionChairComponent> chair)
    {
        var comp = chair.Comp;

        if (_timing.CurTime < comp.NextShock)
            return;

        if (!Transform(chair).Anchored)
            return;

        if (!TryComp<PowerConsumerComponent>(chair, out var consumer)
            || consumer.ReceivedPower < consumer.DrawRate * comp.RequiredPowerRatio)
            return;

        if (!TryComp<StrapComponent>(chair, out var strap) || strap.BuckledEntities.Count == 0)
            return;

        var shocked = false;

        foreach (var target in strap.BuckledEntities.ToArray())
        {
            if (_mobState.IsDead(target))
                continue;

            if (!_electrocution.TryDoElectrocution(
                    target,
                    chair,
                    comp.ShockDamage,
                    comp.ShockDuration,
                    refresh: true,
                    siemensCoefficient: 1f,
                    ignoreInsulation: true))
                continue;

            shocked = true;

            if (!comp.PlaySoundOnShock)
                continue;

            var volume = comp.ShockVolume * _random.NextFloat(VolumeVariationMin, VolumeVariationMax);
            _audio.PlayPvs(comp.ShockNoises, target, AudioParams.Default.WithVolume(volume));
        }

        if (!shocked)
            return;

        comp.NextShock = _timing.CurTime + comp.Cooldown;
        _popup.PopupEntity(Loc.GetString("execution-chair-discharge"), chair, PopupType.MediumCaution);
    }
}
