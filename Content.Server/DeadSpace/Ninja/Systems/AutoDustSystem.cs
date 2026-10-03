// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class AutoDustSystem : SharedAutoDustSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoDustMarkerComponent, MobStateChangedEvent>(OnMobState);

        SubscribeLocalEvent<AutoDustComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<AutoDustComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<AutoDustComponent, ToggleAutoDustModeActionEvent>(OnToggleMode);
    }
    private void OnEquipped(Entity<AutoDustComponent> ent, ref GotEquippedEvent args)
    {
        var marker = EnsureComp<AutoDustMarkerComponent>(args.Equipee);
        marker.AutoDustItem = ent.Owner;
        Dirty(args.Equipee, marker);
    }

    private void OnUnequipped(Entity<AutoDustComponent> ent, ref GotUnequippedEvent args)
    {
        RemComp<AutoDustMarkerComponent>(args.Equipee);
    }

    private void OnToggleMode(Entity<AutoDustComponent> ent, ref ToggleAutoDustModeActionEvent args)
    {
        args.Handled = true;
        var (uid, comp) = ent;
        switch (comp.AutoDustMode)
        {
            case DustMode.Off:
                comp.AutoDustMode = DustMode.Crit;
                _popup.PopupEntity(Loc.GetString("auto-dust-toggle-crit"), args.Performer, args.Performer, PopupType.MediumCaution);
                break;
            case DustMode.Crit:
                comp.AutoDustMode = DustMode.Dead;
                _popup.PopupEntity(Loc.GetString("auto-dust-toggle-dead"), args.Performer, args.Performer, PopupType.MediumCaution);
                break;
            case DustMode.Dead:
                comp.AutoDustMode = DustMode.Off;
                _popup.PopupEntity(Loc.GetString("auto-dust-toggle-off"), args.Performer, args.Performer, PopupType.MediumCaution);
                break;
        }
        Dirty(uid, comp);
    }

    private void OnMobState(EntityUid uid, AutoDustMarkerComponent component, ref MobStateChangedEvent args)
    {
        if (!TryComp<AutoDustComponent>(component.AutoDustItem, out var dust))
            return;

        if (args.NewMobState == MobState.Dead && dust.AutoDustMode != DustMode.Off)
        {
            ActivateAutoDust(uid, component);
        }

        if (args.NewMobState == MobState.Critical && dust.AutoDustMode == DustMode.Crit)
        {
            ActivateAutoDust(uid, component);
        }
    }

    public void ActivateAutoDust(EntityUid uid, AutoDustMarkerComponent component)
    {
        TryComp<SpiderOSComponent>(component.AutoDustItem, out var spiderOS);
        var ev = new AutoDustEvent(uid, spiderOS);
        RaiseLocalEvent(component.AutoDustItem, ref ev);
        var mapCoords = _transform.GetMapCoordinates(uid);
        if (!TryComp<AutoDustComponent>(component.AutoDustItem, out var dust))
            return;
        Spawn(dust.SpawnOnDustProto, mapCoords);

        if (dust.DeleteItems)
        {
            var items = _inventory.GetHandOrInventoryEntities(uid);
            foreach (var item in items)
            {
                QueueDel(item);
            }
        }

        _gibbing.Gib(uid);
    }
}

[ByRefEvent]
public record struct AutoDustEvent(EntityUid Target, SpiderOSComponent? SpiderOS);