using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Robust.Shared.Network; //DS14
using Robust.Shared.Prototypes; //DS14
using Robust.Shared.Serialization;
using Robust.Shared.Timing; //DS14

namespace Content.Shared.Research.Systems;

public abstract class SharedResearchStealerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedNinjaGlovesSystem _gloves = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!; //DS14
    [Dependency] private readonly INetManager _net = default!; //DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchStealerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<ResearchStealerComponent, DoAfterAttemptEvent<ResearchStealDoAfterEvent>>(OnDoAfterAttempt); //DS14
    }

    /// <summary>
    /// Start do after for downloading techs from a r&d server.
    /// Will only try if there is at least 1 tech researched.
    /// </summary>
    private void OnBeforeInteractHand(EntityUid uid, ResearchStealerComponent comp, BeforeInteractHandEvent args)
    {
        // TODO: generic event
        if (args.Handled || !_gloves.AbilityCheck(uid, args, out var target))
            return;

        // can only hack the server, not a random console
        if (!TryComp<TechnologyDatabaseComponent>(target, out var database) || HasComp<ResearchClientComponent>(target))
            return;

        args.Handled = true;

        // fail fast if theres no techs to steal right now
        if (database.UnlockedTechnologies.Count == 0)
        {
            _popup.PopupClient(Loc.GetString("ninja-download-fail"), uid, uid);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.Delay, new ResearchStealDoAfterEvent(), target: target, used: uid, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            AttemptFrequency = AttemptFrequency.EveryTick, //DS14
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    //DS14-start
    private void OnDoAfterAttempt(Entity<ResearchStealerComponent> uid, ref DoAfterAttemptEvent<ResearchStealDoAfterEvent> args)
    {
        if (_net.IsClient)
            return;

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
            SpawnSparkEffect(doAfter.Args.Target, doAfter.Args.User, comp.EffectPrototype);
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
            SpawnSparkEffect(doAfter.Args.Target, doAfter.Args.User, comp.EffectPrototype);
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
}

/// <summary>
/// Raised on the research stealer when the doafter completes.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ResearchStealDoAfterEvent : SimpleDoAfterEvent
{
}
