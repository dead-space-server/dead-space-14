// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
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
        SubscribeLocalEvent<MutilatedCorpseComponent, EntityRenamedEvent>(OnStartUp);
        SubscribeLocalEvent<MutilatedCorpseComponent, IdentityChangedEvent>(OnIdentityChanged);
        SubscribeLocalEvent<MutilatedCorpseComponent, DamageChangedEvent>(OnChangeHealth);
    }

    private void OnStartUp(Entity<MutilatedCorpseComponent> ent, ref EntityRenamedEvent args)
    {
        ent.Comp.RealName = EntityManager.GetComponent<MetaDataComponent>(ent.Owner).EntityName;
        ent.Comp.ChangedName = Loc.GetString(ent.Comp.LocIdChangedName);

    }

        private void OnIdentityChanged(Entity<MutilatedCorpseComponent> ent, ref IdentityChangedEvent args)
    {
        var currentIdentity = EntityManager.GetComponent<MetaDataComponent>(args.IdentityEntity);

        if (currentIdentity.EntityName == ent.Comp.ChangedName ||
            currentIdentity.EntityName == ent.Comp.RealName)
            ent.Comp.IdentityIsHidden = false;
        else
            ent.Comp.IdentityIsHidden = true;

        TryChangeName(ent.Owner, ent.Comp);
    }

    private void OnChangeHealth(Entity<MutilatedCorpseComponent> ent, ref DamageChangedEvent args)
    {
        TryChangeName(ent.Owner, ent.Comp);
    }

    private void TryChangeName(EntityUid uid, MutilatedCorpseComponent comp)
    {
        if (comp.IdentityIsHidden)
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageComp))
            return;

        var damageDict = damageComp.Damage.DamageDict;

        if (!TryComp<IdentityComponent>(uid, out var identityComp))
            return;

        if (identityComp.IdentityEntitySlot.ContainedEntity is not { } ident)
            return;

        if (damageDict[comp.DamageType] >= comp.AmountDamageForMutilated && _mobState.IsDead(uid))
        {
            _meta.SetEntityName(uid, comp.ChangedName, raiseEvents: false);
            _meta.SetEntityName(ident, comp.ChangedName, raiseEvents: false); //for examination
        }
        else
        {
            _meta.SetEntityName(uid, comp.RealName, raiseEvents: false);
            _meta.SetEntityName(ident, comp.RealName, raiseEvents: false); //for examination
        }
    }
}
