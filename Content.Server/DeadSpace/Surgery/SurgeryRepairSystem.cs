// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryRepairSystem : EntitySystem
{
    private const string Effect = "MachineRepair";
    private const string OrganMend = "OrganMend";
    private const string TissueMend = "TissueMend";
    private const string RotMend = "RotMend";
    private const string Rules = "Default";

    private static readonly Dictionary<string, string[]> ZoneHeals = new()
    {
        ["BruiseMend"] = new[] { "Blunt" },
        ["CutMend"] = new[] { "Slash" },
        ["PunctureMend"] = new[] { "Piercing" },
        ["FractureClosedMend"] = new[] { "Blunt" },
        ["FractureOpenMend"] = new[] { "Blunt", "Slash" },
        ["ShardMend"] = new[] { "Piercing" },
    };

    private static readonly string[] OrganMendHeals = { "Piercing", "Slash" };

    private static readonly HashSet<string> ClosesWound = new() { "CutMend", "PunctureMend", "FractureOpenMend" };

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedRottingSystem _rotting = default!;
    [Dependency] private readonly Content.Server.DeadSpace.Body.LimbHearingSystem _hearing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgerySiteComponent, SurgeryEffectEvent>(OnEffect);
    }

    private void OnEffect(Entity<SurgerySiteComponent> ent, ref SurgeryEffectEvent args)
    {
        if (!args.Handled && args.Effect is "EarsOut" or "EarsIn")
        {
            args.Handled = true;
            _hearing.SetGone(ent, args.Effect == "EarsOut");
            return;
        }

        if (!args.Handled && args.Effect == OrganMend)
        {
            args.Handled = true;
            MendOrgans(ent, args.User);
            HealZone(ent, ent.Comp.Zone, OrganMendHeals);
            return;
        }

        if (!args.Handled && args.Effect == RotMend)
        {
            args.Handled = true;
            MendRot(ent, ent.Comp.Zone);
            return;
        }

        if (!args.Handled && args.Effect == TissueMend && LimbWoundRules.IsBody(ent.Comp.Zone))
        {
            args.Handled = true;
            _limbs.ApplyEdit(ent, ent.Comp.Zone, new LimbEdit
            {
                SetHealth = true,
                Health = LimbWoundRules.HealthFor(ent.Comp.Zone, CompOrNull<LimbBodyComponent>(ent)),
                SetFlesh = true,
                Flesh = 0f,
            });
            return;
        }

        if (!args.Handled && ZoneHeals.TryGetValue(args.Effect, out var types))
        {
            args.Handled = true;
            HealZone(ent, ent.Comp.Zone, types);
            if (ClosesWound.Contains(args.Effect) && LimbWoundRules.IsBody(ent.Comp.Zone))
                _limbs.ApplyEdit(ent, ent.Comp.Zone, new LimbEdit { SetFlesh = true, Flesh = 0f });
            return;
        }

        if (args.Handled || args.Effect != Effect || !LimbWoundRules.IsBody(ent.Comp.Zone))
            return;

        args.Handled = true;
        var zone = ent.Comp.Zone;
        _limbs.ApplyEdit(ent, zone, new LimbEdit
        {
            SetHealth = true,
            Health = LimbWoundRules.HealthFor(zone, CompOrNull<LimbBodyComponent>(ent)),
            SetBone = true,
            Bone = 0f,
            SetFlesh = true,
            Flesh = 0f,
            SetBleed = true,
            Bleed = 0f,
            Skin = -1000f,
            ClearAtrophy = true,
        });

        if (_prototype.TryIndex<SurgeryRepairPrototype>(Rules, out var rules))
            _damage.TryChangeDamage(ent.Owner, rules.Heal, true, false);
    }

    private void MendRot(EntityUid body, LimbZone zone)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs) || !SurgeryRot.Treatable(EntityManager, body, zone, limbs))
            return;

        if (zone == LimbZone.Head)
        {
            limbs.Decay = 0f;
            _limbs.ApplyOrganEdit(body, LimbOrgan.Brain, new LimbOrganEdit { SetDamage = true, Damage = 0f });
            return;
        }

        if (zone != LimbZone.Torso || !TryComp<PerishableComponent>(body, out var perishable))
            return;

        var total = perishable.RotAccumulator + (CompOrNull<RottingComponent>(body)?.TotalRotTime ?? TimeSpan.Zero);
        _rotting.ReduceAccumulator(body, total);
    }

    public void HealZone(EntityUid body, LimbZone zone, IReadOnlyList<string> types)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(body, out var limbs))
            return;

        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count || limbs.Zones[index].Severed)
            return;

        var heal = new DamageSpecifier();
        foreach (var type in types)
        {
            var have = _limbs.LedgerFor(limbs.Zones[index], type);
            if (have > 0f)
                heal.DamageDict[type] = -have;
        }

        if (heal.DamageDict.Count == 0)
            return;

        limbs.LedgerZone = zone;
        _damage.TryChangeDamage(body, heal, true, false);
        limbs.LedgerZone = null;
    }

    private void MendOrgans(Entity<SurgerySiteComponent> ent, EntityUid user)
    {
        if (!TryComp<LimbBodyComponent>(ent, out var limbs))
            return;

        var zone = ent.Comp.Zone;
        var torn = false;
        for (var i = 0; i < limbs.Organs.Count && i < LimbOrganRules.Count; i++)
        {
            var organ = (LimbOrgan) i;
            var state = limbs.Organs[i];
            if (state.Ruptured && !state.Missing && organ != LimbOrgan.Eyes && LimbOrganHomes.Home(organ) == zone)
                torn = true;

            if (SurgeryConditions.Mendable(organ, zone, state))
                _limbs.ApplyOrganEdit(ent, organ, new LimbOrganEdit { SetDamage = true, Damage = 0f });
        }

        _popup.PopupEntity(Loc.GetString(torn ? "surgery-organ-mend-torn" : "surgery-organ-mend-done"), ent, user);
    }
}
