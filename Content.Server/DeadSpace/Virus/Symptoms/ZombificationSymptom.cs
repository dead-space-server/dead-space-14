// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Zombies;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class ZombificationSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.Zombification;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "ZombificationSymptom";

    public ZombificationSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);

        InfectZombieVirus(host);
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
        InfectZombieVirus(host);
    }

    private void InfectZombieVirus(EntityUid target)
    {
        if (_entityManager.HasComponent<ZombieComponent>(target) || _entityManager.HasComponent<ZombieImmuneComponent>(target))
            return;

        // DS14-start
        if (_entityManager.HasComponent<NecromorfComponent>(target) || _entityManager.HasComponent<InfectionDeadComponent>(target))
            return;

        _entityManager.EnsureComponent<PendingZombieComponent>(target);
        _entityManager.EnsureComponent<ZombifyOnDeathComponent>(target);
    }

    public override void ApplyDataEffect(VirusData data, bool add)
    {
        base.ApplyDataEffect(data, add);
    }

    public override IVirusSymptom Clone()
    {
        return new ZombificationSymptom(EffectTimedWindow.Clone());
    }
}
