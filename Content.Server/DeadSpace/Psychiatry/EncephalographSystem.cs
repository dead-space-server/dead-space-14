// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Psychiatry;

public sealed class EncephalographSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PsychiatrySystem _psychiatry = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private const float HearRange = 6f;
    private const float ScanRange = 4f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EncephalographComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<EncephalographComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeNetworkEvent<PsychiatryUnrealSoundEvent>(OnUnrealSound);
        SubscribeLocalEvent<EncephalographSubjectComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<EncephalographSubjectComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<EncephalographSubjectComponent, MeleeAttackEvent>(OnAttack);
        SubscribeLocalEvent<EncephalographSubjectComponent, BeforeInteractHandEvent>(OnLook);
        SubscribeLocalEvent<EncephalographSubjectComponent, UserInteractUsingEvent>(OnUse);
        SubscribeLocalEvent<EncephalographSubjectComponent, PsychiatryBrainActivityEvent>(OnBrainActivity);
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
        SubscribeNetworkEvent<PsychiatryTypingRequestEvent>(OnTyping);
        _xform.OnGlobalMoveEvent += OnAnyMove;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _xform.OnGlobalMoveEvent -= OnAnyMove;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<EncephalographSubjectComponent>();
        while (query.MoveNext(out var uid, out var subject))
        {
            if (!HasMesh(uid))
            {
                CloseScanners(subject, null);
                continue;
            }

            if (DropIfFar(uid, subject))
                continue;

            var changed = Decay(subject, frameTime);
            if (_timing.CurTime < subject.TypingUntil)
            {
                subject.Activity[PsychiatryBrainRegion.Memory] = 1f;
                changed = true;
            }

            if (FearHeld(uid))
            {
                subject.Activity.TryGetValue(PsychiatryBrainRegion.Fear, out var fear);
                if (fear < 0.9f)
                {
                    subject.Activity[PsychiatryBrainRegion.Fear] = 0.9f;
                    changed = true;
                }
            }

            if (IsSick(uid) && GhostNearby(uid))
            {
                subject.Activity.TryGetValue(PsychiatryBrainRegion.Fear, out var fear);
                if (fear < 0.85f)
                {
                    subject.Activity[PsychiatryBrainRegion.Fear] = 0.85f;
                    changed = true;
                }
            }
            if (!changed || _timing.CurTime < subject.NextUiPush)
                continue;

            subject.NextUiPush = _timing.CurTime + TimeSpan.FromSeconds(0.1);
            Push(uid, subject);
        }
    }

    private void OnAfterInteract(Entity<EncephalographComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!_psychiatry.HasAdvancedTreatment(args.User))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-enceph-no-skill"), args.User, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (!_inventory.TryGetSlotEntity(target, "head", out var head) || !HasComp<NeuroMeshComponent>(head.Value))
        {
            _popup.PopupEntity(Loc.GetString("psychiatry-enceph-no-mesh"), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (!_ui.HasUi(ent.Owner, EncephalographUiKey.Key))
            return;

        if (ent.Comp.ScanTarget is { } previous && previous != target)
            Detach(ent, previous);

        var subject = EnsureComp<EncephalographSubjectComponent>(target);
        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
            subject.Activity.TryAdd(region, 0f);
        subject.Scanners.Add(ent.Owner);
        ent.Comp.ScanTarget = target;

        _ui.OpenUi(ent.Owner, EncephalographUiKey.Key, args.User);
        Push(target, subject);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg"), ent.Owner);
        args.Handled = true;
    }

    private void OnUiClosed(Entity<EncephalographComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not EncephalographUiKey.Key)
            return;
        if (_ui.IsUiOpen(ent.Owner, EncephalographUiKey.Key))
            return;
        if (ent.Comp.ScanTarget is not { } target)
            return;

        Detach(ent, target);
    }

    private void Detach(Entity<EncephalographComponent> scanner, EntityUid target)
    {
        scanner.Comp.ScanTarget = null;
        if (!TryComp<EncephalographSubjectComponent>(target, out var subject))
            return;

        subject.Scanners.Remove(scanner.Owner);
        if (subject.Scanners.Count == 0)
            RemComp<EncephalographSubjectComponent>(target);
    }

    private void OnMove(Entity<EncephalographSubjectComponent> ent, ref MoveEvent args)
    {
        if (!args.NewPosition.Position.Equals(args.OldPosition.Position))
            Pulse(ent, PsychiatryBrainRegion.Movement, 1f);
        else if (!args.NewRotation.Equals(args.OldRotation))
            Pulse(ent, PsychiatryBrainRegion.Vision, 0.7f);
    }

    private void OnDamaged(Entity<EncephalographSubjectComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;
        if (IsSick(ent) || _mobState.IsCritical(ent))
            Pulse(ent, PsychiatryBrainRegion.Fear, 1f);
    }

    private void OnAttack(Entity<EncephalographSubjectComponent> ent, ref MeleeAttackEvent args)
    {
        Pulse(ent, PsychiatryBrainRegion.Arousal, 0.9f);
    }

    private void OnLook(Entity<EncephalographSubjectComponent> ent, ref BeforeInteractHandEvent args)
    {
        Pulse(ent, PsychiatryBrainRegion.Vision, 0.75f);
    }

    private void OnUse(Entity<EncephalographSubjectComponent> ent, ref UserInteractUsingEvent args)
    {
        Pulse(ent, PsychiatryBrainRegion.Vision, 0.8f);
    }

    private void OnBrainActivity(Entity<EncephalographSubjectComponent> ent, ref PsychiatryBrainActivityEvent args)
    {
        Pulse(ent, args.Region, args.Strength);
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        Pulse(args.Source, PsychiatryBrainRegion.Voice, 1f);
        Pulse(args.Source, PsychiatryBrainRegion.Hearing, 0.9f);

        var origin = _xform.GetWorldPosition(args.Source);
        var map = Transform(args.Source).MapID;
        var query = EntityQueryEnumerator<EncephalographSubjectComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == args.Source || xform.MapID != map)
                continue;
            if ((_xform.GetWorldPosition(xform) - origin).LengthSquared() > HearRange * HearRange)
                continue;
            Pulse(uid, PsychiatryBrainRegion.Hearing, 0.85f);
        }
    }

    private void Pulse(EntityUid uid, PsychiatryBrainRegion region, float strength)
    {
        if (!TryComp<EncephalographSubjectComponent>(uid, out var subject))
            return;

        subject.Activity.TryGetValue(region, out var current);
        var next = MathF.Max(current, Math.Clamp(strength, 0f, 1f));
        if (next <= current + 0.02f)
            return;

        subject.Activity[region] = next;
        subject.NextUiPush = TimeSpan.Zero;
    }

    private static bool Decay(EncephalographSubjectComponent subject, float frameTime)
    {
        var changed = false;
        foreach (var region in Enum.GetValues<PsychiatryBrainRegion>())
        {
            subject.Activity.TryGetValue(region, out var value);
            if (value <= 0f)
                continue;

            var next = value * MathF.Exp(-2.4f * frameTime);
            if (next < 0.04f)
                next = 0f;
            if (MathF.Abs(next - value) > 0.001f)
                changed = true;
            subject.Activity[region] = next;
        }

        return changed;
    }

    private void Push(EntityUid target, EncephalographSubjectComponent subject)
    {
        var activity = new Dictionary<PsychiatryBrainRegion, float>(subject.Activity);
        var state = new EncephalographBoundUserInterfaceState(GetNetEntity(target), activity, _psychiatry.IsPositronic(target));
        foreach (var scanner in subject.Scanners)
        {
            if (TerminatingOrDeleted(scanner))
                continue;
            _ui.SetUiState(scanner, EncephalographUiKey.Key, state);
        }
    }

    private bool HasMesh(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "head", out var head) && HasComp<NeuroMeshComponent>(head.Value);
    }

    private void CloseScanners(EncephalographSubjectComponent subject, string? popup)
    {
        var scanners = new List<EntityUid>(subject.Scanners);
        foreach (var scanner in scanners)
        {
            if (popup != null)
                _popup.PopupEntity(Loc.GetString(popup), scanner);
            _ui.CloseUi(scanner, EncephalographUiKey.Key);
        }
    }

    private bool DropIfFar(EntityUid patient, EncephalographSubjectComponent subject)
    {
        var map = Transform(patient).MapID;
        var pos = _xform.GetWorldPosition(patient);
        var scanners = new List<EntityUid>(subject.Scanners);
        foreach (var scanner in scanners)
        {
            if (TryComp(scanner, out TransformComponent? xform)
                && xform.MapID == map
                && (_xform.GetWorldPosition(scanner) - pos).LengthSquared() <= ScanRange * ScanRange)
                continue;

            _popup.PopupEntity(Loc.GetString("psychiatry-enceph-too-far"), scanner);
            _ui.CloseUi(scanner, EncephalographUiKey.Key);
        }

        return !HasComp<EncephalographSubjectComponent>(patient);
    }

    private bool IsSick(EntityUid uid)
    {
        return HasComp<SchizophreniaComponent>(uid);
    }

    private bool GhostNearby(EntityUid uid)
    {
        foreach (var other in _lookup.GetEntitiesInRange(uid, 8f))
        {
            if (other != uid && HasComp<GhostComponent>(other))
                return true;
        }

        return false;
    }

    private void OnUnrealSound(PsychiatryUnrealSoundEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;
        if (!TryComp<SchizophreniaComponent>(uid, out var schizo) || schizo.Stage < SchizophreniaStage.Acute)
            return;

        var gap = TimeSpan.FromSeconds(_cfg.GetCVar(CCCCVars.PsychiatryUnrealSoundCooldownSec));
        if (_timing.CurTime < schizo.NextUnrealSound)
            return;

        schizo.NextUnrealSound = _timing.CurTime + gap;
        Pulse(uid, PsychiatryBrainRegion.Hearing, _cfg.GetCVar(CCCCVars.PsychiatryUnrealHearing));
        Pulse(uid, PsychiatryBrainRegion.Fear, _cfg.GetCVar(CCCCVars.PsychiatryUnrealFear));
    }

    private bool FearHeld(EntityUid uid)
    {
        if (IsSick(uid))
            return IsHealthBelowHalf(uid);

        return _mobState.IsCritical(uid);
    }

    private bool IsHealthBelowHalf(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damage))
            return false;
        return _thresholds.TryGetIncapPercentage(uid, damage.TotalDamage, out var percent)
               && percent != null
               && percent.Value >= 0.5f;
    }

    private void OnTyping(PsychiatryTypingRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;
        if (!TryComp<EncephalographSubjectComponent>(uid, out var subject))
            return;

        if (!ev.Typing)
        {
            subject.TypingUntil = TimeSpan.Zero;
            return;
        }

        subject.TypingUntil = _timing.CurTime + TimeSpan.FromSeconds(0.6);
        Pulse(uid, PsychiatryBrainRegion.Memory, 1f);
    }

    private void OnAnyMove(ref MoveEvent args)
    {
        if (args.NewPosition.Position.Equals(args.OldPosition.Position))
            return;

        var mover = args.Sender;
        var moverXform = args.Component;
        var pos = _xform.GetWorldPosition(moverXform);
        var flying = HasComp<ProjectileComponent>(mover) || HasComp<GhostComponent>(mover);
        var velocity = TryComp<PhysicsComponent>(mover, out var body) ? body.LinearVelocity : Vector2.Zero;

        var query = EntityQueryEnumerator<EncephalographSubjectComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid == mover || xform.MapID != moverXform.MapID)
                continue;

            var to = _xform.GetWorldPosition(xform) - pos;
            var dist2 = to.LengthSquared();
            if (dist2 > 25f || dist2 < 0.04f)
                continue;

            if (IsSick(uid))
                Pulse(uid, PsychiatryBrainRegion.Fear, 0.7f);
            if (!flying || velocity.LengthSquared() < 0.25f)
                continue;

            if (Vector2.Dot(Vector2.Normalize(velocity), Vector2.Normalize(to)) > 0.35f)
                Pulse(uid, PsychiatryBrainRegion.Arousal, 0.85f);
        }
    }
}
