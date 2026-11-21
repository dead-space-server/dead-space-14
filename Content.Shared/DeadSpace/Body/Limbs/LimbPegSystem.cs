// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbPegSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbPegComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LimbPegComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LimbPegComponent, LimbPegDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<LimbPegComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryStart(ent, args.User, target);
    }

    private void OnUseInHand(Entity<LimbPegComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStart(ent, args.User, args.User);
    }

    private bool TryStart(Entity<LimbPegComponent> ent, EntityUid user, EntityUid target)
    {
        if (!TryComp<LimbBodyComponent>(target, out var limbs))
            return false;

        if (Pick(user, limbs) == null)
        {
            _popup.PopupClient(Loc.GetString("limb-peg-none"), target, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new LimbPegDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        return true;
    }

    private void OnDoAfter(Entity<LimbPegComponent> ent, ref LimbPegDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<LimbBodyComponent>(target, out var limbs))
            return;

        args.Handled = true;
        if (Pick(args.User, limbs) is not { } zone)
        {
            _popup.PopupClient(Loc.GetString("limb-peg-none"), target, args.User);
            return;
        }

        _audio.PlayPredicted(ent.Comp.Sound, target, args.User);
        if (_net.IsClient)
            return;

        if (!Fit(ent, target, zone))
        {
            _popup.PopupEntity(Loc.GetString("limb-peg-none"), target, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("limb-peg-done"), target, args.User);
        QueueDel(ent);
    }

    public bool Fit(Entity<LimbPegComponent> ent, EntityUid target, LimbZone zone)
    {
        if (!TryComp<LimbBodyComponent>(target, out var limbs) || zone is not (LimbZone.LegLeft or LimbZone.LegRight) || !Stump(limbs, zone))
            return false;

        var part = Spawn(zone == LimbZone.LegLeft ? ent.Comp.Left : ent.Comp.Right, Transform(target).Coordinates);
        if (!_limbs.TryReattach(target, zone, part, false))
        {
            QueueDel(part);
            return false;
        }

        var foot = zone == LimbZone.LegLeft ? LimbZone.FootLeft : LimbZone.FootRight;
        foreach (var fitted in new[] { zone, foot })
        {
            _limbs.ApplyEdit(target, fitted, new LimbEdit
            {
                SetHealth = true,
                Health = LimbWoundRules.HealthFor(fitted, limbs),
                SetFlesh = true,
                SetBone = true,
                SetBleed = true,
            });
        }

        if (TryComp<LimbProsthesisComponent>(part, out var prosthesis) && TryComp<HumanoidAppearanceComponent>(target, out var look))
        {
            var leg = zone == LimbZone.LegLeft ? HumanoidVisualLayers.LLeg : HumanoidVisualLayers.RLeg;
            var feet = zone == LimbZone.LegLeft ? HumanoidVisualLayers.LFoot : HumanoidVisualLayers.RFoot;
            _humanoid.SetBaseLayerId(target, leg, prosthesis.Sprite, false, look);
            _humanoid.SetBaseLayerColor(target, leg, Color.White, false, look);
            _humanoid.SetLayerVisibility((target, look), feet, false);
            Dirty(target, look);
        }

        return true;
    }

    private LimbZone? Pick(EntityUid user, LimbBodyComponent limbs)
    {
        if (TryComp<LimbTargetComponent>(user, out var target)
            && target.Selected is LimbZone.LegLeft or LimbZone.LegRight or LimbZone.FootLeft or LimbZone.FootRight)
        {
            var leg = target.Selected is LimbZone.LegLeft or LimbZone.FootLeft ? LimbZone.LegLeft : LimbZone.LegRight;
            if (Stump(limbs, leg))
                return leg;
        }

        if (Stump(limbs, LimbZone.LegLeft))
            return LimbZone.LegLeft;

        return Stump(limbs, LimbZone.LegRight) ? LimbZone.LegRight : null;
    }

    private static bool Stump(LimbBodyComponent limbs, LimbZone zone)
    {
        var index = LimbWoundRules.Index(zone);
        return (uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed;
    }
}
