// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Database;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryHeadBrainSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyPartComponent, InteractUsingEvent>(OnUse);
        SubscribeLocalEvent<BodyPartComponent, SurgeryHeadBrainDoAfterEvent>(OnDoAfter);
    }

    private void OnUse(Entity<BodyPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp<SurgeryMarkerComponent>(args.Used, out var marker)
            || Brain(ent) == null)
            return;

        args.Handled = true;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(marker.HeadBrainDelay), new SurgeryHeadBrainDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<BodyPartComponent> ent, ref SurgeryHeadBrainDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || Brain(ent) is not { } brain)
            return;

        args.Handled = true;
        EnsureComp<SurgeryKeepOrganComponent>(brain);
        var removed = _body.RemoveOrgan(brain);
        RemComp<SurgeryKeepOrganComponent>(brain);
        if (!removed)
            return;

        _hands.PickupOrDrop(args.User, brain);
        _adminLog.Add(LogType.Surgery, LogImpact.High, $"{ToPrettyString(args.User):user} took {ToPrettyString(brain):entity} out of the severed head {ToPrettyString(ent.Owner):entity}");
        if (_prototype.TryIndex<SurgerySoundPrototype>(nameof(SurgeryVerbKind.Extract), out var sound))
            _audio.PlayPvs(sound.Sound, ent.Owner);
    }

    private EntityUid? Brain(Entity<BodyPartComponent> part)
    {
        if (part.Comp.PartType != BodyPartType.Head || part.Comp.Body != null)
            return null;

        foreach (var organ in _body.GetPartOrgans(part.Owner, part.Comp))
        {
            if (HasComp<BrainComponent>(organ.Id))
                return organ.Id;
        }

        return null;
    }
}
