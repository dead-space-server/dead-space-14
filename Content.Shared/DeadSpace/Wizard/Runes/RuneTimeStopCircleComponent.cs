// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared.DeadSpace.Wizard.Runes;

[RegisterComponent]
public sealed partial class RuneTimeStopCircleComponent : Component
{
    public Vector2 Origin;
    public float Radius;
    public MapId MapId;
    public TimeSpan End;
    public List<RuneHeldHitscan> Held = new();
}

public sealed class RuneHeldHitscan
{
    public string Proto = "";
    public MapCoordinates From;
    public Vector2 Direction;
    public float MaxDistance;
    public EntityUid Gun;
    public EntityUid? Shooter;
    public EntityUid? Target;
    public List<EntityUid>? Ignored;
}
