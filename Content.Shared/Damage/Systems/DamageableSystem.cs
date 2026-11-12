using System.Linq;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageableSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedChemistryGuideDataSystem _chemistryGuideData = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;

    [Dependency] private EntityQuery<AppearanceComponent> _appearanceQuery = default!;
    [Dependency] private EntityQuery<DamageableComponent> _damageableQuery = default!;
    [Dependency] private EntityQuery<InjurableComponent> _injurableQuery = default!;

    public float UniversalAllDamageModifier { get; private set; } = 1f;
    public float UniversalAllHealModifier { get; private set; } = 1f;
    public float UniversalMeleeDamageModifier { get; private set; } = 1f;
    public float UniversalProjectileDamageModifier { get; private set; } = 1f;
    public float UniversalHitscanDamageModifier { get; private set; } = 1f;
    public float UniversalReagentDamageModifier { get; private set; } = 1f;
    public float UniversalReagentHealModifier { get; private set; } = 1f;
    public float UniversalExplosionDamageModifier { get; private set; } = 1f;
    public float UniversalThrownDamageModifier { get; private set; } = 1f;
    public float UniversalTopicalsHealModifier { get; private set; } = 1f;
    public float UniversalMobDamageModifier { get; private set; } = 1f;

    /// <summary>
    ///     If the damage in a DamageableComponent was changed this function should be called.
    /// </summary>
    /// <remarks>
    ///     This updates cached damage information, flags the component as dirty, and raises a damage changed event.
    ///     The damage changed event is used by other systems, such as damage thresholds.
    /// </remarks>
    private void OnEntityDamageChanged(
        Entity<DamageableComponent> ent,
        DamageSpecifier? damageDelta = null,
        bool interruptsDoAfters = true,
        EntityUid? origin = null
    )
    {
        ent.Comp.Damage.GetDamagePerGroup(ProtoMan, ent.Comp.DamagePerGroup);
        ent.Comp.TotalDamage = ent.Comp.Damage.GetTotal();
        Dirty(ent);

        if (damageDelta != null && _appearanceQuery.TryGetComponent(ent, out var appearance))
        {
            _appearance.SetData(
                ent,
                DamageVisualizerKeys.DamageUpdateGroups,
                new DamageVisualizerGroupData(ent.Comp.DamagePerGroup.Keys.ToList()),
                appearance
            );

            if (ent.Comp.Displacement != null)
            {
                _appearance.SetData(
                    ent,
                    DamageVisualizerKeys.Displacement,
                    ent.Comp.Displacement.Value.Id,
                    appearance
                );
            }
            else
            {
                _appearance.RemoveData(ent, DamageVisualizerKeys.Displacement);
            }
        }

        // TODO DAMAGE
        // byref struct event.
        RaiseLocalEvent(ent, new DamageChangedEvent(ent.Comp, damageDelta, interruptsDoAfters, origin));
    }

    private void DamageableGetState(Entity<DamageableComponent> ent, ref ComponentGetState args)
    {
        if (_netMan.IsServer)
        {
<<<<<<< ours
            args.State = new DamageableComponentState(
                ent.Comp.Damage.DamageDict,
                ent.Comp.DamageContainerID,
                ent.Comp.DamageModifierSetId,
                ent.Comp.HealthBarThreshold
            );
            // TODO BODY SYSTEM pass damage onto body system
            // BOBBY WHEN? 😭
            // BOBBY SOON 🫡
||||||| base
            var group = _prototypeManager.Index<DamageGroupPrototype>(damageGroupId);  //get group
            foreach (var type in group.DamageTypes) //go through each type inside that group
            {
                if (!damage.DamageDict.TryGetValue(type, out var damageValue) || damageValue == 0) //get value and make sure it isn't 0
                    continue;
=======
            var group = ProtoMan.Index<DamageGroupPrototype>(damageGroupId);  //get group
            foreach (var type in group.DamageTypes) //go through each type inside that group
            {
                if (!damage.DamageDict.TryGetValue(type, out var damageValue) || damageValue == 0) //get value and make sure it isn't 0
                    continue;
>>>>>>> theirs

            return;
        }

        // avoid mispredicting damage on newly spawned entities.
        args.State = new DamageableComponentState(
            ent.Comp.Damage.DamageDict.ShallowClone(),
            ent.Comp.DamageContainerID,
            ent.Comp.DamageModifierSetId,
            ent.Comp.HealthBarThreshold
        );
    }

    public void CopyComponent(Entity<DamageableComponent?> entity, EntityUid clone)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        // Don't clone current damage values.
        var cloneComp = EnsureComp<DamageableComponent>(clone);
        cloneComp.Displacement = entity.Comp.Displacement;
        cloneComp.DamageModifierSetId = entity.Comp.DamageModifierSetId;
        cloneComp.RadiationDamageTypeIDs = new (entity.Comp.RadiationDamageTypeIDs);
        Dirty(clone, cloneComp);
    }
}
