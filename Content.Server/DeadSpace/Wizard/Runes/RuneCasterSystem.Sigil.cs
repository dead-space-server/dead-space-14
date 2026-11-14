// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    private const float SigilIdle = 8f;
    private const float SigilLinger = 1f;
    private const float ImpactMaxRadius = 6f;

    private void InitializeSigil()
    {
        SubscribeNetworkEvent<RuneSigilClearMessage>(OnSigilClear);
    }

    private void OnSigilClear(RuneSigilClearMessage msg, EntitySessionEventArgs args)
    {
        if (TryGetCaster(args, out var caster))
            EndSigil(caster, RuneSigilState.Fade);
    }

    private void DrawSigil(EntityUid caster, List<string> chain)
    {
        var sigil = EnsureComp<RuneSigilComponent>(caster);
        if (sigil.State != RuneSigilState.Drawing || sigil.Chain.Count == 0 || chain.Count <= 1)
            sigil.Started = Timing.CurTime;

        sigil.State = RuneSigilState.Drawing;
        sigil.Chain = new List<string>(chain);
        sigil.Changed = Timing.CurTime;
        Dirty(caster, sigil);
    }

    private void StumbleSigil(EntityUid caster)
    {
        if (!TryComp<RuneSigilComponent>(caster, out var sigil) || sigil.State != RuneSigilState.Drawing)
            return;

        sigil.Stumbled = Timing.CurTime;
        Dirty(caster, sigil);
    }

    private void EndSigil(EntityUid caster, RuneSigilState state)
    {
        if (!TryComp<RuneSigilComponent>(caster, out var sigil) || sigil.State != RuneSigilState.Drawing)
            return;

        sigil.State = state;
        sigil.Changed = Timing.CurTime;
        Dirty(caster, sigil);
    }

    private void FinishSigil(EntityUid caster, RuneComposition composition, RuneSigilState state)
    {
        if (!TryComp<RuneSigilComponent>(caster, out var sigil) || sigil.State != RuneSigilState.Drawing)
        {
            var chain = new List<string> { composition.Form.ID, composition.Element.ID };
            if (composition.Second != null)
                chain.Add(composition.Second.ID);
            foreach (var modifier in composition.Modifiers)
                chain.Add(modifier.ID);

            DrawSigil(caster, chain);
        }

        EndSigil(caster, state);
    }

    private void UpdateSigils()
    {
        var query = EntityQueryEnumerator<RuneSigilComponent>();
        while (query.MoveNext(out var uid, out var sigil))
        {
            var age = (float) (Timing.CurTime - sigil.Changed).TotalSeconds;
            if (sigil.State == RuneSigilState.Drawing)
            {
                if (age > SigilIdle)
                    EndSigil(uid, RuneSigilState.Fade);
            }
            else if (age > SigilLinger)
            {
                RemCompDeferred<RuneSigilComponent>(uid);
            }
        }
    }

    private void RaiseImpact(EntityUid caster, RuneComposition composition, RuneSpellPrototype spell, EntityCoordinates target, EntityUid? entity)
    {
        var projectile = spell.Effects.Exists(e => e is RuneProjectileEffect);
        var at = projectile || spell.Targeting == RuneTargeting.Self
            ? Transform(caster).Coordinates
            : entity is { } hit && !TerminatingOrDeleted(hit) ? Transform(hit).Coordinates : target;

        var radius = composition.Form.ID switch
        {
            "RuneFormWave" => 3f,
            "RuneFormUltimate" => 3.5f,
            "RuneFormTouch" => 0.8f,
            "RuneFormSelf" => 1.1f,
            "RuneFormWall" => 1.8f,
            _ => projectile ? 0.7f : 1.4f,
        };
        radius = MathF.Min(radius * composition.RangeScale, ImpactMaxRadius);

        var ev = new RuneImpactEvent(GetNetCoordinates(at), composition.Element.ID, composition.Second?.ID, radius, composition.Power > 1f);
        RaiseNetworkEvent(ev, Filter.Pvs(at, entityMan: EntityManager));
    }
}
