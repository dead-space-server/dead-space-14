// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Dataset;
using Content.Shared.DeadSpace.CustomizableHumanoidSpawner;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.DeadSpace.GhostRoleIntroduction;
using Content.Shared.Mind.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.GhostRoleIntroduction;

public sealed class GhostRoleIntroductionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostRoleIntroductionComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<GhostRoleIntroductionComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnMindAdded(Entity<GhostRoleIntroductionComponent> ent, ref MindAddedMessage args)
    {
        if (HasComp<CustomizableHumanoidSpawnerComponent>(ent) ||
            !_player.TryGetSessionById(args.Mind.Comp.UserId, out var session))
        {
            return;
        }

        ShowIntroduction(ent, session);
    }

    private void OnPlayerAttached(Entity<GhostRoleIntroductionComponent> ent, ref PlayerAttachedEvent args)
    {
        if (HasComp<CustomizableHumanoidSpawnerComponent>(ent))
            return;

        ShowIntroduction(ent, args.Player);
    }

    private void ShowIntroduction(Entity<GhostRoleIntroductionComponent> ent, ICommonSession session)
    {
        if (ent.Comp.Shown)
            return;

        ent.Comp.Shown = true;

        RaiseNetworkEvent(new GhostRoleIntroductionEvent(
            ResolveOperationName(ent.Comp),
            ResolveText(ent.Comp),
            ent.Comp.TextColor,
            ent.Comp.Font,
            ent.Comp.FontSize,
            ent.Comp.OperationFontSize,
            ent.Comp.Duration,
            ent.Comp.FadeFromBlackDuration,
            ent.Comp.FadeOutDuration,
            ent.Comp.TextDelay,
            ent.Comp.CharactersPerSecond), session);

        if (ent.Comp.Sound != null)
            _audio.PlayGlobal(ent.Comp.Sound, Filter.Empty().AddPlayer(session), false);
    }

    private string ResolveOperationName(GhostRoleIntroductionComponent component)
    {
        if (component.UseNukeopsOperationName)
        {
            var query = EntityQueryEnumerator<NukeopsRuleComponent, ActiveGameRuleComponent>();
            if (query.MoveNext(out var rule, out _, out _))
            {
                return Loc.GetString(
                    "nukeops-operation-heading",
                    ("name", Name(rule)));
            }
        }

        if (component.OperationNameDataset is { } datasetId &&
            _prototype.TryIndex(datasetId, out var dataset))
        {
            return _random.Pick(dataset);
        }

        if (component.OperationNames.Count > 0)
            return _random.Pick(component.OperationNames);

        return string.IsNullOrWhiteSpace(component.OperationName)
            ? string.Empty
            : Loc.GetString(component.OperationName);
    }

    private string ResolveText(GhostRoleIntroductionComponent component)
    {
        string text;
        if (component.TextDataset is { } datasetId &&
            _prototype.TryIndex(datasetId, out var dataset))
        {
            text = _random.Pick(dataset);
        }
        else if (!string.IsNullOrWhiteSpace(component.TextLoc))
        {
            text = Loc.GetString(component.TextLoc);
        }
        else
        {
            text = component.Text;
        }

        if (!component.UseNukeopsOperationName)
            return text;

        var query = EntityQueryEnumerator<NukeopsRuleComponent, ActiveGameRuleComponent>();
        if (!query.MoveNext(out var rule, out var nukeops, out _))
            return text;

        var target = nukeops.TargetStation is { } station
            ? Name(station)
            : Loc.GetString("nukeops-target-fallback");

        var welcome = Loc.GetString(
            "nukeops-welcome",
            ("station", target),
            ("name", Name(rule)));

        return string.IsNullOrWhiteSpace(text)
            ? welcome
            : $"{welcome}{Environment.NewLine}{Environment.NewLine}{text}";
    }
}
