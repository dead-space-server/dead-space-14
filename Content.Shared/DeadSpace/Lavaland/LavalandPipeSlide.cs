// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Movement.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Pipe sliding settings of a lavaland planet.</summary>
[DataDefinition]
public sealed partial class LavalandPipeSlideConfig
{
    [DataField]
    public bool Enabled = true;

    /// <summary>Tiles per second.</summary>
    [DataField]
    public float Speed = 8f;

    /// <summary>How close to the pipe you have to be to hop on.</summary>
    [DataField]
    public float MountRange = 1.5f;

    [DataField]
    public TimeSpan SparkInterval = TimeSpan.FromSeconds(0.12);

    [DataField]
    public int SparksMin = 1;

    [DataField]
    public int SparksMax = 3;

    [DataField]
    public float SparkSpeedMin = 1f;

    [DataField]
    public float SparkSpeedMax = 4f;

    [DataField]
    public EntProtoId SparkPrototype = "LavalandGrindSpark";

    [DataField]
    public SoundSpecifier? MountSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public SoundSpecifier? GrindSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public TimeSpan GrindSoundInterval = TimeSpan.FromSeconds(0.6);

    /// <summary>Speed kept when jumping off or reaching the end of the pipe.</summary>
    [DataField]
    public float DismountSpeed = 4f;
}

/// <summary>Riding along a pipe. Normal movement is blocked while sliding.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LavalandPipeSliderComponent : Component
{
    public EntityUid Pipe;
    public EntityUid? PreviousPipe;
    public EntityUid? NextPipe;
    public System.Numerics.Vector2 Direction;
    public float Speed;
    public TimeSpan NextSpark;
    public TimeSpan NextSound;
    public bool AddedAirMovement;
    public bool AddedChasmImmunity;
}

/// <summary>Alt + left click on a pipe: hop on it, or hop off while sliding.</summary>
[Serializable, NetSerializable]
public sealed class LavalandPipeSlideRequestEvent(NetEntity? pipe) : EntityEventArgs
{
    public NetEntity? Pipe = pipe;
}

public sealed class SharedLavalandPipeSlideSystem : EntitySystem
{
    [Dependency] private readonly ActionBlocker.ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandPipeSliderComponent, UpdateCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<LavalandPipeSliderComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LavalandPipeSliderComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnCanMove(Entity<LavalandPipeSliderComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Cancel();
    }

    private void OnStartup(Entity<LavalandPipeSliderComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent);
    }

    private void OnShutdown(Entity<LavalandPipeSliderComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // Undo the temporary protection the slide gave against lava and chasms.
        if (ent.Comp.AddedAirMovement)
            RemComp<Content.Shared.Movement.Components.CanMoveInAirComponent>(ent);
        if (ent.Comp.AddedChasmImmunity)
            RemComp<Components.LavalandChasmImmuneComponent>(ent);
        _blocker.UpdateCanMove(ent);
    }
}
