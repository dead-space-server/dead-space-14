using Content.Shared.Atmos; // DS14
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Damage.Components; // DS14
using Content.Shared.FixedPoint; // DS14
using Content.Shared.Inventory; // DS14
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed class DamageContactsSystem : EntitySystem
{
    private const string OuterClothingSlot = "outerClothing"; // DS14

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!; // DS14

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
                ApplyContactDamage(ent, damaged.Damage);
        }
    }

    // DS14-start
    private void ApplyContactDamage(EntityUid target, DamageSpecifier damage)
    {
        const string heatType = "Heat";

        if (damage.DamageDict.TryGetValue(heatType, out var heat) && heat > FixedPoint2.Zero)
        {
            var protection = new GetFireProtectionEvent();
            RaiseLocalEvent(target, ref protection);
            if (TryComp<InventoryComponent>(target, out var inventory))
                _inventory.RelayEvent((target, inventory), ref protection);

            if (protection.Multiplier < 1f)
            {
                var reduced = new DamageSpecifier(damage);
                var scaled = heat * protection.Multiplier;
                if (scaled <= FixedPoint2.Zero)
                    reduced.DamageDict.Remove(heatType);
                else
                    reduced.DamageDict[heatType] = scaled;

                if (reduced.Empty)
                    return;

                damage = reduced;
            }
        }

        _damageable.TryChangeDamage(target, damage, interruptsDoAfters: false);
    }
    // DS14-end

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
