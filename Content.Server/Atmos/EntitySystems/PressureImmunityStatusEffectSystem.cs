using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Responds to pressure immunity refreshes for the active status effect.
/// </summary>
// DS14: Current engine uses explicit subscriptions, so this system does not need source generation.
public sealed class PressureImmunityStatusEffectSystem : EntitySystem
{
    public static readonly EntProtoId PressureImmunityEffect = "StatusEffectPressureImmunity";

    [Dependency] private readonly BarotraumaSystem _barotrauma = default!;

    public override void Initialize()
    {
        base.Initialize();

        // DS14-start: Current engine does not support source-generated event subscriptions.
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectAppliedEvent>(OnPressureImmunityStatusApplied);
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectRemovedEvent>(OnPressureImmunityStatusRemoved);
        SubscribeLocalEvent<PressureImmunityStatusEffectComponent, StatusEffectRelayedEvent<RefreshPressureImmunityEvent>>(OnRefreshPressureImmunity);
        // DS14-end
    }

    private void OnPressureImmunityStatusApplied(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _barotrauma.RefreshPressureImmunity(args.Target);
    }

    private void OnPressureImmunityStatusRemoved(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _barotrauma.RefreshPressureImmunity(args.Target);
    }

    private void OnRefreshPressureImmunity(Entity<PressureImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<RefreshPressureImmunityEvent> args)
    {
        args.Args = args.Args with { IsImmune = true };
    }
}
