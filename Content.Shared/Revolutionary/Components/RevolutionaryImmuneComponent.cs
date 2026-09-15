using Robust.Shared.GameStates;

namespace Content.Shared.Revolutionary.Components;

/// <summary>
/// Prevents an entity from being converted into a revolutionary.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RevolutionaryImmuneComponent : Component;
