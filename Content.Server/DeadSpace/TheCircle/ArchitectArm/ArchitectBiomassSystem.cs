// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.TheCircle.ArchitectArm;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.TheCircle.ArchitectArm;

/// <summary>
/// Alt-clicking a corpse lets the Architect drain its biomass.
/// </summary>
public sealed partial class ArchitectBiomassSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private LimbWoundSystem _limbs = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Effects/Changeling/devour_windup.ogg");
    private static readonly SoundSpecifier DrainSound = new SoundPathSpecifier("/Audio/Effects/Changeling/devour_suck.ogg");
    private static readonly SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Effects/Changeling/devour_consume.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ArchitectBiomassComponent, ArchitectAbsorbBiomassDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<BodyComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner ||
            !TryComp<ArchitectBiomassComponent>(args.User, out var biomass) || !CanDrain(ent))
        {
            return;
        }

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("architect-biomass-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Interface/Actions/devour.rsi"), "icon"),
            // Above the other corpse verbs, so a plain alt-click drains the body.
            Priority = 10,
            Act = () => TryStartDrain((user, biomass), target),
        });
    }

    private void TryStartDrain(Entity<ArchitectBiomassComponent> ent, EntityUid target)
    {
        if (!CanDrain(target))
        {
            _popup.PopupEntity(Loc.GetString("architect-biomass-invalid"), ent, ent);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.Delay, new ArchitectAbsorbBiomassDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("architect-biomass-start", ("target", target)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(StartSound, ent);
        _audio.PlayPvs(DrainSound, target);
    }

    private bool CanDrain(EntityUid target)
    {
        return HasComp<BodyComponent>(target) && _mobState.IsDead(target) && !HasComp<ArchitectBiomassDrainedComponent>(target);
    }

    private void OnDoAfter(Entity<ArchitectBiomassComponent> ent, ref ArchitectAbsorbBiomassDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanDrain(target))
            return;

        args.Handled = true;
        AddComp<ArchitectBiomassDrainedComponent>(target);

        RefillArm(ent, ent.Comp.Charges);
        MendSome(ent, ent.Comp);
        _audio.PlayPvs(DoneSound, ent);
        _popup.PopupEntity(Loc.GetString("architect-biomass-done", ("target", target)), ent, PopupType.Medium);
    }

    /// <summary>
    /// The arm is either held or tucked away in its action, so it sits at most a few parents above the Architect.
    /// </summary>
    private void RefillArm(EntityUid architect, int charges)
    {
        var query = EntityQueryEnumerator<ArchitectArmComponent, LimitedChargesComponent, TransformComponent>();
        while (query.MoveNext(out var arm, out _, out var charge, out var xform))
        {
            var parent = xform.ParentUid;
            for (var depth = 0; depth < 3 && parent.IsValid(); depth++)
            {
                if (parent == architect)
                {
                    _charges.AddCharges((arm, charge, null), charges);
                    break;
                }

                parent = Transform(parent).ParentUid;
            }
        }
    }

    private void MendSome(EntityUid body, ArchitectBiomassComponent biomass)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        var zones = Math.Min(limbs.Zones.Count, LimbWoundRules.BodyZoneCount);
        for (var i = 0; i < zones; i++)
        {
            var zone = limbs.Zones[i];
            if (zone.Severed || zone.Bone <= 0f && zone.Bleed <= 0f)
                continue;

            _limbs.ApplyEdit(body, (LimbZone) i, new LimbEdit
            {
                Bone = -biomass.BoneMend,
                Bleed = -biomass.BleedMend,
            });
        }

        var organs = Math.Min(limbs.Organs.Count, LimbOrganRules.Count);
        for (var i = 0; i < organs; i++)
        {
            var organ = limbs.Organs[i];
            if (organ.Ruptured || organ.Missing || organ.Damage <= 0f)
                continue;

            _limbs.ApplyOrganEdit(body, (LimbOrgan) i, new LimbOrganEdit { Damage = -biomass.OrganMend });
        }
    }
}
