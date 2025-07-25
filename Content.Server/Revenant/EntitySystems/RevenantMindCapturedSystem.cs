using Content.Server.Revenant.Components;
using Content.Server.Ghost;
using Content.Shared.Mind;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Ghost;
using Content.Shared.Revenant.Components;
using Content.Shared.Corvax.TTS;
using Content.Shared.Mind.Components;

namespace Content.Server.Revenant.EntitySystems;

public sealed class RevenantMindCapturedSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RevenantMindCapturedComponent, MindUnvisitedMessage>(OnUnvisited);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<RevenantMindCapturedComponent>();

        while (enumerator.MoveNext(out var uid, out var comp))
        {
            comp.Accumulator += frameTime;

            if (!TryComp<RevenantComponent>(comp.RevenantUid, out var revComp) || revComp.Essence <= 0)
            {
                EndCaptureForce(uid, comp);
                RemCompDeferred(uid, comp);
            }

            if (HasComp<MobStateComponent>(uid) && _mobState.IsDead(uid))
            {
                EndCapture(uid, comp);
                RemCompDeferred(uid, comp);
            }

            if (comp.Accumulator < comp.DurationOfCapture)
                continue;

            EndCapture(uid, comp);
            RemCompDeferred(uid, comp);
        }
    }
    private void EndCapture(EntityUid uid, RevenantMindCapturedComponent comp)
    {
        if (!_mind.TryGetMind(comp.RevenantUid, out var mindId, out var mind))
            return;

        if (TryComp<TTSComponent>(uid, out var tts))
        {
            if (!string.IsNullOrEmpty(comp.ReturnTTSPrototype))
                tts.VoicePrototypeId = comp.ReturnTTSPrototype;
            else
                tts.VoicePrototypeId = null;
        }

        if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Dead, out var damage))
            return;
        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Cold", damage.Value);

        _damage.TryChangeDamage(uid, dspec, true, origin: uid);
        _mind.UnVisit(mindId, mind);
    }

    private void EndCaptureForce(EntityUid uid, RevenantMindCapturedComponent comp)
    {

        if (!_mobThresholdSystem.TryGetThresholdForState(uid, MobState.Dead, out var damage))
            return;

        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Cold", damage.Value);
        _damage.TryChangeDamage(uid, dspec, true, origin: uid);
    }

    private void OnUnvisited(EntityUid uid, RevenantMindCapturedComponent comp, MindUnvisitedMessage args)
    {
        if (TryComp<GhostComponent>(comp.TargetUid, out var ghostComponent))
            _ghost.SetCanReturnToBody(ghostComponent, true);
    }
}
