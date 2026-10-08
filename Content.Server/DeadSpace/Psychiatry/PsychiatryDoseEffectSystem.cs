// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Popups;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Psychiatry;

public sealed class PsychiatryClarityDoseSystem : EntityEffectSystem<MobStateComponent, PsychiatryClarityDose>
{
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<PsychiatryClarityDose> args)
    {
        _psychiatry.ApplyClarityDose(entity, args.Scale);
    }
}

public sealed class PsychiatryPsychogenDoseSystem : EntityEffectSystem<MobStateComponent, PsychiatryPsychogenDose>
{
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<PsychiatryPsychogenDose> args)
    {
        _psychiatry.ApplyPsychogenDose(entity, args.Scale);
    }
}

public sealed class PsychiatryInhaleSystem : EntityEffectSystem<MobStateComponent, PsychiatryInhale>
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<PsychiatryInhale> args)
    {
        if (!_psychiatry.TryInhalePsychogen(entity))
            return;

        _popup.PopupEntity(Loc.GetString("psychiatry-onset-felt"), entity, entity, PopupType.MediumCaution);
    }
}

[RegisterComponent]
public sealed partial class PsychogenDoseComponent : Component
{
    public float Units;
}
