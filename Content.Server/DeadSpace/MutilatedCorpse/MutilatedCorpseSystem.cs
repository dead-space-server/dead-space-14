using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;

namespace Content.Server.DeadSpace.MutilatedCorpse;

/// <summary>
/// This handles changes the character's name to unknown if there is a lot of damage
/// </summary>
public sealed class MutilatedCorpseSystem : EntitySystem
{
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<MutilatedCorpseComponent, MapInitEvent>(OnStartUp);
        SubscribeLocalEvent<MutilatedCorpseComponent, DamageChangedEvent>(OnChangeHealth);
    }

    private void OnStartUp(Entity<MutilatedCorpseComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.RealName = EntityManager.GetComponent<MetaDataComponent>(ent.Owner).EntityName;

        ent.Comp.ChangedName = Loc.GetString(ent.Comp.LocIdChangedName);
    }

    private void OnChangeHealth(Entity<MutilatedCorpseComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp<DamageableComponent>(ent.Owner, out var damageComp))
            return;

        var damageDict = damageComp.Damage.DamageDict;
        var currentName = EntityManager.GetComponent<MetaDataComponent>(ent.Owner).EntityName;

        if (damageDict[ent.Comp.DamageType] >= ent.Comp.AmountDamageForMutilated && _mobState.IsDead(ent.Owner))
        {
            if (currentName == ent.Comp.RealName)
                _meta.SetEntityName(ent.Owner, ent.Comp.ChangedName);
        }
        else
        {
            if (currentName == ent.Comp.ChangedName)
                _meta.SetEntityName(ent.Owner, ent.Comp.RealName);
        }
    }
}
