// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Buckle.Components;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Interaction;
using Content.Shared.Standing;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryProneSystem : EntitySystem
{
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbBodyComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<SurgeryProneComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<SurgeryProneComponent, StoodEvent>(OnStood);
    }

    private void OnVerbs(Entity<LimbBodyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner || !CanFlip(ent.Owner))
            return;

        var target = ent.Owner;
        var prone = HasComp<SurgeryProneComponent>(target);
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(prone ? "surgery-flip-back" : "surgery-flip-front"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png")),
            Act = () => Flip(target),
        });
    }

    public void Flip(EntityUid patient)
    {
        if (!CanFlip(patient))
            return;

        if (!RemComp<SurgeryProneComponent>(patient))
            EnsureComp<SurgeryProneComponent>(patient);
    }

    private bool CanFlip(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle) && buckle.Buckled
               && buckle.BuckledTo is { } strap && HasComp<SurgerySurfaceComponent>(strap);
    }

    private void OnUnbuckled(Entity<SurgeryProneComponent> ent, ref UnbuckledEvent args)
    {
        RemCompDeferred<SurgeryProneComponent>(ent);
    }

    private void OnStood(Entity<SurgeryProneComponent> ent, ref StoodEvent args)
    {
        RemCompDeferred<SurgeryProneComponent>(ent);
    }
}
