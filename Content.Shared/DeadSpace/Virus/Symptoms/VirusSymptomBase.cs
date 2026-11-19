// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.TimeWindow;
using Content.Shared.DeadSpace.Virus.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Virus.Symptoms;

public abstract partial class VirusSymptomBase : IVirusSymptom
{
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    public TimedWindow EffectTimedWindow { get; }
    protected abstract ProtoId<VirusSymptomPrototype> PrototypeId { get; }

    protected VirusSymptomBase(TimedWindow effectTimedWindow)
    {
        IoCManager.InjectDependencies(this);
        EffectTimedWindow = effectTimedWindow;
    }

    public abstract VirusSymptom Type { get; }

    public virtual void OnAdded(EntityUid host, VirusComponent virus)
    {
        ApplyDataEffect(virus.Data, true);
    }

    public virtual void OnRemoved(EntityUid host, VirusComponent virus)
    {
        ApplyDataEffect(virus.Data, false);
    }

    public virtual void OnUpdate(EntityUid host, VirusComponent virus)
    {
        var timedWindowSystem = _entityManager.System<TimedWindowSystem>();

        if (timedWindowSystem.IsExpired(EffectTimedWindow))
        {
            DoEffect(host, virus);

            if (!BaseVirusSettings.DebuffVirusMultipliers.TryGetValue(virus.RegenerationType, out var timeMultiplier) || timeMultiplier <= 0f)
                timeMultiplier = 1.0f;

            timedWindowSystem.Reset(
                EffectTimedWindow,
                (float)EffectTimedWindow.Min.TotalSeconds * (1 / timeMultiplier),
                (float)EffectTimedWindow.Max.TotalSeconds * (1 / timeMultiplier)
            );
        }
    }

    public abstract void DoEffect(EntityUid host, VirusComponent virus);
    public abstract IVirusSymptom Clone();
    public virtual void ApplyDataEffect(VirusData data, bool add)
    {
        if (!_prototypeManager.TryIndex(PrototypeId, out var prototype))
            return;

        if (add)
        {
            var timedWindowSystem = _entityManager.System<TimedWindowSystem>();
            timedWindowSystem.Reset(EffectTimedWindow);
            data.Infectivity = Math.Clamp(data.Infectivity + prototype.AddInfectivity, 0, 1);
        }
        else
            data.Infectivity = Math.Clamp(data.Infectivity - prototype.AddInfectivity, 0, 1);
    }
}
