// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Server.DeadSpace.Virus.Systems;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Medical;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class VomitSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.Vomit;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "VomitSymptom";

    public VomitSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
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
        var virusSystem = _entityManager.System<VirusSystem>();
        var vomitSystem = _entityManager.System<VomitSystem>();

        vomitSystem.Vomit(host);
        virusSystem.InfectAround(host);
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new VomitSymptom(EffectTimedWindow.Clone());
    }
}
