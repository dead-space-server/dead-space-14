// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Emoting;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneScryingOrbComponent : Component
{
    [DataField]
    public EntProtoId Projection = "RuneAstralProjection";

    [DataField]
    public float Duration = 20f;
}

[RegisterComponent]
public sealed partial class RuneAstralProjectionComponent : Component
{
    public EntityUid Body;

    public EntityUid Mind;

    public TimeSpan End;
}

public sealed partial class RuneCasterSystem
{
    private void InitializeOrb()
    {
        SubscribeLocalEvent<RuneScryingOrbComponent, UseInHandEvent>(OnOrbUse);
        SubscribeLocalEvent<RuneAstralProjectionComponent, SpeakAttemptEvent>((_, _, args) => args.Cancel());
        SubscribeLocalEvent<RuneAstralProjectionComponent, EmoteAttemptEvent>((_, _, args) => args.Cancel());
    }

    private void OnOrbUse(Entity<RuneScryingOrbComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_mind.TryGetMind(args.User, out var mindId, out var mind) || mind.VisitingEntity != null)
            return;

        args.Handled = true;
        var projection = Spawn(ent.Comp.Projection, Transform(args.User).Coordinates);
        var astral = EnsureComp<RuneAstralProjectionComponent>(projection);
        astral.Body = args.User;
        astral.Mind = mindId;
        astral.End = Timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Duration);
        _mind.Visit(mindId, projection, mind);
        _popup.PopupEntity(Loc.GetString("rune-orb-leave", ("seconds", (int) ent.Comp.Duration)), projection, projection, PopupType.Medium);
    }

    private void UpdateOrb()
    {
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<RuneAstralProjectionComponent>();
        while (query.MoveNext(out var uid, out var astral))
        {
            var visiting = TryComp<MindComponent>(astral.Mind, out var mind) && mind.VisitingEntity == uid;
            if (visiting && now < astral.End && !TerminatingOrDeleted(astral.Body) && !_mobState.IsDead(astral.Body))
                continue;

            if (visiting)
            {
                _mind.UnVisit(astral.Mind, mind);
                _popup.PopupEntity(Loc.GetString("rune-orb-return"), astral.Body, astral.Body, PopupType.Medium);
            }

            QueueDel(uid);
        }
    }
}
