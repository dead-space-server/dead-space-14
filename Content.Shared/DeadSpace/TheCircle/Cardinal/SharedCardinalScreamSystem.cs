// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Ensnaring.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// The predicted half of the cardinal's red scream: movement and fire rate.
/// </summary>
public sealed partial class SharedCardinalScreamSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardinalRallyComponent, ComponentStartup>(OnRallyStartup);
        SubscribeLocalEvent<CardinalRallyComponent, ComponentShutdown>(OnRallyShutdown);
        SubscribeLocalEvent<CardinalRallyComponent, RefreshMovementSpeedModifiersEvent>(OnRallySpeed);
        SubscribeLocalEvent<CardinalRallyGunComponent, GunRefreshModifiersEvent>(OnGunRefresh);
    }

    private void OnRallyStartup(Entity<CardinalRallyComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRallyShutdown(Entity<CardinalRallyComponent> ent, ref ComponentShutdown args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnRallySpeed(Entity<CardinalRallyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        var walk = ent.Comp.SpeedModifier;
        var sprint = ent.Comp.SpeedModifier;
        if (TryComp<EnsnareableComponent>(ent, out var snare) && snare.IsEnsnared)
        {
            walk *= Relief(snare.WalkSpeed, ent.Comp.EnsnareRelief);
            sprint *= Relief(snare.SprintSpeed, ent.Comp.EnsnareRelief);
        }

        args.ModifySpeed(walk, sprint);
    }

    /// <summary>
    /// The factor that takes the given share of a bola's slowdown back off.
    /// </summary>
    private static float Relief(float slowed, float relief)
    {
        if (slowed <= 0f || slowed >= 1f)
            return 1f;

        return (1f - (1f - slowed) * (1f - relief)) / slowed;
    }

    private void OnGunRefresh(Entity<CardinalRallyGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.FireRate += ent.Comp.FireRateBonus;
    }
}
