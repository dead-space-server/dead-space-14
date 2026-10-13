// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Wear.Components;
using Content.Shared.Interaction;
using Content.Shared.RCD.Systems;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;

namespace Content.Server.DeadSpace.Wear;

public sealed class AddWearAfterUseSystem : EntitySystem
{
    [Dependency] private readonly WearSystem _wear = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddWearAfterUseComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<AddWearAfterUseComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<AddWearAfterUseComponent, TileToolDoAfterEvent>(OnToolTileComplete);
        SubscribeLocalEvent<AddWearAfterUseComponent, RCDDoAfterEvent>(OnRCDDoAfter);
    }

    private void OnMeleeHit(EntityUid uid, AddWearAfterUseComponent component, MeleeHitEvent args)
    {
        if ((component.Triggers & WearTrigger.MeleeHit) == 0)
            return;

        if (args.HitEntities.Count <= 0)
            return;

        if (component.Whitelist != null && _whitelist.IsWhitelistFail(component.Whitelist, uid))
            return;

        if (TryComp<WearComponent>(uid, out var wearComp))
            _wear.AddWear(uid, component.Damage, wearComp);
    }

    private void OnAfterInteractUsing(EntityUid uid, AddWearAfterUseComponent component, AfterInteractUsingEvent args)
    {
        if ((component.Triggers & WearTrigger.Interact) == 0)
            return;

        if (args.Target == null)
            return;

        if (component.Whitelist != null && _whitelist.IsWhitelistFail(component.Whitelist, uid))
            return;

        if (TryComp<WearComponent>(uid, out var wearComp))
            _wear.AddWear(uid, component.Damage, wearComp);
    }

    private void OnToolTileComplete(EntityUid uid, AddWearAfterUseComponent component, TileToolDoAfterEvent args)
    {
        if ((component.Triggers & WearTrigger.TileTool) == 0)
            return;

        if (component.Whitelist != null && _whitelist.IsWhitelistFail(component.Whitelist, uid))
            return;

        if (TryComp<WearComponent>(uid, out var wearComp))
            _wear.AddWear(uid, component.Damage, wearComp);
    }

    private void OnRCDDoAfter(EntityUid uid, AddWearAfterUseComponent component, RCDDoAfterEvent args)
    {
        if ((component.Triggers & WearTrigger.RCD) == 0)
            return;

        if (component.Whitelist != null && _whitelist.IsWhitelistFail(component.Whitelist, uid))
            return;

        if (TryComp<WearComponent>(uid, out var wearComp))
            _wear.AddWear(uid, component.Damage, wearComp);
    }
}
