// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The lavaland's bosses are not shot down with station guns: every bullet of an ordinary gun that hits one makes the
/// next weaker (as the super resonator's blasts), down to a tenth. Kinetic guns, made for them, keep their full damage.
/// </summary>
public sealed class LavalandBossGunResistSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Each recent hit takes this much off the next.</summary>
    private const float Falloff = 0.05f;

    private const float MinMultiplier = 0.1f;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, BeforeProjectileHitEvent>(OnBeforeHit);
    }

    private void OnBeforeHit(Entity<ProjectileComponent> ent, ref BeforeProjectileHitEvent args)
    {
        // Only a station gun's bullets: kinetic guns and the lavaland's own staves, wisps and blasts hit in full.
        if (ent.Comp.Weapon is not { } weapon ||
            !HasComp<GunComponent>(weapon) ||
            HasComp<LavalandPressureDamageComponent>(weapon) ||
            HasComp<LavalandResonatorBoltComponent>(ent) ||
            !IsBoss(args.Target))
        {
            return;
        }

        var now = _timing.CurTime;
        var pressure = EnsureComp<LavalandGunPressureComponent>(args.Target);
        while (pressure.Hits.Count > 0 && now - pressure.Hits.Peek() >= Window)
            pressure.Hits.Dequeue();

        var multiplier = MathF.Max(MinMultiplier, 1f - Falloff * pressure.Hits.Count);
        pressure.Hits.Enqueue(now);
        if (multiplier < 1f)
            args.Damage = args.Damage * multiplier;
    }

    private bool IsBoss(EntityUid target)
    {
        return HasComp<LavalandBossComponent>(target) ||
               HasComp<LavalandMadMinerComponent>(target) ||
               HasComp<LavalandEliteComponent>(target) ||
               TryComp<LavalandAshWolfComponent>(target, out var wolf) && !wolf.Tamed;
    }
}
