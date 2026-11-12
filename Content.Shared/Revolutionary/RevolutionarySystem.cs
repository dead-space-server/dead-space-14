using Content.Shared.Revolutionary.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Content.Shared.Antag;
using Content.Shared.StatusIcon.Components;

namespace Content.Shared.Revolutionary;

<<<<<<< ours
// DS14-start
public abstract class SharedRevolutionarySystem : EntitySystem
||||||| base
public abstract class SharedRevolutionarySystem : EntitySystem
=======
public sealed partial class RevolutionarySystem : EntitySystem
>>>>>>> theirs
{
    public override void Initialize()
    {
        base.Initialize();

<<<<<<< ours
        SubscribeLocalEvent<RevolutionaryComponent, ComponentGetStateAttemptEvent>(OnComponentGetStateAttempt);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentGetStateAttemptEvent>(OnComponentGetStateAttempt);
||||||| base
        SubscribeLocalEvent<MindShieldComponent, MapInitEvent>(MindShieldImplanted);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentGetStateAttemptEvent>(OnRevCompGetStateAttempt);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentGetStateAttemptEvent>(OnRevCompGetStateAttempt);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentStartup>(DirtyRevComps);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentStartup>(DirtyRevComps);
        SubscribeLocalEvent<ShowAntagIconsComponent, ComponentStartup>(DirtyRevComps);
=======
        SubscribeLocalEvent<RevolutionaryComponent, ComponentStartup>(DirtyRevComps);
        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentStartup>(DirtyRevComps);
        SubscribeLocalEvent<ShowAntagIconsComponent, ComponentStartup>(DirtyRevComps);
>>>>>>> theirs
    }

<<<<<<< ours
    private static void OnComponentGetStateAttempt(
        EntityUid uid,
        RevolutionaryComponent component,
        ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = args.Player != null;
    }

    private static void OnComponentGetStateAttempt(
        EntityUid uid,
        HeadRevolutionaryComponent component,
        ref ComponentGetStateAttemptEvent args)
||||||| base
    /// <summary>
    /// When the mindshield is implanted in the rev it will popup saying they were deconverted. In Head Revs it will remove the mindshield component.
    /// </summary>
    private void MindShieldImplanted(EntityUid uid, MindShieldComponent comp, MapInitEvent init)
    {
        if (HasComp<HeadRevolutionaryComponent>(uid))
        {
            RemCompDeferred<MindShieldComponent>(uid);
            return;
        }

        if (HasComp<RevolutionaryComponent>(uid))
        {
            var stunTime = TimeSpan.FromSeconds(4);
            var name = Identity.Entity(uid, EntityManager);
            RemComp<RevolutionaryComponent>(uid);
            _sharedStun.TryUpdateParalyzeDuration(uid, stunTime);
            _popupSystem.PopupEntity(Loc.GetString("rev-break-control", ("name", name)), uid);
        }
    }

    /// <summary>
    /// Determines if a HeadRev component should be sent to the client.
    /// </summary>
    private void OnRevCompGetStateAttempt(EntityUid uid, HeadRevolutionaryComponent comp, ref ComponentGetStateAttemptEvent args)
=======
    /// <summary>
    /// Determines if a HeadRev component should be sent to the client.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRevCompGetStateAttempt(EntityUid uid, HeadRevolutionaryComponent comp, ref ComponentGetStateAttemptEvent args)
>>>>>>> theirs
    {
<<<<<<< ours
        args.Cancelled = args.Player != null;
||||||| base
        args.Cancelled = !CanGetState(args.Player);
    }

    /// <summary>
    /// Determines if a Rev component should be sent to the client.
    /// </summary>
    private void OnRevCompGetStateAttempt(EntityUid uid, RevolutionaryComponent comp, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(args.Player);
    }

    /// <summary>
    /// The criteria that determine whether a Rev/HeadRev component should be sent to a client.
    /// </summary>
    /// <param name="player"> The Player the component will be sent to.</param>
    /// <returns></returns>
    private bool CanGetState(ICommonSession? player)
    {
        //Apparently this can be null in replays so I am just returning true.
        if (player?.AttachedEntity is not {} uid)
            return true;

        if (HasComp<RevolutionaryComponent>(uid) || HasComp<HeadRevolutionaryComponent>(uid))
            return true;

        return HasComp<ShowAntagIconsComponent>(uid);
    }
    /// <summary>
    /// Dirties all the Rev components so they are sent to clients.
    ///
    /// We need to do this because if a rev component was not earlier sent to a client and for example the client
    /// becomes a rev then we need to send all the components to it. To my knowledge there is no way to do this on a
    /// per client basis so we are just dirtying all the components.
    /// </summary>
    private void DirtyRevComps<T>(EntityUid someUid, T someComp, ComponentStartup ev)
    {
        var revComps = AllEntityQuery<RevolutionaryComponent>();
        while (revComps.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }

        var headRevComps = AllEntityQuery<HeadRevolutionaryComponent>();
        while (headRevComps.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }
=======
        args.Cancelled = !CanGetState(args.Player);
    }

    /// <summary>
    /// Determines if a Rev component should be sent to the client.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnRevCompGetStateAttempt(EntityUid uid, RevolutionaryComponent comp, ref ComponentGetStateAttemptEvent args)
    {
        args.Cancelled = !CanGetState(args.Player);
    }

    /// <summary>
    /// The criteria that determine whether a Rev/HeadRev component should be sent to a client.
    /// </summary>
    /// <param name="player"> The Player the component will be sent to.</param>
    /// <returns></returns>
    private bool CanGetState(ICommonSession? player)
    {
        //Apparently this can be null in replays so I am just returning true.
        if (player?.AttachedEntity is not {} uid)
            return true;

        if (HasComp<RevolutionaryComponent>(uid) || HasComp<HeadRevolutionaryComponent>(uid))
            return true;

        return HasComp<ShowAntagIconsComponent>(uid);
    }

    /// <summary>
    /// Dirties all the Rev components so they are sent to clients.
    ///
    /// We need to do this because if a rev component was not earlier sent to a client and for example the client
    /// becomes a rev then we need to send all the components to it. To my knowledge there is no way to do this on a
    /// per client basis so we are just dirtying all the components.
    /// </summary>
    private void DirtyRevComps<T>(EntityUid someUid, T someComp, ComponentStartup ev)
    {
        var revComps = AllEntityQuery<RevolutionaryComponent>();
        while (revComps.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }

        var headRevComps = AllEntityQuery<HeadRevolutionaryComponent>();
        while (headRevComps.MoveNext(out var uid, out var comp))
        {
            Dirty(uid, comp);
        }
>>>>>>> theirs
    }

    [SubscribeLocalEvent]
    private void OnAttemptConvert(Entity<RevolutionaryComponent> ent, ref AttemptConvertRevolutionaryEvent args)
    {
        args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnAttemptConvertImmune(Entity<RevolutionaryImmuneComponent> ent, ref AttemptConvertRevolutionaryEvent args)
    {
        args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void GetRevIcon(Entity<RevolutionaryComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<HeadRevolutionaryComponent>(ent))
            return;

        if (ProtoMan.Resolve(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }

    [SubscribeLocalEvent]
    private void GetHeadRevIcon(Entity<HeadRevolutionaryComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ProtoMan.Resolve(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
// DS14-end
