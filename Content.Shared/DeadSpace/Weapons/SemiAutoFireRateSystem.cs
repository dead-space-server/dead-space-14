// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Sandevistan;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared.DeadSpace.Weapons;

public sealed class SemiAutoFireRateSystem : EntitySystem
{
    [Dependency] private readonly SharedSandevistanSystem _sandevistan = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private readonly Dictionary<EntityUid, bool> _activeStates = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SemiAutoFireRateComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SemiAutoFireRateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var active = _sandevistan.IsActive(uid);
            if (_activeStates.TryGetValue(uid, out var previous) && previous == active)
                continue;

            _activeStates[uid] = active;
            RefreshHeldGuns(uid);
        }
    }

    public void ApplyGunModifiers(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.SelectedMode != SelectiveFire.SemiAuto ||
            ent.Comp.BurstActivated ||
            Holder(ent.Owner) is not { } user ||
            !TryComp<SemiAutoFireRateComponent>(user, out var fireRate) ||
            fireRate.OnlyWhileActive && !_sandevistan.IsActive(user))
        {
            return;
        }

        args.FireRate *= MathF.Max(0f, fireRate.ActiveMultiplier);
    }

    private void OnShutdown(Entity<SemiAutoFireRateComponent> ent, ref ComponentShutdown args)
    {
        _activeStates.Remove(ent.Owner);
        RefreshHeldGuns(ent.Owner);
    }

    private EntityUid? Holder(EntityUid item)
    {
        var parent = Transform(item).ParentUid;
        while (parent.IsValid())
        {
            if (HasComp<SemiAutoFireRateComponent>(parent))
                return parent;

            parent = Transform(parent).ParentUid;
        }

        return null;
    }

    private void RefreshHeldGuns(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (HasComp<GunComponent>(held))
                _guns.RefreshModifiers(held);
        }
    }
}
