using Content.Server.DeadSpace.CentComm; // DS14
using Content.Server.RoundEnd;
using Content.Shared.Antag; // DS14
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules;
using Content.Shared.GameTicking.Rules.Components;
using Content.Shared.Objectives.Systems;
using Robust.Shared.Timing;

namespace Content.Server.GameTicking.Rules;

public sealed partial class ServerXenoborgsRuleSystem : XenoborgsRuleSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private AliveHumanoidTargetSystem _target = default!;
    // DS14-start
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private GameRuleStationSystem _ruleStation = default!;

    private static readonly Color AnnouncementColor = Color.Gold;
    // DS14-end

    protected override void ActiveTick(EntityUid uid, XenoborgsRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (!component.NextRoundEndCheck.HasValue || component.NextRoundEndCheck > _timing.CurTime)
            return;

        CheckRoundEnd(uid, component); // DS14
        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
    }

    private void CheckRoundEnd(EntityUid uid, XenoborgsRuleComponent xenoborgsRuleComponent) // DS14
    {
        var numXenoborgs = GetNumberXenoborgs();
        var numHumans = _target.GetMinds().Count;

        xenoborgsRuleComponent.MaxNumberXenoborgs = Math.Max(xenoborgsRuleComponent.MaxNumberXenoborgs, numXenoborgs);

        if (xenoborgsRuleComponent.XenoborgShuttleCalled
            || _roundEnd.IsRoundEndRequested()
            || !ShouldCallEmergencyShuttle(
                numXenoborgs,
                numHumans,
                xenoborgsRuleComponent.XenoborgShuttleCallPercentage)) // DS14
            return;

        _ruleStation.Announce(uid, Loc.GetString("xenoborg-shuttle-call"), colorOverride: Color.BlueViolet); // DS14
        _roundEnd.RequestRoundEnd(null, null, false, name: "station-event-announcer", cantRecall: true, announcementSource: uid); // DS14
        xenoborgsRuleComponent.XenoborgShuttleCalled = true;
    }

    // DS14-start
    internal static bool ShouldCallEmergencyShuttle(int xenoborgs, int humans, float threshold)
    {
        if (humans <= 0 || xenoborgs <= 0)
            return false;

        return (float) xenoborgs / (humans + xenoborgs) > threshold;
    }

    public override void SendXenoborgDeathAnnouncement(Entity<XenoborgsRuleComponent> ent, bool mothershipCoreAlive)
    {
        if (ent.Comp.MothershipCoreDeathAnnouncmentSent)
            return;

        var status = mothershipCoreAlive ? "alive" : "dead";
        _ruleStation.Announce(ent.Owner,
            Loc.GetString($"xenoborgs-no-more-threat-mothership-core-{status}-announcement"),
            colorOverride: AnnouncementColor);
    }

    public override void SendMothershipDeathAnnouncement(Entity<XenoborgsRuleComponent> ent)
    {
        _ruleStation.Announce(ent.Owner,
            Loc.GetString("mothership-destroyed-announcement"),
            sender: Loc.GetString("chat-manager-sender-announcement"),
            colorOverride: AnnouncementColor);

        ent.Comp.MothershipCoreDeathAnnouncmentSent = true;
    }

    protected override void AppendRoundEndDiscordText(EntityUid uid,
        XenoborgsRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndDiscordTextAppendEvent args)
    {
        args.AddLine(Loc.GetString("xenoborgs-list-start"));

        var antags = _antag.GetAntagIdentifiers(uid);

        foreach (var (_, sessionData, name) in antags)
        {
            args.AddLine(Loc.GetString("xenoborgs-list", ("name", name), ("user", sessionData.UserName)));
        }

        args.AddLine("");
    }
    // DS14-end
}
