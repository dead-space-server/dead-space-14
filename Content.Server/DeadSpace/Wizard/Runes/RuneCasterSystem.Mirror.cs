// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneMirrorDisguiseComponent : Component
{
    public string Name = string.Empty;

    public Dictionary<string, string>? Voices;

    public ProtoId<JobIconPrototype>? JobIcon;

    public bool HadJobIcon;

    public TimeSpan End;
}

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly IdentitySystem _identity = default!;

    private void ApplyMirrorCrowd(RuneCastContext ctx, RuneMirrorCrowdEffect effect)
    {
        var caster = ctx.Caster;
        var center = _transform.GetMapCoordinates(caster);
        var crowd = new List<EntityUid>();
        foreach (var humanoid in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(center, effect.Radius * ctx.RangeScale))
        {
            if (humanoid.Owner != caster && !_mobState.IsDead(humanoid) && !HasComp<RuneMirrorImageComponent>(humanoid))
                crowd.Add(humanoid);
        }

        Spawn(effect.Smoke, Transform(caster).Coordinates);
        if (crowd.Count == 0)
        {
            ShoutCrowd(ctx, caster, crowd);
            return;
        }

        var end = Timing.CurTime + TimeSpan.FromSeconds(effect.Duration * ctx.DurationScale);
        var name = Name(caster);
        var voices = TryComp<TTSComponent>(caster, out var tts) ? new Dictionary<string, string>(tts.Voices) : null;
        foreach (var target in crowd)
        {
            Disguise(target, caster, name, voices, end);
            Spawn(effect.Smoke, Transform(target).Coordinates);
        }

        if (!_random.Prob(effect.StayChance))
            _transform.SwapPositions(caster, _random.Pick(crowd));

        ShoutCrowd(ctx, caster, crowd);
    }

    private void ShoutCrowd(RuneCastContext ctx, EntityUid caster, List<EntityUid> crowd)
    {
        if (ctx.Composition is not { } composition)
            return;

        var runes = new List<RunePrototype> { composition.Form, composition.Element };
        if (composition.Second != null)
            runes.Add(composition.Second);
        runes.AddRange(composition.Modifiers);
        var line = Loc.GetString(composition.Spell?.Name ?? composition.Element.Name);
        var loudness = ComputeLoudness(runes, composition.Spell);
        var absurdity = ComputeAbsurdity(runes, composition.Spell);
        Shout(caster, line, loudness, absurdity, 7);
        foreach (var target in crowd)
            Shout(target, line, loudness, absurdity, target.Id);
    }

    private void Disguise(EntityUid target, EntityUid source, string name, Dictionary<string, string>? voices, TimeSpan end)
    {
        var disguise = EnsureComp<RuneMirrorDisguiseComponent>(target);
        disguise.Name = Name(target);
        disguise.End = end;
        if (voices != null && TryComp<TTSComponent>(target, out var tts))
        {
            disguise.Voices = new Dictionary<string, string>(tts.Voices);
            tts.Voices = new Dictionary<string, string>(voices);
        }

        var image = EnsureComp<RuneMirrorImageComponent>(target);
        image.Source = source;
        Dirty(target, image);

        if (TryComp<JobStatusComponent>(target, out var job))
        {
            disguise.HadJobIcon = true;
            disguise.JobIcon = job.JobStatusIcon;
            if (TryComp<JobStatusComponent>(source, out var sourceJob))
                job.JobStatusIcon = sourceJob.JobStatusIcon;
            Dirty(target, job);
        }

        _meta.SetEntityName(target, name);
        _identity.QueueIdentityUpdate(target);
    }

    private void Undisguise(EntityUid target, RuneMirrorDisguiseComponent disguise)
    {
        _meta.SetEntityName(target, disguise.Name);
        if (disguise.Voices != null && TryComp<TTSComponent>(target, out var tts))
            tts.Voices = disguise.Voices;

        if (disguise.HadJobIcon && TryComp<JobStatusComponent>(target, out var job))
        {
            job.JobStatusIcon = disguise.JobIcon;
            Dirty(target, job);
        }

        RemCompDeferred<RuneMirrorImageComponent>(target);
        RemCompDeferred<RuneMirrorDisguiseComponent>(target);
        _identity.QueueIdentityUpdate(target);
    }

    private void UpdateMirrors()
    {
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<RuneMirrorDisguiseComponent>();
        while (query.MoveNext(out var uid, out var disguise))
        {
            if (now >= disguise.End)
                Undisguise(uid, disguise);
        }
    }
}
