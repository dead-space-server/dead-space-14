using Content.Server.Atmos.EntitySystems;
using Content.Server.Backmen.Blob.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Trigger;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server.Backmen.Blob;

public sealed class BlobMineSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundPathSpecifier DetonateSound = new("/Audio/Effects/explosion1.ogg");

    public override void Initialize()
    {
        SubscribeLocalEvent<BlobMineComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<BlobMineComponent> ent, ref TriggerEvent args)
    {
        if (args.Handled || args.User is not { } user)
            return;

        if (!HasComp<DamageableComponent>(user))
            return;

        if (IsBlobFriendly(user))
            return;

        args.Handled = true;

        _damageable.TryChangeDamage(user, new DamageSpecifier
        {
            DamageDict =
            {
                ["Heat"] = FixedPoint2.New(_random.Next(15, 21)),
            }
        });

        if (TryComp<FlammableComponent>(user, out var flammable))
        {
            flammable.FireStacks += 3;
            _flammable.Ignite(user, ent.Owner, flammable);
        }

        if (!BlobSilence.BeforeAnnouncement(EntityManager))
            _audio.PlayPvs(DetonateSound, ent);
        QueueDel(ent.Owner);
    }

    private bool IsBlobFriendly(EntityUid uid)
    {
        if (HasComp<BlobMobComponent>(uid) ||
            HasComp<ZombieBlobComponent>(uid) ||
            HasComp<BlobCarrierComponent>(uid))
        {
            return true;
        }

        return HasComp<BlobTileComponent>(uid);
    }
}
