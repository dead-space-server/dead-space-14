// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class BlindableSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.Blindable;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "BlindableSymptom";
    private float _eyeDamageProcent = 0.7f;
    private int _eyeTotalDamage = 0;

    public BlindableSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);

        var system = _entityManager.System<BlindableSystem>();

        if (_entityManager.TryGetComponent<BlindableComponent>(host, out var component))
        {
            var damage = component.MaxDamage - component.MinDamage;
            _eyeTotalDamage = (int)Math.Round(damage - damage * _eyeDamageProcent);
        }

        system.AdjustEyeDamage((host, component), _eyeTotalDamage);
    }

    public override void OnRemoved(EntityUid host, VirusComponent virus)
    {
        base.OnRemoved(host, virus);

        var system = _entityManager.System<BlindableSystem>();

        if (_entityManager.TryGetComponent<BlindableComponent>(host, out var component))
            system.AdjustEyeDamage((host, component), -_eyeTotalDamage);
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
        return new BlindableSymptom(EffectTimedWindow.Clone());
    }
}
