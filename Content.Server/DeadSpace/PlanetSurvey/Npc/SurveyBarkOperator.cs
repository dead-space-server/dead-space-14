// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Shared.Chat;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.PlanetSurvey.Npc;

public sealed partial class SurveyBarkOperator : HTNOperator
{
    private const string NextKey = "SurveyBarkNext";
    private const string LastKey = "SurveyBarkLast";
    private const int MaxLines = 8;

    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IRobustRandom _random = default!;
    private ChatSystem _chat = default!;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(7);

    private readonly List<string> _lines = new();

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _chat = sysManager.GetEntitySystem<ChatSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        if (!blackboard.TryGetValue<string>(SurveyKeys.Bark, out var pool, _entManager))
            return HTNOperatorStatus.Finished;

        blackboard.Remove<string>(SurveyKeys.Bark);

        if (blackboard.TryGetValue<TimeSpan>(NextKey, out var next, _entManager) && _timing.CurTime < next)
            return HTNOperatorStatus.Finished;

        if (!blackboard.TryGetValue<string>(SurveyKeys.Voice, out var voice, _entManager) || voice.Length == 0)
            return HTNOperatorStatus.Finished;

        _lines.Clear();
        var last = blackboard.GetValueOrDefault<string>(LastKey, _entManager);
        for (var i = 1; i <= MaxLines; i++)
        {
            var id = $"survey-bark-{voice}-{pool}-{i}";
            if (!_loc.HasString(id))
                break;

            if (id != last)
                _lines.Add(id);
        }

        if (_lines.Count == 0)
            return HTNOperatorStatus.Finished;

        var line = _random.Pick(_lines);
        blackboard.SetValue(LastKey, line);
        blackboard.SetValue(NextKey, _timing.CurTime + Cooldown);

        _chat.TrySendInGameICMessage(
            blackboard.GetValue<EntityUid>(NPCBlackboard.Owner),
            _loc.GetString(line),
            InGameICChatType.Speak,
            ChatTransmitRange.Normal);

        return HTNOperatorStatus.Finished;
    }
}
