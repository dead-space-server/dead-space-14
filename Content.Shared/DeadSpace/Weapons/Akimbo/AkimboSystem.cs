// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Weapons.Akimbo;

public sealed class AkimboSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AkimboComponent, AkimboSelectGunEvent>(OnSelectGun);
        SubscribeLocalEvent<AkimboComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<AkimboComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<AkimboComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<AkimboComponent, GotUnequippedHandEvent>(OnUnequipped);
    }

    private void OnSelectGun(Entity<AkimboComponent> ent, ref AkimboSelectGunEvent args)
    {
        if (!TryGetPair(args.User, ent.Owner, out var left, out var right))
            return;

        args.Active = true;
        var leftLoaded = _guns.GetAmmoCount(left.Owner) > 0;
        var rightLoaded = _guns.GetAmmoCount(right.Owner) > 0;

        if (!leftLoaded && !rightLoaded)
            return;

        if (!leftLoaded)
            args.SelectedGun = right.Owner;
        else if (!rightLoaded)
            args.SelectedGun = left.Owner;
        else
            args.SelectedGun = ent.Comp.NextHand == HandLocation.Left ? left.Owner : right.Owner;
    }

    private void OnShotAttempted(Entity<AkimboComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!TryGetPair(args.User, ent.Owner, out _, out _))
            return;

        if (ent.Comp.NextPairFire > _timing.CurTime)
            args.Cancel();
    }

    private void OnGunShot(Entity<AkimboComponent> ent, ref GunShotEvent args)
    {
        if (!TryGetPair(args.User, ent.Owner, out var left, out var right))
            return;

        var fireRate = Math.Max(ent.Owner == left.Owner
            ? left.Comp1.FireRateModified
            : right.Comp1.FireRateModified, 0.001f);
        var bothLoaded = _guns.GetAmmoCount(left.Owner) > 0 && _guns.GetAmmoCount(right.Owner) > 0;
        var delayMultiplier = bothLoaded ? Math.Max(0f, ent.Comp.FireDelayMultiplier) : 1f;
        var nextFire = _timing.CurTime + TimeSpan.FromSeconds(delayMultiplier / fireRate);
        var nextHand = ent.Owner == left.Owner ? HandLocation.Right : HandLocation.Left;

        SetPairState(left.Owner, left.Comp2, nextFire, nextHand);
        SetPairState(right.Owner, right.Comp2, nextFire, nextHand);
    }

    private void OnEquipped(Entity<AkimboComponent> ent, ref GotEquippedHandEvent args)
    {
        ResetHeldPair(args.User);
    }

    private void OnUnequipped(Entity<AkimboComponent> ent, ref GotUnequippedHandEvent args)
    {
        ResetHeldPair(args.User);
    }

    private void ResetHeldPair(EntityUid user)
    {
        if (!TryComp<HandsComponent>(user, out var hands))
            return;

        foreach (var held in _hands.EnumerateHeld((user, hands)))
        {
            if (TryComp<AkimboComponent>(held, out var akimbo))
                SetPairState(held, akimbo, TimeSpan.Zero, HandLocation.Left);
        }
    }

    private void SetPairState(EntityUid uid, AkimboComponent component, TimeSpan nextFire, HandLocation nextHand)
    {
        component.NextPairFire = nextFire;
        component.NextHand = nextHand;
        Dirty(uid, component);
    }

    public float GetShotAccuracy(EntityUid user, EntityUid gun)
    {
        if (!TryComp<AkimboComponent>(gun, out var akimbo) ||
            !TryGetPair(user, gun, out _, out _))
        {
            return 1f;
        }

        return Math.Clamp(akimbo.AkimboAccuracy, 0.01f, 1f);
    }

    private bool TryGetPair(
        EntityUid user,
        EntityUid requested,
        out Entity<GunComponent, AkimboComponent> left,
        out Entity<GunComponent, AkimboComponent> right)
    {
        left = default;
        right = default;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        foreach (var handId in hands.SortedHands)
        {
            if (!_hands.TryGetHand((user, hands), handId, out var hand) ||
                !_hands.TryGetHeldItem((user, hands), handId, out var held) ||
                !TryComp<GunComponent>(held, out var gun) ||
                !TryComp<AkimboComponent>(held, out var akimbo))
            {
                continue;
            }

            if (hand.Value.Location == HandLocation.Left)
                left = (held.Value, gun, akimbo);
            else if (hand.Value.Location == HandLocation.Right)
                right = (held.Value, gun, akimbo);
        }

        return left.Owner.Valid &&
               right.Owner.Valid &&
               (requested == left.Owner || requested == right.Owner) &&
               AreCompatible(left, right);
    }

    private bool AreCompatible(Entity<GunComponent, AkimboComponent> left, Entity<GunComponent, AkimboComponent> right)
    {
        var leftPrototype = MetaData(left).EntityPrototype?.ID;
        if (leftPrototype != null && leftPrototype == MetaData(right).EntityPrototype?.ID)
            return true;

        return left.Comp2.MatchTag is { } tag &&
               tag == right.Comp2.MatchTag &&
               _tags.HasTag(left, tag) &&
               _tags.HasTag(right, tag);
    }
}
