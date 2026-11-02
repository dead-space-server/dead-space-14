// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Surgery;

public sealed class LimbRestorerSystem : EntitySystem
{
    private static readonly LimbZone[] RegrowOrder =
    {
        LimbZone.ArmLeft,
        LimbZone.ArmRight,
        LimbZone.LegLeft,
        LimbZone.LegRight,
        LimbZone.HandLeft,
        LimbZone.HandRight,
        LimbZone.FootLeft,
        LimbZone.FootRight,
    };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SurgeryLimbPort _port = default!;
    [Dependency] private readonly Content.Server.DeadSpace.Body.LimbHearingSystem _hearing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LimbRestorerComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LimbRestorerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<LimbRestorerComponent, LimbRestorerPrimeDoAfterEvent>(OnPrimed);
        SubscribeLocalEvent<LimbRestorerComponent, LimbRestorerApplyDoAfterEvent>(OnApplied);
        SubscribeLocalEvent<LimbRestorerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<LimbRestorerComponent, LimbRestorerImplantEvent>(OnImplantUsed);
        SubscribeLocalEvent<LimbRestorerComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnImplantedState);
    }

    private void OnImplantUsed(Entity<LimbRestorerComponent> ent, ref LimbRestorerImplantEvent args)
    {
        if (args.Handled || !TryComp<SubdermalImplantComponent>(ent, out var implant) || implant.ImplantedEntity is not { } target)
            return;

        args.Handled = Trigger(ent, target);
    }

    private void OnImplantedState(EntityUid uid, LimbRestorerComponent restorer, ImplantRelayEvent<MobStateChangedEvent> args)
    {
        if (!restorer.AutoCrit || args.Event.NewMobState != MobState.Critical || restorer.Charges <= 0)
            return;

        Trigger((uid, restorer), args.ImplantedEntity);
    }

    private bool Trigger(Entity<LimbRestorerComponent> ent, EntityUid target)
    {
        if (ent.Comp.Charges <= 0)
        {
            _popup.PopupEntity(Loc.GetString("limb-restorer-implant-empty"), target, target);
            return false;
        }

        Restore(target, ent.Comp);
        if (ent.Comp.Cost != null)
            _damage.TryChangeDamage(target, ent.Comp.Cost, true, false);

        _audio.PlayPvs(ent.Comp.UseSound, target);
        _popup.PopupEntity(Loc.GetString(ent.Comp.DoneLine), target, target);
        ent.Comp.Charges--;
        Dirty(ent);
        return true;
    }

    private bool Primed(LimbRestorerComponent restorer)
    {
        return restorer.PrimedUntil > _timing.CurTime;
    }

    private void OnExamined(Entity<LimbRestorerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("limb-restorer-charges", ("count", ent.Comp.Charges)));
        if (Primed(ent.Comp))
            args.PushMarkup(Loc.GetString("limb-restorer-examine-primed"));
    }

    private void OnUseInHand(Entity<LimbRestorerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Charges <= 0)
            return;

        args.Handled = true;
        if (Primed(ent.Comp))
        {
            if (ent.Comp.AllowSelf)
                StartApply(ent, args.User, args.User);
            else
                _popup.PopupEntity(Loc.GetString("limb-restorer-no-self"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(ent.Comp.Prime), new LimbRestorerPrimeDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnAfterInteract(Entity<LimbRestorerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<LimbBodyComponent>(target))
            return;

        args.Handled = true;
        if (ent.Comp.Charges <= 0)
            return;

        if (!Primed(ent.Comp))
        {
            _popup.PopupEntity(Loc.GetString("limb-restorer-not-primed"), args.User, args.User);
            return;
        }

        if (target == args.User && !ent.Comp.AllowSelf)
        {
            _popup.PopupEntity(Loc.GetString("limb-restorer-no-self"), args.User, args.User);
            return;
        }

        StartApply(ent, args.User, target);
    }

    private void StartApply(Entity<LimbRestorerComponent> ent, EntityUid user, EntityUid target)
    {
        var delay = target == user ? ent.Comp.SelfDelay : ent.Comp.Delay;
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(delay), new LimbRestorerApplyDoAfterEvent(), ent.Owner, target: target, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            MovementThreshold = 0.5f,
        });
    }

    private void OnPrimed(Entity<LimbRestorerComponent> ent, ref LimbRestorerPrimeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        ent.Comp.PrimedUntil = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.PrimeWindow);
        Dirty(ent);
        _audio.PlayPvs(ent.Comp.PrimeSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString(ent.Comp.PrimedLine), args.User, args.User);
    }

    private void OnApplied(Entity<LimbRestorerComponent> ent, ref LimbRestorerApplyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.Charges <= 0 || !Primed(ent.Comp))
            return;

        args.Handled = true;
        Restore(target, ent.Comp);
        if (ent.Comp.Cost != null)
            _damage.TryChangeDamage(target, ent.Comp.Cost, true, false);

        _audio.PlayPvs(ent.Comp.UseSound, target);
        _popup.PopupEntity(Loc.GetString(ent.Comp.DoneLine), target, args.User);
        ent.Comp.Charges--;
        ent.Comp.PrimedUntil = TimeSpan.Zero;
        Dirty(ent);
        if (ent.Comp.Charges <= 0 && ent.Comp.DeleteEmpty)
            QueueDel(ent);
    }

    private void Restore(EntityUid patient, LimbRestorerComponent restorer)
    {
        if (!TryComp<LimbBodyComponent>(patient, out var limbs))
            return;

        var heal = restorer.Heal;
        if (heal.Contains(LimbRestore.Limbs))
        {
            foreach (var zone in RegrowOrder)
            {
                var index = LimbWoundRules.Index(zone);
                if ((uint) index < (uint) limbs.Zones.Count && limbs.Zones[index].Severed)
                    _limbs.TryRegrowZone(patient, zone, restorer.Regrow);
            }
        }

        foreach (var zone in Enum.GetValues<LimbZone>())
        {
            if (!LimbWoundRules.IsBody(zone))
                continue;

            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) limbs.Zones.Count || limbs.Zones[index].Severed)
                continue;

            var state = limbs.Zones[index];
            var edit = new LimbEdit();
            if (heal.Contains(LimbRestore.Bleed))
            {
                edit.SetBleed = true;
                edit.Bleed = 0f;
            }

            if (heal.Contains(LimbRestore.Fracture))
            {
                edit.SetBone = true;
                edit.Bone = 0f;
            }

            if (heal.Contains(LimbRestore.Flesh))
            {
                edit.SetFlesh = true;
                edit.Flesh = 0f;
            }

            if (heal.Contains(LimbRestore.Skin))
            {
                edit.Skin = -state.Skin;
                edit.ClearSkinBare = true;
            }

            if (heal.Contains(LimbRestore.Shard))
                edit.ClearEmbedded = true;

            var floor = LimbWoundRules.HealthFor(zone, limbs) * restorer.Health;
            if (heal.Contains(LimbRestore.Health) && state.Health < floor)
            {
                edit.SetHealth = true;
                edit.Health = floor;
            }

            if (heal.Contains(LimbRestore.Atrophy))
            {
                edit.ClearAtrophy = true;
                if (state.Atrophied && !edit.SetHealth)
                {
                    edit.SetHealth = true;
                    edit.Health = MathF.Max(state.Health, floor);
                }
            }

            _limbs.ApplyEdit(patient, zone, edit);
        }

        foreach (var organ in Enum.GetValues<LimbOrgan>())
        {
            var bit = (LimbOrganMask) (1 << (int) organ);
            var index = (int) organ;
            if ((limbs.Present & bit) == 0 || (uint) index >= (uint) limbs.Organs.Count)
                continue;

            var missing = limbs.Organs[index].Missing || organ == LimbOrgan.Eyes && limbs.EyesGone > 0;
            if (missing)
            {
                if (heal.Contains(LimbRestore.MissingOrgans) && organ is not (LimbOrgan.Brain or LimbOrgan.Core))
                {
                    if (!limbs.Organs[index].Missing)
                        _limbs.ApplyOrganEdit(patient, organ, new LimbOrganEdit { EyesGone = -2 });
                    else
                        _port.Regrow(patient, organ);
                }

                continue;
            }

            if (heal.Contains(LimbRestore.Organs))
            {
                _limbs.ApplyOrganEdit(patient, organ, new LimbOrganEdit
                {
                    SetDamage = true,
                    Damage = 0f,
                    SetRuptured = true,
                    Ruptured = false,
                });
            }
        }

        if (heal.Contains(LimbRestore.Organs) && !limbs.EarsGone)
            _hearing.Damage(patient, -limbs.Ears, limbs);

        if (heal.Contains(LimbRestore.MissingOrgans) && limbs.EarsGone)
        {
            _hearing.SetGone(patient, false, limbs);
            if (TryComp<SurgeryPartsGoneComponent>(patient, out var gone) && (gone.Parts.Remove("Ears") | gone.Parts.Remove("EarsMachine")))
                Dirty(patient, gone);
        }

        if (heal.Contains(LimbRestore.Infection))
            RemComp<SurgeryInfectionComponent>(patient);

        if (heal.Contains(LimbRestore.Rejection))
            RemComp<SurgeryRejectionComponent>(patient);
    }
}
