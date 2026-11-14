using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Hitscan.Events;

// DS14-start: aggregate hitscan visual traces so the client can render animated bullet trails.
[Serializable, NetSerializable]
public struct HitscanTrace
{
    public Angle Angle;
    public float Distance;
    public float HoldSeconds;

    public NetCoordinates? MuzzleCoordinates;
    public NetCoordinates? TravelCoordinates;
    public NetCoordinates ImpactCoordinates;
    public NetEntity? ImpactedEnt;
}

[Serializable, NetSerializable]
public sealed class HitscanLightVisual
{
    public Color Color = Color.White;
    public float Radius = 5f;
    public float Energy = 1f;
    public float Softness = 1f;
    public float Falloff = 6.8f;
    public float CurveFactor;
    public bool CastShadows = true;
    public Vector2 Offset;
}
// DS14-end
