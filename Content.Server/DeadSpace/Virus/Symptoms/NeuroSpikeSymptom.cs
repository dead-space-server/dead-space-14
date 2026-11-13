// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Jittering;
using Content.Server.Stunnable;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class NeuroSpikeSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.NeuroSpike;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "NeuroSpikeSymptom";
    private TimedWindow _duration = default!;

    public NeuroSpikeSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);

        _duration = new TimedWindow(TimeSpan.FromSeconds(5f), TimeSpan.FromSeconds(10f));
    }

    public override void OnRemoved(EntityUid host, VirusComponent virus)
    {
        base.OnRemoved(host, virus);
    }

    public override void OnUpdate(EntityUid host, VirusComponent virus)
    {
        base.OnUpdate(host, virus);
    }

    public override void DoEffect(EntityUid host, VirusComponent virus)
    {
        var jitteringSystem = _entityManager.System<SharedJitteringSystem>();
        var stun = _entityManager.System<StunSystem>();
        var timedWindowSystem = _entityManager.System<TimedWindowSystem>();

        timedWindowSystem.Reset(_duration);
        var duration = timedWindowSystem.GetSecondsRemaining(_duration);

        jitteringSystem.DoJitter(host, TimeSpan.FromSeconds(duration), true);
        stun.TryUpdateParalyzeDuration(host, TimeSpan.FromSeconds(duration));
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new NeuroSpikeSymptom(EffectTimedWindow.Clone());
    }
}
