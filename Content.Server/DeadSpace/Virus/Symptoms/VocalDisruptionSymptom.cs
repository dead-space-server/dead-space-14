// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Symptoms;
using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Robust.Shared.Prototypes;
using Content.Server.Speech.Prototypes;
using Content.Server.Speech.Components;
using Content.Shared.DeadSpace.Virus.Prototypes;

namespace Content.Server.DeadSpace.Virus.Symptoms;

public sealed partial class VocalDisruptionSymptom : VirusSymptomBase
{
    [Dependency] private EntityManager _entityManager = default!;
    public override VirusSymptom Type => VirusSymptom.VocalDisruption;
    protected override ProtoId<VirusSymptomPrototype> PrototypeId => "VocalDisruptionSymptom";
    private static readonly ProtoId<ReplacementAccentPrototype> Accent = "virus";
    private ProtoId<ReplacementAccentPrototype>? _oldAccent = null;

    public VocalDisruptionSymptom(TimedWindow effectTimedWindow) : base(effectTimedWindow)
    { }

    public override void OnAdded(EntityUid host, VirusComponent virus)
    {
        base.OnAdded(host, virus);

        if (_entityManager.TryGetComponent<ReplacementAccentComponent>(host, out var component))
            _oldAccent = component.Accent;
        else
        {
            var comp = _entityManager.AddComponent<ReplacementAccentComponent>(host);
            comp.Accent = Accent;
        }
    }

    public override void OnRemoved(EntityUid host, VirusComponent virus)
    {
        base.OnRemoved(host, virus);

        if (_entityManager.TryGetComponent<ReplacementAccentComponent>(host, out var component)
            && _oldAccent is { } accent)
            component.Accent = accent;
        else
            _entityManager.RemoveComponent<ReplacementAccentComponent>(host);
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
        return new VocalDisruptionSymptom(EffectTimedWindow.Clone());
    }
}
