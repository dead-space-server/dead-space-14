// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Magic;

public sealed class WizardWeaponRestrictionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> WizardWandTag = "WizardWand";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindContainerComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<MindContainerComponent> ent, ref ShotAttemptedEvent args)
    {
        if (args.Cancelled || args.User != ent.Owner)
            return;

        if (_tags.HasTag(args.Used, WizardWandTag))
            return;

        if (ent.Comp.Mind is not { } mindId || !_roles.MindHasRole<WizardRoleComponent>(mindId))
            return;

        if (TryComp<MindComponent>(mindId, out var mind) && GetEntity(mind.OriginalOwnedEntity) is { } original && original != ent.Owner)
            return;

        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }
}
