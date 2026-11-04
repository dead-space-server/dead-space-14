// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Notify;
using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryMmiSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SurgeryOrganFreshSystem _fresh = default!;
    [Dependency] private readonly ReturnToBodyNotificationSystem _returnToBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BrainComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<BrainComponent, EntGotInsertedIntoContainerMessage>(OnBrainInserted);
        SubscribeLocalEvent<MMIComponent, PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<MMIComponent, PlayerDetachedEvent>(OnDetached);
    }

    private void OnInsertAttempt(Entity<BrainComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<MMIComponent>(args.SlotEntity) || _fresh.Spoil(ent) < 1f)
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("surgery-mmi-rotten"), args.SlotEntity, user);
    }

    private void OnBrainInserted(Entity<BrainComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var mmi = args.Container.Owner;
        if (!HasComp<MMIComponent>(mmi))
            return;

        Refresh(mmi);
        _returnToBody.Notify(mmi);
    }

    private void OnAttached(Entity<MMIComponent> ent, ref PlayerAttachedEvent args)
    {
        Refresh(ent);
    }

    private void OnDetached(Entity<MMIComponent> ent, ref PlayerDetachedEvent args)
    {
        _appearance.SetData(ent, MMIVisuals.HasMind, false);
    }

    private void Refresh(EntityUid mmi)
    {
        var alive = _mind.TryGetMind(mmi, out _, out var mind) && mind.CurrentEntity == mmi && mind.UserId != null;
        _appearance.SetData(mmi, MMIVisuals.HasMind, alive);
    }
}
