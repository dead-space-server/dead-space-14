// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Movement.Components;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class ParalyzedLegsSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.ParalyzedLegs;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "ParalyzedLegsSymptom";
    private bool _hasComp = false;

    public ParalyzedLegsSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);

        if (_entityManager.HasComponent<WormComponent>(host))
            _hasComp = true;
        else
            _entityManager.AddComponent<WormComponent>(host);
    }

    public override void OnRemoved(EntityUid host, VirusComponent virus)
    {
        base.OnRemoved(host, virus);

        if (!_hasComp && _entityManager.HasComponent<WormComponent>(host))
            _entityManager.RemoveComponent<WormComponent>(host);
    }

    public override void OnUpdate(EntityUid host, VirusComponent virus)
    {
        base.OnUpdate(host, virus);
    }

    public override void DoEffect(EntityUid host, VirusComponent virus)
    {

    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new ParalyzedLegsSymptom(EffectTimedWindow.Clone());
    }
}
