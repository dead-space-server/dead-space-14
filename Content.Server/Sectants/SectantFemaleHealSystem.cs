using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.Sectants;
using Robust.Shared.Prototypes;

namespace Content.Server.Sectants;

public sealed class SectantFemaleHealSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly HashSet<EntityUid> _entitySet = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantFemaleHealActionEvent>(OnHeal);
    }

    private void OnHeal(SectantFemaleHealActionEvent args)
    {
        var uid = args.Performer;
        var coords = Transform(uid).Coordinates;

        _entitySet.Clear();
        _lookup.GetEntitiesInRange(coords, 5f, _entitySet);

        foreach (var ally in _entitySet)
        {
            if (ally == uid) continue;
            if (!AreInSameFaction(uid, ally)) continue;

            var heal = new DamageSpecifier();
            heal.DamageDict[_proto.Index<DamageTypePrototype>("Poison").ID]    = -50;
            heal.DamageDict[_proto.Index<DamageTypePrototype>("Bloodloss").ID] = -50;
            heal.DamageDict[_proto.Index<DamageTypePrototype>("Radiation").ID] = -50;
            _damage.TryChangeDamage(ally, heal, ignoreResistances: true);
            Spawn("SectantHealEffect", Transform(ally).Coordinates);
        }

        args.Handled = true;
    }

    /// <summary>Ручная проверка общих фракций — обходит RA0002.</summary>
    private bool AreInSameFaction(EntityUid a, EntityUid b)
    {
        if (!TryComp<NpcFactionMemberComponent>(a, out var ca)) return false;
        if (!TryComp<NpcFactionMemberComponent>(b, out var cb)) return false;

        foreach (var fa in ca.Factions)
        foreach (var fb in cb.Factions)
            if (fa == fb) return true;

        return false;
    }
}
