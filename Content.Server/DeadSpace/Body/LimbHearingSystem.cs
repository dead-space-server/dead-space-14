// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Shared.Player;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Body;

public sealed partial class LimbHearingSystem : EntitySystem
{
    private static readonly EntProtoId DeafEffect = "StatusEffectDeaf";

    [Dependency] private StatusEffectsSystem _status = default!;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnChatRecipients);
    }

    private void OnChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        List<ICommonSession>? drop = null;
        foreach (var (session, _) in ev.Recipients)
        {
            if (session.AttachedEntity is not { } listener || listener == ev.Source || !CantHear(listener))
                continue;

            drop ??= new List<ICommonSession>();
            drop.Add(session);
        }

        if (drop == null)
            return;

        foreach (var session in drop)
            ev.Recipients.Remove(session);
    }

    public bool CantHear(EntityUid uid)
    {
        return LimbHearing.State(EntityManager, uid) == LimbHearingState.Deaf
               || _status.HasEffectComp<DeafStatusEffectComponent>(uid);
    }

    public void Damage(EntityUid uid, float amount, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false) || !limbs.HasEars || limbs.EarsGone)
            return;

        limbs.Ears = MathF.Max(0f, limbs.Ears + amount);
        Dirty(uid, limbs);
    }

    public void SetGone(EntityUid uid, bool gone, LimbBodyComponent? limbs = null)
    {
        if (!Resolve(uid, ref limbs, false) || !limbs.HasEars)
            return;

        limbs.EarsGone = gone;
        limbs.Ears = 0f;
        Dirty(uid, limbs);
        if (gone)
            _status.TrySetStatusEffectDuration(uid, DeafEffect);
        else
            _status.TryRemoveStatusEffect(uid, DeafEffect);
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        var dt = _accumulator;
        _accumulator = 0f;
        var query = EntityQueryEnumerator<LimbBodyComponent>();
        while (query.MoveNext(out var uid, out var limbs))
        {
            if (limbs.EarsGone || limbs.Ears <= 0f || limbs.Ears >= LimbHearing.Lost)
                continue;

            limbs.Ears = MathF.Max(0f, limbs.Ears - LimbHearing.Heal * dt);
            Dirty(uid, limbs);
        }
    }
}
