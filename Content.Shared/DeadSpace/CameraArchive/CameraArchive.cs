// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.CameraArchives;

[Serializable, NetSerializable]
public sealed class CameraArchive
{

    public const float ViewRadius = 9f;

    public const float EyeLead = 0.8f;

    public Angle GridRotation;

    public Angle CameraFacing;
    public Vector2 CameraLocal;
    public Vector2 Zoom = Vector2.One;
    public bool DrawFov = true;
    public bool DrawLight = true;

    public List<CameraArchiveTile> Tiles = new();
    public List<CameraArchiveEntity> Entities = new();
    public List<CameraArchiveDecal> Decals = new();

    public List<string> Chat = new();

    public bool Interference;

    public static Angle EyeRotation(Angle gridWorldRotation) => -gridWorldRotation;

    public static Vector2 PlaybackOrigin(Vector2 cameraLocal)
    {
        return new Vector2(MathF.Floor(cameraLocal.X), MathF.Floor(cameraLocal.Y));
    }
}

public static class CameraArchiveViewMath
{
    public const float PixelsPerMeter = 32f;

    public static Vector2 OriginalHalf(Vector2i viewport, Vector2 eyeZoom)
    {
        return new Vector2(viewport.X, viewport.Y) / (2f * PixelsPerMeter) * eyeZoom;
    }

    public static Vector2 ClampPan(Vector2 pan, Vector2 originalHalf, float zoomFactor)
    {
        if (zoomFactor <= 1f)
            return Vector2.Zero;

        var limit = originalHalf * (1f - 1f / zoomFactor);
        return new Vector2(
            Math.Clamp(pan.X, -limit.X, limit.X),
            Math.Clamp(pan.Y, -limit.Y, limit.Y));
    }

    public static Vector2 EyeZoom(Vector2 baseZoom, float magnification)
    {
        magnification = MathF.Max(1f, magnification);
        return baseZoom / magnification;
    }

    public static bool AcceptPlayback(int requestedId, int responseId) => requestedId == responseId;
}

[Serializable, NetSerializable]
public sealed class CameraArchiveTile
{
    public Vector2i Indices;
    public int TypeId;
    public byte Flags;
    public byte Variant;
    public byte RotationMirroring;
}

[Serializable, NetSerializable]
public sealed class CameraArchiveEntity
{
    public string Prototype = string.Empty;
    public Vector2 LocalPosition;
    public Angle LocalRotation;
    public bool Anchored;

    public int Parent = -1;

    public string Container = string.Empty;

    public bool ShowContents;

    public bool Loose;

    public bool InHand;

    public byte HandSide;

    public bool CopySsd;

    public bool IsSsd;

    public string SignText = string.Empty;

    public string ScreenText = string.Empty;

    public byte[]? Humanoid;

    public bool Conceal;

    public byte[]? Appearance;
}

[Serializable, NetSerializable]
public sealed class CameraArchiveDecal
{
    public Vector2 Coordinates;
    public string Id = string.Empty;
    public Color Color;
    public bool HasColor;
    public Angle Angle;
    public int ZIndex;
    public bool Cleanable;
}
