// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.Humanoid;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Storyteller;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StorytellerComponent : Component
{
    [AutoNetworkedField]
    public bool Telling = true;
}

public static class StoryStage
{
    public static readonly ProtoId<StorySettingsPrototype> SettingsId = "Default";

    public static StorySettingsPrototype Settings(IPrototypeManager prototypes)
    {
        return prototypes.Index(SettingsId);
    }

    public static void Bounds(StorySettingsPrototype settings, StoryLocationPrototype location, out float minX, out float maxX, out float minY, out float maxY)
    {
        var walled = location.Wall != null;
        minX = Math.Max(location.FloorFrom.X + 0.5f, 0.5f);
        maxX = Math.Min(location.FloorTo.X + 0.5f, settings.Width - 0.5f);
        minY = location.FloorFrom.Y + (walled ? 1.5f : 0.5f);
        maxY = Math.Min(location.FloorTo.Y + 0.3f, settings.Height - 0.7f);
        if (maxY < minY)
            maxY = minY;
    }

    public static System.Numerics.Vector2 Door(StorySettingsPrototype settings, StoryLocationPrototype location)
    {
        Bounds(settings, location, out _, out _, out var minY, out var maxY);
        return new System.Numerics.Vector2(0.5f, Math.Clamp(settings.DoorY + 0.5f, minY, maxY));
    }
}

[Serializable, NetSerializable]
public struct StoryPropState
{
    public ushort Id;
    public string Proto;
    public int X;
    public int Y;
    public Direction Dir;

    public StoryPropState(ushort id, string proto, int x, int y, Direction dir)
    {
        Id = id;
        Proto = proto;
        X = x;
        Y = y;
        Dir = dir;
    }
}

public enum StoryPose : byte
{
    Stand,
    Sit,
    Lie,
    Kneel,
    Fallen,
    Dead,
    Gone,
}

public enum StoryMove : byte
{
    None,
    Left,
    Right,
    Up,
    Down,
    Center,
    Target,
    Door,
}

public enum StoryCueKind : byte
{
    Location,
    Enter,
    Leave,
    Move,
    Pose,
    Face,
    Say,
    Emote,
    Hit,
    Shoot,
    Hug,
    Kiss,
    Give,
    Hold,
    Point,
    Dance,
    Camera,
    Shake,
    Look,
    Light,
    PropAdd,
    PropMove,
    PropTurn,
    PropRemove,
    PropReset,
    PropClear,
}

public enum StoryEffectKind : byte
{
    Say,
    Emote,
    Hit,
    Shoot,
    Hug,
    Kiss,
    Give,
    Point,
    Dance,
    Fall,
    Enter,
    Shake,
}

[Serializable, NetSerializable]
public struct StoryCue
{
    public StoryCueKind Kind;
    public string Actor = "";
    public string Target = "";
    public string Id = "";
    public string Role = "";
    public string Text = "";
    public Sex Sex;
    public bool HasSex;
    public StoryPose Pose;
    public StoryMove Move;
    public bool Run;
    public float Zoom;
    public ushort Prop;
    public int X;
    public int Y;
    public Direction Dir;

    public StoryCue()
    {
    }
}

[Serializable, NetSerializable]
public sealed class StoryActorState
{
    public byte Id;
    public ushort Look;
    public string Name = "";
    public string Kind = "";
    public string Role = "";
    public Sex Sex;
    public HumanoidCharacterAppearance? Appearance;
    public RoundEndDollData? Copy;
    public float X;
    public float Y;
    public Direction Facing = Direction.South;
    public StoryPose Pose;
    public bool Running;
    public string Held = "";
}

[Serializable, NetSerializable]
public struct StoryEffect
{
    public StoryEffectKind Kind;
    public byte Actor;
    public byte Target;
    public string Text = "";
    public string Item = "";
    public float Delay;

    public StoryEffect()
    {
    }
}

[Serializable, NetSerializable]
public struct StoryListener
{
    public NetEntity Entity;
    public string Name;

    public StoryListener(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class StorySceneEvent : EntityEventArgs
{
    public NetEntity Teller;
    public string TellerName = "";
    public string Caption = "";
    public string Location = "";
    public string Light = "";
    public string Tint = "";
    public float Zoom = 1f;
    public byte Focus;
    public ushort Revision;
    public StoryActorState[] Actors = Array.Empty<StoryActorState>();
    public StoryPropState[] Props = Array.Empty<StoryPropState>();
    public StoryEffect[] Effects = Array.Empty<StoryEffect>();
    public StoryListener[] Listeners = Array.Empty<StoryListener>();
    public int Pages;
    public int ScriptPage;
    public int ScriptPages;
    public string Title = "";
}

[Serializable, NetSerializable]
public sealed class StoryLineRequest : EntityEventArgs
{
    public string Line = "";
    public bool Apply;
}

[Serializable, NetSerializable]
public sealed class StoryCuesRequest : EntityEventArgs
{
    public StoryCue[] Cues = Array.Empty<StoryCue>();
}

[Serializable, NetSerializable]
public sealed class StoryJoinRequest : EntityEventArgs
{
    public NetEntity Teller;
}

[Serializable, NetSerializable]
public sealed class StoryLeaveRequest : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class StoryStopRequest : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class StoryWriteBookRequest : EntityEventArgs
{
    public string Title = "";
}

[Serializable, NetSerializable]
public sealed class StoryPageRequest : EntityEventArgs
{
    public bool Restart;
    public bool Back;
}

[Serializable, NetSerializable]
public sealed class StorySuggestionsEvent : EntityEventArgs
{
    public string[] Labels = Array.Empty<string>();
    public StoryCue[] Cues = Array.Empty<StoryCue>();
}

[Serializable, NetSerializable]
public sealed class StoryClosedEvent : EntityEventArgs;
