using Content.Shared.Access.Systems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Content.Shared.DoAfter;//DS14
using Content.Shared.Power.EntitySystems;//DS14
using Content.Shared.Database;//DS14
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.Player;//DS14
using Robust.Shared.Serialization;//DS14
using System.Linq;//DS14

namespace Content.Shared.SmartFridge;

public abstract class SharedSmartFridgeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _receiver = default!;//DS14
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;//DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartFridgeComponent, InteractUsingEvent>(OnInteractUsing, after: [typeof(AnchorableSystem)]);
        SubscribeLocalEvent<SmartFridgeComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<SmartFridgeComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
        SubscribeLocalEvent<SmartFridgeComponent, AfterAutoHandleStateEvent>((ent, ref _) => UpdateUI(ent));

        SubscribeLocalEvent<SmartFridgeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);
        SubscribeLocalEvent<SmartFridgeComponent, GetDumpableVerbEvent>(OnGetDumpableVerb);
        SubscribeLocalEvent<SmartFridgeComponent, DumpEvent>(OnDump);

        SubscribeLocalEvent<SmartFridgeComponent, GetVerbsEvent<Verb>>(OnGetVerb);//DS14
        SubscribeLocalEvent<SmartFridgeComponent, ExtractItemDoAfterEvent>(OnExtractItem);//DS14

        Subs.BuiEvents<SmartFridgeComponent>(SmartFridgeUiKey.Key,
            sub =>
            {
                sub.Event<SmartFridgeDispenseItemMessage>(OnDispenseItem);
                sub.Event<SmartFridgeRemoveEntryMessage>(OnRemoveEntry);
            });
    }

    private bool DoInsert(Entity<SmartFridgeComponent> ent, EntityUid user, IEnumerable<EntityUid> usedItems, bool playSound)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.Container, out var container))
            return false;

        if (!Allowed(ent, user))
            return true;

        bool anyInserted = false;
        foreach (var used in usedItems)
        {
            if (!_whitelist.CheckBoth(used, ent.Comp.Blacklist, ent.Comp.Whitelist))
                continue;
            anyInserted = true;

            _container.Insert(used, container);
        }

        if (anyInserted && playSound)
        {
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, user);
        }

        return anyInserted;
    }

    private void OnInteractUsing(Entity<SmartFridgeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_hands.CanDrop(args.User, args.Used))
            return;

        args.Handled = DoInsert(ent, args.User, [args.Used], true);
    }

    private void OnItemInserted(Entity<SmartFridgeComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container || _timing.ApplyingState)
            return;

        var key = new SmartFridgeEntry(Identity.Name(args.Entity, EntityManager));
        if (!ent.Comp.Entries.Contains(key))
            ent.Comp.Entries.Add(key);

        ent.Comp.ContainedEntries.TryAdd(key, new());
        var entries = ent.Comp.ContainedEntries[key];
        if (!entries.Contains(GetNetEntity(args.Entity)))
            entries.Add(GetNetEntity(args.Entity));

        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnItemRemoved(Entity<SmartFridgeComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        //DS-14 start
        if (args.Container.ID != ent.Comp.Container || _timing.ApplyingState)
            return;
        //DS-14 end

        var key = new SmartFridgeEntry(Identity.Name(args.Entity, EntityManager));

        if (ent.Comp.ContainedEntries.TryGetValue(key, out var contained))
        {
            contained.Remove(GetNetEntity(args.Entity));
        }

        Dirty(ent);
        UpdateUI(ent);
    }

    private bool Allowed(Entity<SmartFridgeComponent> machine, EntityUid user)
    {
        if (_accessReader.IsAllowed(user, machine))
            return true;

        _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-eject-access-denied"), machine, user);
        _audio.PlayPredicted(machine.Comp.SoundDeny, machine, user);
        return false;
    }

    private void OnDispenseItem(Entity<SmartFridgeComponent> ent, ref SmartFridgeDispenseItemMessage args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!Allowed(ent, args.Actor))
            return;

        if (!ent.Comp.ContainedEntries.TryGetValue(args.Entry, out var contained))
        {
            _audio.PlayPredicted(ent.Comp.SoundDeny, ent, args.Actor);
            _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-eject-unknown-entry"), ent, args.Actor);
            return;
        }

        foreach (var item in contained)
        {
            if (!_container.TryRemoveFromContainer(GetEntity(item)))
                continue;

            _audio.PlayPredicted(ent.Comp.SoundVend, ent, args.Actor);
            contained.Remove(item);
            Dirty(ent);
            UpdateUI(ent);
            return;
        }

        _audio.PlayPredicted(ent.Comp.SoundDeny, ent, args.Actor);
        _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-eject-out-of-stock"), ent, args.Actor);
    }

    //DS14-start
    private void OnExtractItem(Entity<SmartFridgeComponent> ent, ref ExtractItemDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_timing.IsFirstTimePredicted)
            return;

        if (_receiver.IsPowered(ent.Owner))
        {
            _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-extract-powered-cancel"), ent, args.User);
            return;
        }

        if (!_accessReader.IsAllowed(args.User, ent))
            return;

        if (!ent.Comp.ContainedEntries.TryGetValue(args.Entry, out var contained))
        {
            _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-eject-unknown-entry"), ent, args.User);
            return;
        }

        foreach (var item in contained)
        {
            if (!_container.TryRemoveFromContainer(GetEntity(item)))
                continue;

            contained.Remove(item);
            Dirty(ent);
            UpdateUI(ent);
            return;
        }

        _popup.PopupPredicted(Loc.GetString("smart-fridge-component-try-eject-out-of-stock"), ent, args.User);
    }
    //DS14-end

    private void OnGetAltVerb(Entity<SmartFridgeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!args.CanInteract
            || args.Using is not { } item
            || !_hands.CanDrop(user, item)
            || !_whitelist.CheckBoth(item, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => DoInsert(ent, user, [item], true),
            Text = Loc.GetString("verb-categories-insert"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/insert.svg.192dpi.png")),
        });
    }

    private void OnRemoveEntry(Entity<SmartFridgeComponent> ent, ref SmartFridgeRemoveEntryMessage args)
    {
        if (!Allowed(ent, args.Actor))
            return;

        if (!ent.Comp.ContainedEntries.TryGetValue(args.Entry, out var contained)
            || contained.Count > 0
            || !ent.Comp.Entries.Contains(args.Entry))
            return;

        ent.Comp.Entries.Remove(args.Entry);
        ent.Comp.ContainedEntries.Remove(args.Entry);
        Dirty(ent);
        UpdateUI(ent);
    }

    private void OnGetDumpableVerb(Entity<SmartFridgeComponent> ent, ref GetDumpableVerbEvent args)
    {
        if (_accessReader.IsAllowed(args.User, ent))
        {
            args.Verb = Loc.GetString("dump-smartfridge-verb-name", ("unit", ent));
        }
    }

    private void OnDump(Entity<SmartFridgeComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.PlaySound = true;

        DoInsert(ent, args.User, args.DumpQueue, false);
    }

    // DS14-start
    private void OnGetVerb(EntityUid uid, SmartFridgeComponent component, GetVerbsEvent<Verb> args)
    {
        if (HasComp<ActorComponent>(uid) ||
            !args.CanAccess ||
            !args.CanInteract ||
            !args.CanComplexInteract)
        {
            return;
        }

        if (_receiver.IsPowered(uid))
            return;

        if (!_accessReader.IsAllowed(args.User, uid))
            return;

        for (var i = 0; i < component.Entries.Count; i++)
        {
            var entry = component.Entries[i];
            if (!component.ContainedEntries.TryGetValue(entry, out var items) || items.Count == 0)
                return;
            var netEnt = items.First();

            args.Verbs.Add(new Verb
            {
                Priority = component.Entries.Count - i,
                IconEntity = netEnt,
                Category = VerbCategory.Eject,
                Text = entry.Name,
                Impact = LogImpact.Low,
                DoContactInteraction = false,
                CloseMenu = true,
                Act = () => TryExtractItem((uid,component),args.User, entry)
            });
        }
    }


    private void TryExtractItem(Entity<SmartFridgeComponent> target, EntityUid user, SmartFridgeEntry entry)
    {
        if (_receiver.IsPowered(target.Owner))
            return;

        if (!_accessReader.IsAllowed(user, target))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3), new ExtractItemDoAfterEvent(entry), target) // DS14
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            MovementThreshold = 0.01f,
            DistanceThreshold = 1f,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _audio.PlayPredicted(target.Comp.SoundCreaking, target, user);
    }
    // DS14-end

    protected virtual void UpdateUI(Entity<SmartFridgeComponent> ent)
    {

    }
}
// DS14-start
[Serializable, NetSerializable]
public sealed partial class ExtractItemDoAfterEvent : SimpleDoAfterEvent
{
    public SmartFridgeEntry Entry;

    public ExtractItemDoAfterEvent(SmartFridgeEntry entry)
    {
        Entry = entry;
    }
}
// DS14-end