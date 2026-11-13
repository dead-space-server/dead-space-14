// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Magic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Wizard.Runes;

public sealed partial class RuneCasterSystem : SharedRuneCasterSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;

    private static readonly EntProtoId ScrollPrototype = "WizardRuneScroll";
    private const int ScrollRunes = 2;
    private static readonly ProtoId<RuneSurgePrototype> MisfireSurge = "WizardRuneSurge";
    private const string BloodDamage = "Slash";
    private readonly HashSet<string> _usedThisRound = new();
    private const float FatalMinimumStrain = 35f;
    private const float SyllableInterval = 0.25f;
    private const float FizzleStrain = 3f;
    private const float MismatchStrain = 6f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RuneCasterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RuneCasterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RuneCasterComponent, RuneLearnProductEvent>(OnLearn);
        SubscribeLocalEvent<RuneCasterComponent, RuneInscribeDoAfterEvent>(OnInscribed);
        SubscribeLocalEvent<RuneCasterComponent, RuneScrollActionEvent>(OnScrollAction);
        SubscribeLocalEvent<RuneScrollComponent, UseInHandEvent>(OnScrollUse);
        SubscribeLocalEvent<RuneScrollComponent, ComponentShutdown>(OnScrollShutdown);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<RuneDrawnMessage>(OnDrawn);
        SubscribeNetworkEvent<RuneFizzleMessage>(OnFizzle);
        SubscribeNetworkEvent<RuneCastMessage>(OnCast);

        InitializeEffects();
        InitializeTimeStop();
        InitializeShields();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateTimedComponents();
        UpdateImmortality();
        UpdateShields();
        UpdateRegeneration();
        UpdateBlood(frameTime);
        UpdateTimeStop(frameTime);
        UpdateArena();
    }

    private void OnMapInit(Entity<RuneCasterComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.CircleActionEntity, ent.Comp.CircleAction);
        ent.Comp.Home ??= Transform(ent).Coordinates;

        var pool = ent.Comp.AffinityPool.ToList();
        if (pool.Count >= 2)
        {
            _random.Shuffle(pool);
            ent.Comp.Affinity = pool[0];
            ent.Comp.Weakness = pool[1];
        }

        Dirty(ent);
    }

    private void OnShutdown(Entity<RuneCasterComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.CircleActionEntity);
        foreach (var scroll in ent.Comp.Scrolls.ToArray())
            Unbind(scroll);
    }

    private void OnLearn(Entity<RuneCasterComponent> ent, ref RuneLearnProductEvent args)
    {
        if (!Proto.TryIndex(args.Rune, out var rune) || !ent.Comp.Known.Add(args.Rune))
            return;

        Dirty(ent);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(ent):user} learned rune {args.Rune}");
        _popup.PopupEntity(Loc.GetString("rune-learned", ("rune", Loc.GetString(rune.Name))), ent, ent, PopupType.Medium);
    }

    private bool TryGetCaster(EntitySessionEventArgs args, out Entity<RuneCasterComponent> caster)
    {
        caster = default;
        if (args.SenderSession.AttachedEntity is not { } user || !TryComp<RuneCasterComponent>(user, out var comp))
            return false;

        caster = (user, comp);
        return true;
    }

    private bool ValidChain(RuneCasterComponent caster, IReadOnlyList<string> chain)
    {
        if (chain.Count == 0 || chain.Count > caster.MaxRunes)
            return false;

        var prefix = new List<string>(chain.Count);
        foreach (var id in chain)
        {
            if (!Knows(caster, id) ||
                !Proto.TryIndex<RunePrototype>(id, out var rune) ||
                NextKind(prefix, caster.MaxRunes) != rune.Kind ||
                rune.Fusion && prefix.Count != 2)
                return false;

            prefix.Add(id);
        }

        return true;
    }

    private void OnDrawn(RuneDrawnMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCaster(args, out var caster) ||
            msg.Index != msg.Chain.Count - 1 ||
            msg.Chain[^1] != msg.Rune ||
            !ValidChain(caster.Comp, msg.Chain) ||
            Timing.CurTime < caster.Comp.NextSyllable)
            return;

        caster.Comp.NextSyllable = Timing.CurTime + TimeSpan.FromSeconds(SyllableInterval);

        if (!_blocker.CanInteract(caster, null) || !_blocker.CanSpeak(caster))
            return;

        var runes = ResolveRunes(msg.Chain);
        TryCompose(msg.Chain, out var composition);
        var loudness = ComputeLoudness(runes, composition?.Spell);
        var absurdity = ComputeAbsurdity(runes, composition?.Spell);
        Shout(caster, Loc.GetString(runes[^1].Syllable), loudness, absurdity, msg.Chain.Count);
    }

    private void OnFizzle(RuneFizzleMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCaster(args, out var caster) || Timing.CurTime < caster.Comp.NextSyllable)
            return;

        caster.Comp.NextSyllable = Timing.CurTime + TimeSpan.FromSeconds(SyllableInterval);
        SetStrain(caster, caster.Comp, GetStrain(caster.Comp) + FizzleStrain);
    }

    private void OnCast(RuneCastMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetCaster(args, out var caster))
            return;

        if (!ValidChain(caster.Comp, msg.Runes))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-unknown"), caster, caster, PopupType.SmallCaution);
            return;
        }

        var target = GetCoordinates(msg.Target);

        if (!_blocker.CanInteract(caster, null))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-incapacitated"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (!TryCompose(msg.Runes, out var composition) || composition.Spell == null)
        {
            SetStrain(caster, caster.Comp, GetStrain(caster.Comp) + MismatchStrain);
            _popup.PopupEntity(Loc.GetString("rune-cast-mismatch"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (msg.Inscribe)
        {
            StartInscribe(caster, msg.Runes);
            return;
        }

        if (!_blocker.CanSpeak(caster))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-mute"), caster, caster, PopupType.SmallCaution);
            return;
        }

        TryCast(caster, composition, target, false);
    }

    private bool TryCast(Entity<RuneCasterComponent> caster, RuneComposition composition, EntityCoordinates target, bool scroll)
    {
        var spell = composition.Spell!;
        var ultimate = composition.Form.Ultimate;

        if (!_arenaCastsEnabled && spell.ID == "RuneUltimateArena")
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-arena-disabled"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<RuneArenaFighterComponent>(caster))
        {
            _popup.PopupEntity(Loc.GetString("rune-arena-no-magic"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (ultimate && Timing.CurTime < caster.Comp.UltimateReady)
        {
            var left = (int) MathF.Ceiling((float) (caster.Comp.UltimateReady - Timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("rune-ultimate-cooldown", ("seconds", left)), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (spell.RequiresWounded > 0f && !IsWounded(caster, spell.RequiresWounded))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-not-wounded"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (spell.OncePerRound && _usedThisRound.Contains(spell.ID))
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-once"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (caster.Comp.PortalPending is { } pending && !spell.Effects.Exists(e => e is RunePortalEffect))
        {
            QueueDel(pending);
            caster.Comp.PortalPending = null;
            _popup.PopupEntity(Loc.GetString("rune-portal-fizzled"), caster, caster, PopupType.SmallCaution);
        }

        if (spell.RequiresClothes && !WearsRobes(caster))
        {
            _popup.PopupEntity(Loc.GetString("spell-requirements-failed"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (caster.Comp.Cooldowns.TryGetValue(composition.Key, out var cooldown) && Timing.CurTime < cooldown.Ready)
        {
            var left = (int) MathF.Ceiling((float) (cooldown.Ready - Timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("rune-cast-cooldown", ("seconds", left)), caster, caster, PopupType.SmallCaution);
            return false;
        }

        if (spell.Targeting == RuneTargeting.Self)
        {
            target = Transform(caster).Coordinates;
        }
        else
        {
            var from = _transform.GetMapCoordinates(caster);
            var to = _transform.ToMapCoordinates(target);
            if (from.MapId != to.MapId || (from.Position - to.Position).Length() > spell.Range * composition.RangeScale + 0.5f)
            {
                _popup.PopupEntity(Loc.GetString("rune-cast-range"), caster, caster, PopupType.SmallCaution);
                return false;
            }
        }

        var entity = FindTarget(caster, target, spell.Targeting == RuneTargeting.EntityOrSelf);
        if (entity == null && spell.Targeting is RuneTargeting.Entity or RuneTargeting.EntityOrSelf)
        {
            _popup.PopupEntity(Loc.GetString("rune-cast-no-target"), caster, caster, PopupType.SmallCaution);
            return false;
        }

        var strain = ComputeStrain(caster.Comp, composition) * (scroll ? caster.Comp.ScrollStrainScale : 1f);
        var blood = composition.BloodPrice;
        if (blood > 0f)
        {
            var paid = strain * blood;
            strain -= paid;
            var wound = new DamageSpecifier();
            wound.DamageDict[BloodDamage] = FixedPoint2.New(paid * 0.6f);
            _damageable.TryChangeDamage(caster.Owner, wound, true, origin: caster);
            _popup.PopupEntity(Loc.GetString("rune-blood-price"), caster, caster, PopupType.SmallCaution);
        }

        var before = GetStrain(caster.Comp);
        var after = before + strain;

        if (caster.Comp.LastElement == composition.Element.ID)
        {
            caster.Comp.ElementStreak++;
        }
        else
        {
            caster.Comp.LastElement = composition.Element.ID;
            caster.Comp.ElementStreak = 0;
        }

        StartCooldown(caster, composition.Key, spell.Cooldown, spell.Name);
        if (!scroll)
            SpendBlood(caster, caster.Comp, composition);

        var runes = new List<RunePrototype> { composition.Form, composition.Element };
        if (composition.Second != null)
            runes.Add(composition.Second);
        runes.AddRange(composition.Modifiers);
        Shout(caster, Loc.GetString(spell.Name), ComputeLoudness(runes, spell), ComputeAbsurdity(runes, spell), 7);

        var impact = ultimate ? LogImpact.High : LogImpact.Medium;
        var chain = string.Join(",", runes.Select(r => r.ID));
        var source = scroll ? " from a scroll" : "";
        if (entity is { } victim)
            _adminLog.Add(LogType.Action, impact, $"{ToPrettyString(caster):user} cast rune spell {spell.ID} ({chain}){source} on {ToPrettyString(victim):target}, strain {after:0}/{caster.Comp.MaxStrain:0}");
        else
            _adminLog.Add(LogType.Action, impact, $"{ToPrettyString(caster):user} cast rune spell {spell.ID} ({chain}){source} at {target}, strain {after:0}/{caster.Comp.MaxStrain:0}");

        if (after > caster.Comp.MaxStrain)
        {
            if (ultimate)
                StartUltimateCooldown(caster);

            SetStrain(caster, caster.Comp, caster.Comp.MaxStrain * 0.5f);
            var element = composition.Second != null && _random.Prob(0.5f) ? composition.Second : composition.Element;
            var fatal = after > caster.Comp.MaxStrain * caster.Comp.FatalOverload && strain >= FatalMinimumStrain;
            Surge(caster, fatal ? element.FatalSurge ?? element.Surge : element.Surge);
            return true;
        }

        SetStrain(caster, caster.Comp, after);
        WarnStrain(caster, before / caster.Comp.MaxStrain, after / caster.Comp.MaxStrain);

        var context = new RuneCastContext(caster, target, entity, composition);
        foreach (var effect in spell.Effects)
            Apply(context, effect);

        if (ultimate && !context.KeepUltimateReady)
            StartUltimateCooldown(caster);

        if (spell.OncePerRound && !context.KeepUltimateReady)
            _usedThisRound.Add(spell.ID);

        if (composition.Echo)
        {
            var echo = new RuneCastContext(caster, target, entity, composition, caster.Comp.EchoPower);
            var effects = spell.Effects;
            Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(caster.Comp.EchoDelay), () =>
            {
                if (TerminatingOrDeleted(echo.Caster))
                    return;

                foreach (var effect in effects)
                    Apply(echo, effect);
            });
        }

        return true;
    }

    private void StartCooldown(Entity<RuneCasterComponent> caster, string key, float seconds, string name)
    {
        var now = Timing.CurTime;
        foreach (var (old, cooldown) in caster.Comp.Cooldowns.ToArray())
        {
            if (cooldown.Ready <= now)
                caster.Comp.Cooldowns.Remove(old);
        }

        caster.Comp.Cooldowns[key] = new RuneCooldown(now + TimeSpan.FromSeconds(seconds), seconds, name);
        Dirty(caster);
    }

    private void StartUltimateCooldown(Entity<RuneCasterComponent> caster)
    {
        caster.Comp.UltimateReady = Timing.CurTime + TimeSpan.FromSeconds(caster.Comp.UltimateCooldown);
        Dirty(caster);
    }

    private bool IsWounded(EntityUid uid, float fraction)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable) ||
            !_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var critical))
            return false;

        return damageable.TotalDamage >= critical.Value * fraction;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _usedThisRound.Clear();
    }

    private void WarnStrain(EntityUid caster, float before, float after)
    {
        if (before < 0.75f && after >= 0.75f)
            _popup.PopupEntity(Loc.GetString("rune-strain-high"), caster, caster, PopupType.MediumCaution);
        else if (before < 0.5f && after >= 0.5f)
            _popup.PopupEntity(Loc.GetString("rune-strain-shaking"), caster, caster, PopupType.SmallCaution);
    }

    private void Surge(Entity<RuneCasterComponent> caster, ProtoId<RuneSurgePrototype>? table = null)
    {
        if (!Proto.TryIndex(table ?? caster.Comp.Surge, out var surge) || surge.Entries.Count == 0)
            return;

        var total = surge.Entries.Sum(e => e.Weight);
        var roll = _random.NextFloat() * total;
        var entry = surge.Entries[^1];
        foreach (var candidate in surge.Entries)
        {
            roll -= candidate.Weight;
            if (roll > 0f)
                continue;

            entry = candidate;
            break;
        }

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(caster):user} lost control of a rune spell: surge {surge.ID}, {entry.Popup}");
        _popup.PopupEntity(Loc.GetString(entry.Popup), caster, PopupType.LargeCaution);

        var context = new RuneCastContext(caster, Transform(caster).Coordinates, caster, null);
        foreach (var effect in entry.Effects)
            Apply(context, effect);
    }

    private bool WearsRobes(EntityUid uid)
    {
        var slots = _inventory.GetSlotEnumerator(uid, SlotFlags.OUTERCLOTHING | SlotFlags.HEAD);
        var any = false;
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item || !HasComp<WizardClothesComponent>(item))
                return false;

            any = true;
        }

        return any;
    }

    private EntityUid? FindTarget(EntityUid caster, EntityCoordinates point, bool includeSelf)
    {
        if (includeSelf)
        {
            var self = _transform.GetMapCoordinates(caster);
            var at = _transform.ToMapCoordinates(point);
            if (self.MapId == at.MapId && (self.Position - at.Position).Length() < 0.6f)
                return caster;
        }

        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        var map = _transform.ToMapCoordinates(point);

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(map, 0.9f))
        {
            if (mob.Owner == caster)
                continue;

            var distance = (_transform.GetWorldPosition(mob) - map.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = mob;
            bestDistance = distance;
        }

        return best;
    }

    private void Shout(EntityUid caster, string text, int loudness, int absurdity, int seed)
    {
        var plain = RuneIncantation.Plain(text, loudness, absurdity);
        var markup = RuneIncantation.Markup(text, loudness, absurdity, seed + _random.Next(100));
        _chat.SendIncantation(caster, plain, markup, RuneIncantation.HearingRange(loudness));
    }

    private void StartInscribe(Entity<RuneCasterComponent> caster, List<string> runes)
    {
        if (runes.Count > ScrollRunes)
        {
            _popup.PopupEntity(Loc.GetString("rune-inscribe-too-complex"), caster, caster, PopupType.SmallCaution);
            return;
        }

        if (_hands.GetActiveItem(caster.Owner) is not { } paper || !HasComp<PaperComponent>(paper) || HasComp<RuneScrollComponent>(paper))
        {
            _popup.PopupEntity(Loc.GetString("rune-inscribe-need-paper"), caster, caster, PopupType.SmallCaution);
            return;
        }

        var args = new DoAfterArgs(EntityManager, caster, caster.Comp.InscribeTime, new RuneInscribeDoAfterEvent(runes), caster, paper, paper)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("rune-inscribe-start"), caster, caster);
    }

    private void OnInscribed(Entity<RuneCasterComponent> ent, ref RuneInscribeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } paper || Deleted(paper) || !ValidChain(ent.Comp, args.Runes) || args.Runes.Count > ScrollRunes)
            return;

        args.Handled = true;
        var coords = Transform(paper).Coordinates;
        QueueDel(paper);

        var scroll = Spawn(ScrollPrototype, coords);
        var comp = EnsureComp<RuneScrollComponent>(scroll);
        comp.Runes = args.Runes.Select(r => new ProtoId<RunePrototype>(r)).ToList();
        Dirty(scroll, comp);

        if (TryCompose(args.Runes, out var composition) && composition.Spell != null)
            _meta.SetEntityName(scroll, Loc.GetString("rune-scroll-name", ("spell", Loc.GetString(composition.Spell.Name))));

        _hands.PickupOrDrop(ent, scroll);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(ent):user} inscribed rune scroll {ToPrettyString(scroll):entity} ({string.Join(",", args.Runes)})");
        _popup.PopupEntity(Loc.GetString("rune-inscribe-done"), ent, ent);
    }

    private void OnScrollUse(Entity<RuneScrollComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<RuneCasterComponent>(args.User, out var caster))
        {
            Misfire(ent, args.User);
            return;
        }

        if (ent.Comp.BoundTo == args.User)
        {
            Unbind(ent);
            _popup.PopupEntity(Loc.GetString("rune-scroll-unbound"), args.User, args.User);
            return;
        }

        if (ent.Comp.BoundTo != null)
            Unbind(ent);

        if (caster.Scrolls.Count >= caster.MaxScrolls)
        {
            _popup.PopupEntity(Loc.GetString("rune-scroll-full", ("max", caster.MaxScrolls)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        EntityUid? action = null;
        if (!_actions.AddAction(args.User, ref action, ent.Comp.ActionPrototype))
            return;

        _meta.SetEntityName(action.Value, Name(ent));
        EnsureComp<RuneScrollActionComponent>(action.Value).Scroll = ent;
        ent.Comp.Action = action;
        ent.Comp.BoundTo = args.User;
        caster.Scrolls.Add(ent);
        Dirty(ent);
        Dirty(args.User, caster);
        _popup.PopupEntity(Loc.GetString("rune-scroll-bound"), args.User, args.User);
    }

    private void Misfire(Entity<RuneScrollComponent> scroll, EntityUid user)
    {
        var chain = scroll.Comp.Runes.Select(r => r.Id).ToList();
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} used rune scroll {ToPrettyString(scroll):entity} without magic ({string.Join(",", chain)})");
        QueueDel(scroll);

        if (!TryCompose(chain, out var composition) || composition.Spell == null || _random.Prob(scroll.Comp.MisfireChance))
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
        foreach (var effect in composition.Spell.Effects)
            Apply(near, effect);
    }

    private void OnScrollAction(Entity<RuneCasterComponent> ent, ref RuneScrollActionEvent args)
    {
        if (args.Handled ||
            !TryComp<RuneScrollActionComponent>(args.Action, out var link) ||
            link.Scroll is not { } scroll ||
            !TryComp<RuneScrollComponent>(scroll, out var comp))
            return;

        args.Handled = true;

        if (!IsCarried(scroll, ent))
        {
            _popup.PopupEntity(Loc.GetString("rune-scroll-not-carried"), ent, ent, PopupType.SmallCaution);
            return;
        }

        var chain = comp.Runes.Select(r => r.Id).ToList();
        if (!TryCompose(chain, out var composition) || composition.Spell == null)
            return;

        if (TryCast(ent, composition, args.Target, true) && TryGetCooldown(ent.Comp, composition.Key, out var left, out _))
            _actions.SetCooldown(args.Action.Owner, TimeSpan.FromSeconds(left));
    }

    private bool IsCarried(EntityUid item, EntityUid holder)
    {
        var parent = Transform(item).ParentUid;
        while (parent.IsValid())
        {
            if (parent == holder)
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private void OnScrollShutdown(Entity<RuneScrollComponent> ent, ref ComponentShutdown args)
    {
        Unbind(ent);
    }

    private void Unbind(EntityUid scroll)
    {
        if (!TryComp<RuneScrollComponent>(scroll, out var comp))
            return;

        if (comp.BoundTo is { } owner && TryComp<RuneCasterComponent>(owner, out var caster))
        {
            caster.Scrolls.Remove(scroll);
            Dirty(owner, caster);
        }

        if (comp.Action is { } action)
            _actions.RemoveAction(action);

        comp.Action = null;
        comp.BoundTo = null;
        if (!TerminatingOrDeleted(scroll))
            Dirty(scroll, comp);
    }
}
