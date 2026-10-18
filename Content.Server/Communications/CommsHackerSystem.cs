using Content.Server.GameTicking;
using Content.Server.Ninja.Systems;
using Content.Shared.Communications;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups; //DS14
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing; //DS14

namespace Content.Server.Communications;

public sealed class CommsHackerSystem : SharedCommsHackerSystem
{
    [Dependency] private readonly Content.Server.DeadSpace.CentComm.GameRuleStationSystem _ruleStation = default!; // DS14
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    // TODO: remove when generic check event is used
    [Dependency] private readonly NinjaGlovesSystem _gloves = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!; //DS14
    [Dependency] private readonly IGameTiming _timing = default!; //DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CommsHackerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<CommsHackerComponent, TerrorDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CommsHackerComponent, DoAfterAttemptEvent<TerrorDoAfterEvent>>(OnDoAfterAttempt); //DS14
    }

    /// <summary>
    /// Start the doafter to hack a comms console
    /// </summary>
    private void OnBeforeInteractHand(EntityUid uid, CommsHackerComponent comp, BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<CommunicationsConsoleComponent>(args.Target))
            return;

        // TODO: generic check event
        if (!_gloves.AbilityCheck(uid, args, out var target))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.Delay, new TerrorDoAfterEvent(), target: target, used: uid, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = false,
            AttemptFrequency = AttemptFrequency.EveryTick, //DS14
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    //DS14-start
    private void OnDoAfterAttempt(Entity<CommsHackerComponent> uid, ref DoAfterAttemptEvent<TerrorDoAfterEvent> args)
    {
        var comp = uid.Comp;
        var doAfter = args.DoAfter;

        if (comp.ActiveDoAfter != doAfter.Id)
        {
            comp.ActiveDoAfter = doAfter.Id;
            comp.TriggeredStages = 0;
        }

        var duration = Math.Max(doAfter.Args.Delay.TotalSeconds, 0.1);
        var progress = Math.Clamp((_timing.CurTime - doAfter.StartTime).TotalSeconds / duration, 0d, 1d);

        if (progress >= 0.25 && (comp.TriggeredStages & 1) == 0)
        {
            comp.TriggeredStages |= 1;
            SpawnSparkEffect(doAfter.Args.Target, doAfter.Args.User, comp.SparksEffectPrototype);
        }

        if (progress >= 0.5 && (comp.TriggeredStages & 2) == 0)
        {
            comp.TriggeredStages |= 2;
            if (comp.HalfwayPopup is { } popup)
                _popup.PopupEntity(Loc.GetString(popup), doAfter.Args.Target ?? doAfter.Args.User, PopupType.LargeCaution);
        }

        if (progress >= 0.75 && (comp.TriggeredStages & 4) == 0)
        {
            comp.TriggeredStages |= 4;
            SpawnSparkEffect(doAfter.Args.Target, doAfter.Args.User, comp.SparksEffectPrototype);
        }
    }

    private void SpawnSparkEffect(EntityUid? target, EntityUid user, EntProtoId effect)
    {
        var coords = target is { } targetUid && !Deleted(targetUid)
            ? Transform(targetUid).Coordinates
            : Transform(user).Coordinates;

        Spawn(effect, coords);
    }
    //DS14-end

    /// <summary>
    /// Call in a random threat and do cleanup.
    /// </summary>
    private void OnDoAfter(EntityUid uid, CommsHackerComponent comp, TerrorDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        var threats = _proto.Index<WeightedRandomPrototype>(comp.Threats);
        var threat = threats.Pick(_random);
        CallInThreat(_proto.Index<NinjaHackingThreatPrototype>(threat));

        // prevent calling in multiple threats
        RemComp<CommsHackerComponent>(uid);

        var ev = new ThreatCalledInEvent(uid, args.Target.Value);
        RaiseLocalEvent(args.User, ref ev);
    }

    /// <summary>
    /// Makes announcement and adds game rule of the threat.
    /// </summary>
    public void CallInThreat(NinjaHackingThreatPrototype ninjaHackingThreat)
    {
        _gameTicker.StartGameRule(ninjaHackingThreat.Rule, out var rule); // DS14
        _ruleStation.Announce(rule, Loc.GetString(ninjaHackingThreat.Announcement), playSound: true, colorOverride: Color.Red); // DS14
    }
}

/// <summary>
/// Raised on the user when a threat is called in on the communications console.
/// </summary>
/// <remarks>
/// If you add <see cref="CommsHackerComponent"/>, make sure to use this event to prevent adding it twice.
/// For example, you could add a marker component after a threat is called in then check if the user doesn't have that marker before adding CommsHackerComponent.
/// </remarks>
[ByRefEvent]
public record struct ThreatCalledInEvent(EntityUid Used, EntityUid Target);
