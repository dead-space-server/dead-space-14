// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbSplintSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly LimbZone[] Splintable =
    {
        LimbZone.ArmLeft, LimbZone.ArmRight, LimbZone.HandLeft, LimbZone.HandRight,
        LimbZone.LegLeft, LimbZone.LegRight, LimbZone.FootLeft, LimbZone.FootRight,
    };

    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbSplintComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LimbSplintComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LimbSplintComponent, LimbSplintDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<LimbSplintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryStart(ent, args.User, target);
    }

    private void OnUseInHand(Entity<LimbSplintComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStart(ent, args.User, args.User);
    }

    private bool TryStart(Entity<LimbSplintComponent> ent, EntityUid user, EntityUid target)
    {
        if (!TryComp<LimbBodyComponent>(target, out var limbs))
            return false;

        if (Pick(user, limbs) == null)
        {
            _popup.PopupClient(Loc.GetString("limb-splint-none"), target, user);
            return true;
        }

        var delay = user == target ? ent.Comp.SelfDelay : ent.Comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new LimbSplintDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private void OnDoAfter(Entity<LimbSplintComponent> ent, ref LimbSplintDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<LimbBodyComponent>(target, out var limbs))
            return;

        args.Handled = true;
        if (Pick(args.User, limbs) is not { } zone)
        {
            _popup.PopupClient(Loc.GetString("limb-splint-none"), target, args.User);
            return;
        }

        _audio.PlayPredicted(ent.Comp.Sound, target, args.User);
        if (_net.IsClient)
            return;

        Apply(ent, target, zone);
        _popup.PopupEntity(Loc.GetString("limb-splint-done"), target, args.User);
        QueueDel(ent);
    }

    public bool Apply(Entity<LimbSplintComponent> ent, EntityUid target, LimbZone zone)
    {
        if (!_limbs.SplintZone(target, zone))
            return false;

        var knitting = EnsureComp<LimbKnittingComponent>(target);
        knitting.Knit = ent.Comp.Knit;
        knitting.Due[zone] = _timing.CurTime + ent.Comp.Knit;
        return true;
    }

    private LimbZone? Pick(EntityUid user, LimbBodyComponent limbs)
    {
        if (TryComp<LimbTargetComponent>(user, out var target)
            && target.Selected is { } selected
            && Array.IndexOf(Splintable, selected) >= 0
            && Needs(limbs, selected))
            return selected;

        LimbZone? best = null;
        var worst = 0f;
        foreach (var zone in Splintable)
        {
            if (!Needs(limbs, zone))
                continue;

            var bone = limbs.Zones[LimbWoundRules.Index(zone)].Bone;
            if (bone <= worst)
                continue;

            worst = bone;
            best = zone;
        }

        return best;
    }

    private static bool Needs(LimbBodyComponent limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count)
            return false;

        var state = limbs.Zones[index];
        return !state.Severed && !state.Splinted && state.Bone >= LimbWoundRules.StrainBone;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_net.IsClient || _timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
        var query = EntityQueryEnumerator<LimbKnittingComponent, LimbBodyComponent>();
        var done = new List<LimbZone>();
        var again = new List<LimbZone>();
        while (query.MoveNext(out var uid, out var knitting, out var limbs))
        {
            done.Clear();
            again.Clear();
            foreach (var (zone, due) in knitting.Due)
            {
                var index = LimbWoundRules.Index(zone);
                if ((uint) index >= (uint) limbs.Zones.Count)
                {
                    done.Add(zone);
                    continue;
                }

                var state = limbs.Zones[index];
                if (state.Severed || !state.Splinted || state.Bone <= 0f)
                {
                    done.Add(zone);
                    continue;
                }

                if (_timing.CurTime < due)
                    continue;

                if (state.Bone >= LimbWoundRules.BreakBone)
                    again.Add(zone);
                else
                    done.Add(zone);
            }

            foreach (var zone in again)
            {
                _limbs.ApplyEdit(uid, zone, new LimbEdit { SetBone = true, Bone = LimbWoundRules.StrainBone });
                knitting.Due[zone] = _timing.CurTime + knitting.Knit;
            }

            foreach (var zone in done)
            {
                var index = LimbWoundRules.Index(zone);
                if ((uint) index < (uint) limbs.Zones.Count
                    && limbs.Zones[index] is { Severed: false, Splinted: true } state
                    && state.Bone > 0f
                    && knitting.Due[zone] <= _timing.CurTime)
                    _limbs.ApplyEdit(uid, zone, new LimbEdit { SetBone = true, Bone = 0f });

                knitting.Due.Remove(zone);
            }

            if (knitting.Due.Count == 0)
                RemCompDeferred<LimbKnittingComponent>(uid);
        }
    }
}
