using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Damage.Components; // DS14
using Content.Shared.Inventory; // DS14
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed partial class DamageContactsSystem : EntitySystem
{
    private const string OuterClothingSlot = "outerClothing"; // DS14

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private InventorySystem _inventory = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageContactsComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<DamageContactsComponent, EndCollideEvent>(OnEntityExit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DamagedByContactComponent>();

        while (query.MoveNext(out var ent, out var damaged))
        {
            if (_timing.CurTime < damaged.NextSecond)
                continue;

            damaged.NextSecond = _timing.CurTime + TimeSpan.FromSeconds(1);

            // DS14-start
            if (IsProtectedFromContactDamage(ent))
                continue;
            // DS14-end

            if (damaged.Damage != null)
                _damageable.TryChangeDamage(ent, damaged.Damage, interruptsDoAfters: false);
        }
    }

    // DS14-start
    private bool IsProtectedFromContactDamage(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, OuterClothingSlot, out var outerClothing)
               && HasComp<IgnoreContactDamageComponent>(outerClothing.Value);
    }
    // DS14-end

    private void OnEntityExit(EntityUid uid, DamageContactsComponent component, ref EndCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (!TryComp<PhysicsComponent>(otherUid, out var body))
            return;

        var damageQuery = GetEntityQuery<DamageContactsComponent>();
        foreach (var ent in _physics.GetContactingEntities(otherUid, body))
        {
            if (ent == uid)
                continue;

            if (damageQuery.HasComponent(ent))
                return;
        }

        RemComp<DamagedByContactComponent>(otherUid);
    }

    private void OnEntityEnter(EntityUid uid, DamageContactsComponent component, ref StartCollideEvent args)
    {
        var otherUid = args.OtherEntity;

        if (HasComp<DamagedByContactComponent>(otherUid))
            return;

        if (_whitelistSystem.IsWhitelistPass(component.IgnoreWhitelist, otherUid))
            return;

        var damagedByContact = EnsureComp<DamagedByContactComponent>(otherUid);
        damagedByContact.Damage = component.Damage;
    }
}
