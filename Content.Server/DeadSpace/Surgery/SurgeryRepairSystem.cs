// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

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

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
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
            return;
        }

        if (args.Handled || args.Effect != Effect || !LimbWoundRules.IsBody(ent.Comp.Zone))
            return;

        args.Handled = true;
        var zone = ent.Comp.Zone;
        _limbs.ApplyEdit(ent, zone, new LimbEdit
        {
            SetHealth = true,
            Health = LimbWoundRules.HealthFor(zone),
            SetBone = true,
            Bone = 0f,
            SetFlesh = true,
            Flesh = 0f,
            SetBleed = true,
            Bleed = 0f,
            Skin = -1000f,
            ClearAtrophy = true,
        });

        if (_prototype.TryIndex<SurgeryRepairPrototype>("Default", out var rules))
            _damage.TryChangeDamage(ent.Owner, rules.Heal, true, false);
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
