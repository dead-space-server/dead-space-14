using Content.Shared.Actions;
using System.Numerics; // DS14

namespace Content.Shared.RepulseAttract.Events;

// Action event to repulse/attract
// TODO: Give speech support later for wizard
// TODO: When actions are refactored, give action targeting data (to change between single target, all around, etc)
public sealed partial class RepulseAttractActionEvent : InstantActionEvent;

// DS14-start
/// <summary>
/// Raised on an entity immediately before repulse/attract throws it.
/// </summary>
[ByRefEvent]
public readonly record struct BeforeRepulseAttractThrownEvent(EntityUid? User, Vector2 Direction, float Speed);
// DS14-end
