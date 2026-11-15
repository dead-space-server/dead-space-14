// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem
{
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private static readonly EntProtoId AshPrototype = "Ash";
    private const int ScrollRunes = 2;

    private void InitializeScrolls()
    {
        SubscribeLocalEvent<RuneScrollComponent, UseInHandEvent>(OnScrollUse);
        SubscribeLocalEvent<RuneScrollComponent, AfterInteractEvent>(OnScrollInteract);
        SubscribeLocalEvent<RuneQuillComponent, AfterInteractEvent>(OnQuillInteract);
        SubscribeNetworkEvent<RuneQuillInscribeMessage>(OnQuillInscribe);
        SubscribeNetworkEvent<RuneQuillBurnMessage>(OnQuillBurn);
        SubscribeLocalEvent<RuneScrollHatComponent, GotEquippedEvent>(OnHatEquipped);
        SubscribeLocalEvent<RuneScrollHatComponent, GotUnequippedEvent>(OnHatUnequipped);
    }

    private void OnHatEquipped(Entity<RuneScrollHatComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.HEAD) == 0)
            return;

        ent.Comp.Wearer = args.Equipee;
        ent.Comp.Next = Timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
    }

    private void OnHatUnequipped(Entity<RuneScrollHatComponent> ent, ref GotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
    }

    private void UpdateScrollHats()
    {
        var now = Timing.CurTime;
        var hats = EntityQueryEnumerator<RuneScrollHatComponent, StorageComponent>();
        while (hats.MoveNext(out var uid, out var hat, out var storage))
        {
            if (hat.Wearer is not { } wearer || now < hat.Next)
                continue;

            hat.Next = now + TimeSpan.FromSeconds(hat.Interval);
            if (!HasComp<RuneCasterComponent>(wearer) || _mobState.IsDead(wearer))
                continue;

            var scroll = Spawn(hat.Scroll, Transform(uid).Coordinates);
            if (!_storage.Insert(uid, scroll, out _, storageComp: storage, playSound: false))
            {
                Del(scroll);
                continue;
            }

            _popup.PopupEntity(Loc.GetString("rune-hat-scroll"), wearer, wearer);
        }
    }

    private bool HoldsQuill(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<RuneQuillComponent>(held))
                return true;
        }

        return false;
    }

    private bool IsBlankScroll(EntityUid uid)
    {
        return TryComp<RuneScrollComponent>(uid, out var scroll) && scroll.Runes.Count == 0 && !TerminatingOrDeleted(uid);
    }

    private bool TryGetQuillScroll(Entity<RuneCasterComponent> caster, NetEntity net, out Entity<RuneScrollComponent> scroll)
    {
        scroll = default;
        if (!TryGetEntity(net, out var found) || !IsBlankScroll(found.Value) || !HoldsQuill(caster) ||
            !_blocker.CanInteract(caster, found) || !_interaction.InRangeUnobstructed(caster.Owner, found.Value))
            return false;

        scroll = (found.Value, Comp<RuneScrollComponent>(found.Value));
        return true;
    }

    private void OnQuillInteract(Entity<RuneQuillComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } scroll || !IsBlankScroll(scroll))
            return;

        args.Handled = true;
        if (!HasComp<RuneCasterComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("rune-quill-silent"), args.User, args.User);
            return;
        }

        if (TryComp<ActorComponent>(args.User, out var actor))
            RaiseNetworkEvent(new RuneQuillOpenEvent(GetNetEntity(scroll)), actor.PlayerSession);
    }

    private void OnQuillBurn(RuneQuillBurnMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCaster(args, out var caster) || !TryGetQuillScroll(caster, msg.Scroll, out var scroll))
            return;

        var coords = _transform.GetMapCoordinates(scroll);
        QueueDel(scroll);
        Spawn(AshPrototype, coords);
        _popup.PopupEntity(Loc.GetString("rune-quill-burn-popup"), caster, PopupType.SmallCaution);
    }

    private void OnQuillInscribe(RuneQuillInscribeMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCaster(args, out var caster) || !TryGetQuillScroll(caster, msg.Scroll, out var scroll))
            return;

        if (msg.Runes.Count != ScrollRunes || !ValidChain(caster.Comp, msg.Runes) ||
            !TryCompose(msg.Runes, out var composition) || composition.Spell == null)
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-mismatch"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (composition.Form.Ultimate)
        {
            _popup.PopupEntity(Loc.GetString("rune-inscribe-no-ultimate"), caster, caster, PopupType.SmallCaution);
            return;
        }

        scroll.Comp.Runes = msg.Runes.Select(r => new ProtoId<RunePrototype>(r)).ToList();
        Dirty(scroll);
        _meta.SetEntityName(scroll, Loc.GetString("rune-scroll-name", ("spell", Loc.GetString(composition.Spell.Name))));
        _meta.SetEntityDescription(scroll, Loc.GetString("rune-scroll-desc"));

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(caster):user} inscribed rune scroll {ToPrettyString(scroll):entity} ({string.Join(",", msg.Runes)})");
        _popup.PopupEntity(Loc.GetString("rune-inscribe-done"), caster, caster);
    }

    private void OnScrollUse(Entity<RuneScrollComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Runes.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("rune-scroll-blank"), args.User, args.User);
            return;
        }

        if (!TryComp<RuneCasterComponent>(args.User, out var caster))
        {
            Misfire(ent, args.User);
            return;
        }

        if (!TryComposeScroll(ent, out var composition))
            return;

        if (composition.Spell!.Targeting is not (RuneTargeting.Self or RuneTargeting.EntityOrSelf))
        {
            _popup.PopupEntity(Loc.GetString("rune-scroll-aim"), args.User, args.User);
            return;
        }

        ReadScroll(ent, (args.User, caster), composition, Transform(args.User).Coordinates);
    }

    private void OnScrollInteract(Entity<RuneScrollComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || ent.Comp.Runes.Count == 0 || !TryComp<RuneCasterComponent>(args.User, out var caster))
            return;

        args.Handled = true;
        if (!TryComposeScroll(ent, out var composition))
            return;

        var target = args.Target is { } clicked && HasComp<MobStateComponent>(clicked)
            ? Transform(clicked).Coordinates
            : args.ClickLocation;

        ReadScroll(ent, (args.User, caster), composition, target);
    }

    private bool TryComposeScroll(Entity<RuneScrollComponent> scroll, out RuneComposition composition)
    {
        var chain = scroll.Comp.Runes.Select(r => r.Id).ToList();
        if (!TryCompose(chain, out var composed) || composed.Spell == null)
        {
            composition = null!;
            return false;
        }

        composition = composed;
        return true;
    }

    private void ReadScroll(Entity<RuneScrollComponent> scroll, Entity<RuneCasterComponent> caster, RuneComposition composition, EntityCoordinates target)
    {
        if (composition.Form.Ultimate)
        {
            _popup.PopupEntity(Loc.GetString("rune-inscribe-no-ultimate"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (!_blocker.CanSpeak(caster))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-mute"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (TryCast(caster, composition, target, true))
            QueueDel(scroll);
    }

    private void Misfire(Entity<RuneScrollComponent> scroll, EntityUid user)
    {
        var chain = scroll.Comp.Runes.Select(r => r.Id).ToList();
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} used rune scroll {ToPrettyString(scroll):entity} without magic ({string.Join(",", chain)})");
        QueueDel(scroll);

        if (!TryCompose(chain, out var composition) || composition.Spell is not { } spell || composition.Form.Ultimate || _random.Prob(scroll.Comp.MisfireChance))
        {
            _popup.PopupEntity(Loc.GetString("rune-scroll-misfire"), user, PopupType.MediumCaution);
            if (Proto.TryIndex(MisfireSurge, out var surge) && surge.Entries.Count > 0)
            {
                var entry = _random.Pick(surge.Entries);
                var context = new RuneCastContext(user, Transform(user).Coordinates, user, null);
                foreach (var effect in entry.Effects)
                    Apply(context, effect);
            }

            return;
        }

        var self = Transform(user).Coordinates;
        var near = new RuneCastContext(user, self, user, composition);
        foreach (var effect in spell.Effects)
            Apply(near, effect);
    }
}
