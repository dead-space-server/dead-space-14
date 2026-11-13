// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.DeadSpace.Virus.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class DrowsinessSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    public override VirusSymptom Type => VirusSymptom.Drowsiness;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "DrowsinessSymptom";
    public static readonly EntProtoId StatusEffectForcedSleeping = "StatusEffectForcedSleeping";

    private const float MinSleepDuration = 5f;
    private const float MaxSleepDuration = 15f;

    public DrowsinessSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);
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
        var statusEffectsSystem = _entityManager.System<StatusEffectsSystem>();

        var sleepDuration = _random.NextFloat(MinSleepDuration, MaxSleepDuration);
        statusEffectsSystem.TryAddStatusEffectDuration(host, StatusEffectForcedSleeping, TimeSpan.FromSeconds(sleepDuration));
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new DrowsinessSymptom(EffectTimedWindow.Clone());
    }
}
