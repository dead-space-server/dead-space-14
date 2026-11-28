// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const string BurntKey = "EncryptionKeySurveyNativeBurnt";
    private const int EavesdropMin = 2;
    private const int EavesdropMax = 4;
    private static readonly TimeSpan EavesdropDebounce = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ReplyDelay = TimeSpan.FromSeconds(2);

    [Dependency] private SharedContainerSystem _containers = default!;

    private void InitializeEavesdrop()
    {
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSend);
    }

    private void OnRadioSend(ref RadioSendAttemptEvent ev)
    {
        if (ev.Channel.ID != NativeChannel.Id || HasComp<SurveyMemberComponent>(ev.RadioSource))
            return;

        var speaker = HasComp<ActorComponent>(ev.RadioSource) ? ev.RadioSource : Transform(ev.RadioSource).ParentUid;
        if (!HasComp<ActorComponent>(speaker))
            return;

        Entity<SurveyDirectorComponent>? listener = null;
        var speakerMap = Transform(speaker).MapUid;
        var query = EntityQueryEnumerator<SurveyDirectorComponent>();
        while (query.MoveNext(out var uid, out var director))
        {
            if (!director.Ready || director.Kind is not (SurveyKind.Raiders or SurveyKind.Pirates or SurveyKind.Factory))
                continue;

            if (listener == null || uid == speakerMap)
                listener = (uid, director);
        }

        if (listener is { } ent)
            Eavesdrop(ent, speaker, ev.RadioSource);
    }

    private void Eavesdrop(Entity<SurveyDirectorComponent> ent, EntityUid speaker, EntityUid headset)
    {
        var director = ent.Comp;
        var now = _timing.CurTime;
        if (!director.Eavesdroppers.TryGetValue(speaker, out var state))
        {
            state = new SurveyEavesdrop { Limit = _random.Next(EavesdropMin, EavesdropMax + 1) };
            director.Eavesdroppers[speaker] = state;
        }

        if (now < state.Next)
            return;

        state.Next = now + EavesdropDebounce;
        state.Strikes++;

        if (state.Strikes >= state.Limit)
        {
            QueueRadio(ent, "intruder-cut");
            BurnKeys(headset);
            state.Strikes = 0;
            state.Limit = _random.Next(EavesdropMin, EavesdropMax + 1);
            return;
        }

        if (state.Strikes == 1)
        {
            QueueRadio(ent, "intruder-heard");
            return;
        }

        QueueRadio(ent, "intruder-warn");
        if (Transform(speaker).MapUid != ent.Owner)
            return;

        var pos = Pos(speaker);
        QueueRadio(ent, "intruder-locate", ("where", Where(director, pos)));
        Triangulate(ent, speaker, pos);
    }

    private void QueueRadio(Entity<SurveyDirectorComponent> ent, string topic, params (string, object)[] args)
    {
        if (AnySpeaker(ent.Comp) is not { } speaker || RadioLine($"survey-radio-{Voice(ent.Comp)}-{topic}", args) is not { } line)
            return;

        var at = _timing.CurTime + ReplyDelay;
        if (ent.Comp.RadioQueue.Count > 0)
            at = ent.Comp.RadioQueue.Max(q => q.At) + ReplyDelay;

        ent.Comp.RadioQueue.Enqueue((speaker, line, at));
    }

    private void Triangulate(Entity<SurveyDirectorComponent> ent, EntityUid speaker, Vector2 pos)
    {
        var director = ent.Comp;
        var hunters = director.Squads
            .Where(s => s.Alive > 0 && !s.Withdrawn && !s.Ambush && !(s.Engaged && s.Target != null))
            .OrderBy(s => Vector2.DistanceSquared(Centroid(s, out _), pos))
            .Take(2)
            .ToList();

        if (hunters.Count == 0)
            return;

        RaiseAlarm(ent, hunters[0]);
        foreach (var squad in hunters)
        {
            squad.Engaged = true;
            squad.Target = speaker;
            squad.LastSeen = _timing.CurTime + TimeSpan.FromSeconds(25);
            squad.LastKnown = pos;
            squad.NextOrders = TimeSpan.Zero;

            if (director.Kind is SurveyKind.Raiders or SurveyKind.Pirates)
                Issue(ent, squad, director.Kind == SurveyKind.Pirates);
        }
    }

    private void BurnKeys(EntityUid headset)
    {
        if (!TryComp<EncryptionKeyHolderComponent>(headset, out var holder))
            return;

        foreach (var key in holder.KeyContainer.ContainedEntities.ToList())
        {
            if (!TryComp<EncryptionKeyComponent>(key, out var comp) || !comp.Channels.Contains(NativeChannel))
                continue;

            _containers.Remove(key, holder.KeyContainer);
            Del(key);
            SpawnInContainerOrDrop(BurntKey, headset, holder.KeyContainer.ID);
        }
    }
}
