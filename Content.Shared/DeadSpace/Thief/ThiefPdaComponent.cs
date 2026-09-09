using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Thief;

/// <summary>
/// DS14: Marks a PDA as the thief's own PDA. The ВорПРО program can only be
/// unlocked by inserting a tool into a PDA that carries this marker; any other
/// PDA refuses to accept tools in its tool slot (checked on both server and
/// client so the insertion is rejected predictively). The marker is added to
/// the thief's starter PDA when the thief antag is activated.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ThiefPdaComponent : Component
{
}