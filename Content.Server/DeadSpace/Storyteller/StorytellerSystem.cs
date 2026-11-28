// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.RoundEnd;
using Content.Shared.DeadSpace.Storyteller;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Storyteller;

public sealed partial class StorytellerSystem : EntitySystem
{
    private const int MaxEffects = 32;
    private const string TintPrefix = "tint-";

    private static readonly ProtoId<TagPrototype> BookTag = "Book";


    [Dependency] private IAdminLogManager _admin = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCards = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, StorySession> _sessions = new();
    private readonly List<ICommonSession> _gone = new();
    private readonly Dictionary<(NetUserId, int), TimeSpan> _gates = new();
    private StoryLexicon _lexicon = new();

    private StorySettingsPrototype Settings => StoryStage.Settings(_prototypes);

    public override void Initialize()
    {
        base.Initialize();

        _lexicon = StoryLexicon.Build(_prototypes);

        SubscribeLocalEvent<StorytellerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StorytellerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<StorytellerComponent, MobStateChangedEvent>(OnMobState);
        SubscribeLocalEvent<StorytellerComponent, PlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<StoryBookComponent, ExaminedEvent>(OnBookExamined);
        SubscribeLocalEvent<StoryBookComponent, GetVerbsEvent<Verb>>(OnBookVerbs);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeNetworkEvent<StoryLineRequest>(OnLine);
        SubscribeNetworkEvent<StoryCuesRequest>(OnCues);
        SubscribeNetworkEvent<StoryJoinRequest>(OnJoin);
        SubscribeNetworkEvent<StoryLeaveRequest>(OnLeave);
        SubscribeNetworkEvent<StoryStopRequest>(OnStop);
        SubscribeNetworkEvent<StoryWriteBookRequest>(OnWriteBook);
        SubscribeNetworkEvent<StoryPageRequest>(OnPage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        foreach (var story in _sessions.Values)
        {
            var changed = false;
            for (var i = story.Leaving.Count - 1; i >= 0; i--)
            {
                if (story.Leaving[i].At > now)
                    continue;

                var leaving = story.Leaving[i].Id;
                story.Actors.RemoveAll(a => a.Id == leaving);
                story.Leaving.RemoveAt(i);
                changed = true;
            }

            if (now >= story.NextRangeCheck)
            {
                story.NextRangeCheck = now + TimeSpan.FromSeconds(1);
                _gone.Clear();
                foreach (var listener in story.Audience)
                {
                    if (listener.AttachedEntity is not { } mob || !InRange(story.Teller, mob, Settings.LeaveRange, false))
                        _gone.Add(listener);
                }

                foreach (var listener in _gone)
                {
                    Drop(story, listener, "storyteller-popup-too-far");
                    changed = true;
                }
            }

            if (changed)
                SendScene(story);
        }
    }

    #region Verbs and examine

    private void OnVerbs(GetVerbsEvent<Verb> args)
    {
        var target = args.Target;
        if (args.User == target)
        {
            if (!TryComp(target, out ActorComponent? self))
                return;

            if (_sessions.ContainsKey(target))
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("storyteller-verb-stop"),
                    Icon = BookIcon(),
                    Act = () => EndSession(target, false),
                });
            }
            else if (_mobs.IsAlive(target) && CanTell(target) && !Listening(self.PlayerSession))
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("storyteller-verb-start"),
                    Icon = BookIcon(),
                    Act = () => Start(target, self.PlayerSession),
                });
            }

            return;
        }

        if (_sessions.ContainsKey(args.User))
            return;

        if (!_sessions.ContainsKey(target) || !InRange(target, args.User, Settings.JoinRange, true))
            return;

        if (!HasComp<GhostComponent>(args.User) && !args.CanAccess)
            return;

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("storyteller-verb-listen"),
            Icon = BookIcon(),
            Act = () => Join(target, actor.PlayerSession),
        });
    }

    private void OnBookVerbs(Entity<StoryBookComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Pages.Count == 0)
            return;

        if (!TryComp(args.User, out ActorComponent? actor) || !_mobs.IsAlive(args.User))
            return;

        var user = args.User;
        var book = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("storyteller-verb-book"),
            Icon = BookIcon(),
            Act = () => StartFromBook(user, actor.PlayerSession, book),
        });
    }

    private void OnExamined(EntityUid uid, StorytellerComponent component, ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("storyteller-examined"));
    }

    private void OnBookExamined(Entity<StoryBookComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Pages.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("storyteller-book-examined",
            ("title", FormattedMessage.EscapeText(ent.Comp.Title)),
            ("pages", ent.Comp.Pages.Count)));
    }

    #endregion

    #region Lifecycle

    private void OnMobState(EntityUid uid, StorytellerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            EndSession(uid, true);
    }

    private void OnDetached(EntityUid uid, StorytellerComponent component, PlayerDetachedEvent args)
    {
        EndSession(uid, true);
    }

    private void OnShutdown(EntityUid uid, StorytellerComponent component, ComponentShutdown args)
    {
        EndSession(uid, true, false);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var id in _sessions.Keys.ToArray())
            EndSession(id, true);

        _gates.Clear();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<StoryLocationPrototype>() || args.WasModified<StoryKindPrototype>() ||
            args.WasModified<StoryRolePrototype>() || args.WasModified<StoryItemPrototype>() ||
            args.WasModified<StoryLanguagePrototype>() || args.WasModified<StoryEmotePrototype>() ||
            args.WasModified<StoryLightPrototype>() || args.WasModified<StorySettingsPrototype>())
        {
            _lexicon = StoryLexicon.Build(_prototypes);
        }
    }

    private bool CanTell(EntityUid uid)
    {
        var jobs = Settings.TellerJobs;
        if (jobs.Count == 0)
            return true;

        if (_idCards.TryFindIdCard(uid, out var card))
        {
            if (card.Comp.JobPrototype is { } cardJob)
            {
                if (jobs.Contains(cardJob))
                    return true;
            }
            else if (jobs.Any(allowed => _prototypes.TryIndex(allowed, out var job) && job.Icon == card.Comp.JobIcon))
            {
                return true;
            }
        }

        return _mind.TryGetMind(uid, out var mindId, out _) &&
               _jobs.MindTryGetJobId(mindId, out var job) &&
               job is { } id && jobs.Contains(id);
    }

    private StorySession? Start(EntityUid uid, ICommonSession session, bool fromBook = false)
    {
        if (_sessions.TryGetValue(uid, out var existing))
            return existing;

        if (!_mobs.IsAlive(uid) || !fromBook && !CanTell(uid))
            return null;

        if (Listening(session))
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-busy"), uid, session);
            return null;
        }

        var story = new StorySession(uid, session, _timing.CurTime) { Location = Settings.DefaultLocation };
        ResetProps(story);
        _sessions.Add(uid, story);
        var comp = EnsureComp<StorytellerComponent>(uid);
        comp.Telling = true;
        Dirty(uid, comp);

        SendScene(story);
        _popup.PopupEntity(Loc.GetString("storyteller-popup-started", ("name", Name(uid))), uid);
        _admin.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(uid):player} started a story");
        return story;
    }

    private void StartFromBook(EntityUid user, ICommonSession session, EntityUid book)
    {
        if (!TryComp(book, out StoryBookComponent? data) || data.Pages.Count == 0)
            return;

        var story = Start(user, session, true);
        if (story == null)
            return;

        ResetScene(story);
        story.Script = data.Pages.Select(p => p.Clone()).ToList();
        story.ScriptIndex = 0;
        story.Title = data.Title;
        story.Caption = Loc.GetString("storyteller-book-opened", ("title", data.Title));
        PlaySetup(story, true);
        SendScene(story);
        _admin.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} tells a story from {ToPrettyString(book)}");
    }

    private void Join(EntityUid teller, ICommonSession session)
    {
        if (!_sessions.TryGetValue(teller, out var story) || story.TellerSession == session)
            return;

        if (session.AttachedEntity is not { } mob)
            return;

        if (!InRange(teller, mob, Settings.JoinRange, true))
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-range"), mob, session);
            return;
        }

        if (_sessions.ContainsKey(mob))
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-busy"), mob, session);
            return;
        }

        story.SentLooks.Remove(session.UserId);
        if (story.Audience.Contains(session))
        {
            SendScene(story, session);
            return;
        }

        Leave(session, false);
        story.Audience.Add(session);
        _popup.PopupEntity(Loc.GetString("storyteller-popup-joined"), mob, session);
        SendScene(story);
    }

    private void Leave(ICommonSession session, bool popup)
    {
        foreach (var story in _sessions.Values)
        {
            if (!story.Audience.Contains(session))
                continue;

            Drop(story, session, popup ? "storyteller-popup-left" : null);
            SendScene(story);
            return;
        }
    }

    private void Drop(StorySession story, ICommonSession session, string? popup)
    {
        if (!story.Audience.Remove(session))
            return;

        story.SentLooks.Remove(session.UserId);
        if (popup != null && session.AttachedEntity is { } mob)
            _popup.PopupEntity(Loc.GetString(popup), mob, session);

        Send(new StoryClosedEvent(), session);
    }

    private void EndSession(EntityUid uid, bool quiet, bool removeComponent = true)
    {
        if (!_sessions.Remove(uid, out var story))
        {
            if (removeComponent)
                RemoveTellerComponent(uid);
            return;
        }

        Send(new StoryClosedEvent(), story.TellerSession);
        foreach (var listener in story.Audience)
        {
            Send(new StoryClosedEvent(), listener);
            if (!quiet && listener.AttachedEntity is { } mob)
                _popup.PopupEntity(Loc.GetString("storyteller-popup-stopped"), mob, listener);
        }

        if (!TerminatingOrDeleted(uid))
        {
            if (!quiet)
                _popup.PopupEntity(Loc.GetString("storyteller-popup-stopped"), uid);
            if (removeComponent)
                RemoveTellerComponent(uid);
        }
    }

    private void RemoveTellerComponent(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !TryComp(uid, out StorytellerComponent? comp) || comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        RemCompDeferred(uid, comp);
    }

    #endregion

    #region Requests

    private bool TellerOf(EntitySessionEventArgs args, out StorySession story)
    {
        story = default!;
        if (args.SenderSession.AttachedEntity is not { } teller)
            return false;

        if (!_sessions.TryGetValue(teller, out var found) || found.TellerSession != args.SenderSession)
            return false;

        story = found;
        return true;
    }

    private void OnLine(StoryLineRequest msg, EntitySessionEventArgs args)
    {
        if (!TellerOf(args, out var story) || story.Script != null)
            return;

        var line = msg.Line;
        if (string.IsNullOrWhiteSpace(line) || line.Length > StorytellerParser.MaxLineLength || StorytellerParser.HasControl(line))
            return;

        if (!Ready(args.SenderSession, 0, 0.4f))
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-wait"), story.Teller, args.SenderSession);
            return;
        }

        line = line.Trim();
        var refs = story.Actors.Where(a => a.Pose != StoryPose.Gone).Select(a => new StoryActorRef(a.Name, a.Sex, a.Role, a.Kind)).ToList();
        var cues = StorytellerParser.Parse(line, Name(story.Teller), refs, _lexicon);

        if (msg.Apply)
        {
            story.Caption = Summary(line);
            ApplyBatch(story, cues, story.Caption);
            Send(new StorySuggestionsEvent(), args.SenderSession);
        }
        else
        {
            story.PendingCaption = Summary(line);
            var labels = cues.Select(c => Label(story, c)).ToArray();
            Send(new StorySuggestionsEvent { Labels = labels, Cues = cues.ToArray() }, args.SenderSession);
        }

        _admin.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(story.Teller):player} story: {line}");
    }

    private void OnCues(StoryCuesRequest msg, EntitySessionEventArgs args)
    {
        if (!TellerOf(args, out var story) || story.Script != null)
            return;

        if (msg.Cues.Length == 0 || msg.Cues.Length > StorytellerParser.MaxCues || !Ready(args.SenderSession, 1, 0.1f))
            return;

        var caption = story.PendingCaption;
        story.PendingCaption = "";
        if (caption.Length > 0)
            story.Caption = caption;

        ApplyBatch(story, msg.Cues, caption);
        Send(new StorySuggestionsEvent(), args.SenderSession);
    }

    private void OnJoin(StoryJoinRequest msg, EntitySessionEventArgs args)
    {
        if (Ready(args.SenderSession, 5, 1f) && TryGetEntity(msg.Teller, out var teller))
            Join(teller.Value, args.SenderSession);
    }

    private void OnLeave(StoryLeaveRequest msg, EntitySessionEventArgs args)
    {
        if (Ready(args.SenderSession, 6, 0.5f))
            Leave(args.SenderSession, true);
    }

    private void OnStop(StoryStopRequest msg, EntitySessionEventArgs args)
    {
        if (TellerOf(args, out var story))
            EndSession(story.Teller, false);
    }

    private void OnWriteBook(StoryWriteBookRequest msg, EntitySessionEventArgs args)
    {
        if (!TellerOf(args, out var story) || story.Script != null || !Ready(args.SenderSession, 2, 2f))
            return;

        var teller = story.Teller;
        if (story.Pages.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-book-empty"), teller, args.SenderSession);
            return;
        }

        if (_hands.GetActiveItem(teller) is not { } book || !_tags.HasTag(book, BookTag) || !TryComp(book, out PaperComponent? paper))
        {
            _popup.PopupEntity(Loc.GetString("storyteller-popup-book-hand"), teller, args.SenderSession);
            return;
        }

        if (!StorytellerParser.CleanText(msg.Title, StorytellerParser.MaxTitleLength, out var title))
            title = Loc.GetString("storyteller-book-untitled");

        var data = EnsureComp<StoryBookComponent>(book);
        data.Title = title;
        data.Pages = story.Pages.Select(p => p.Clone()).ToList();
        story.Title = title;

        var text = new StringBuilder();
        text.AppendLine($"[head=2]{FormattedMessage.EscapeText(title)}[/head]");
        text.AppendLine();
        var number = 1;
        foreach (var page in data.Pages)
        {
            if (page.Caption.Length == 0)
                continue;

            text.AppendLine($"{number++}. {FormattedMessage.EscapeText(Summary(page.Caption))}");
        }

        var content = text.ToString();
        if (content.Length > paper.ContentSize)
            content = content[..paper.ContentSize];

        _paper.SetContent((book, paper), content);
        _popup.PopupEntity(Loc.GetString("storyteller-popup-book-written", ("pages", data.Pages.Count)), teller, args.SenderSession);
        _admin.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(teller):player} wrote story '{title}' into {ToPrettyString(book)}");
        SendScene(story);
    }

    private void OnPage(StoryPageRequest msg, EntitySessionEventArgs args)
    {
        if (!TellerOf(args, out var story) || story.Script == null || !Ready(args.SenderSession, 3, 0.3f))
            return;

        var script = story.Script;
        if (msg.Restart || msg.Back)
        {
            var target = 0;
            if (msg.Back)
            {
                var current = script.FindLastIndex(Math.Max(0, story.ScriptIndex - 1), Math.Max(0, story.ScriptIndex), p => p.Caption.Length > 0);
                if (story.ScriptIndex == 0 || current < 0)
                    return;

                target = current;
            }

            ResetScene(story);
            story.ScriptIndex = 0;
            PlaySetup(story, true);
            while (story.ScriptIndex < target)
            {
                PlayPage(story, script[story.ScriptIndex++], true);
                PlaySetup(story, true);
            }

            var shown = story.ScriptIndex > 0 ? script.FindLastIndex(story.ScriptIndex - 1, p => p.Caption.Length > 0) : -1;
            story.Caption = shown >= 0
                ? Summary(script[shown].Caption)
                : Loc.GetString("storyteller-book-opened", ("title", story.Title));
            SendScene(story);
            return;
        }

        if (story.ScriptIndex >= script.Count)
            return;

        var page = script[story.ScriptIndex++];
        story.Caption = Summary(page.Caption);
        PlayPage(story, page, false);
        PlaySetup(story, false);
        SendScene(story);
    }

    private void ResetScene(StorySession story)
    {
        story.Actors.Clear();
        story.Leaving.Clear();
        story.Effects.Clear();
        story.NextId = 1;
        story.NextProp = 0;
        story.Location = Settings.DefaultLocation;
        ResetProps(story);
        story.Light = "";
        story.Tint = "";
        story.Zoom = 1f;
        story.Focus = 0;
    }

    #endregion

    #region Staging

    private void ApplyBatch(StorySession story, IReadOnlyList<StoryCue> cues, string caption)
    {
        var batch = new Batch();
        var applied = new List<StoryCue>();
        var looks = new Dictionary<string, StoryLook>();
        foreach (var cue in cues.Take(StorytellerParser.MaxCues))
        {
            var recorded = cue;
            if (cue.Kind is StoryCueKind.PropMove or StoryCueKind.PropTurn or StoryCueKind.PropRemove &&
                story.Props.FirstOrDefault(p => p.Id == cue.Prop) is { Proto: not null } before)
            {
                recorded.Id = before.Proto;
                recorded.Text = $"{before.X},{before.Y}";
            }

            var count = story.Actors.Count;
            if (!Apply(story, cue, batch))
                continue;

            if (cue.Kind == StoryCueKind.Enter && story.Actors.Count > count)
            {
                var actor = story.Actors[^1];
                recorded.Actor = actor.Name;
                recorded.Text = "";
                looks[actor.Name] = Snapshot(actor);
            }
            else if (cue.Kind == StoryCueKind.Look && Find(story, cue.Actor) is { } restyled)
            {
                looks[restyled.Name] = Snapshot(restyled);
            }

            applied.Add(recorded);
        }

        if (story.Script == null)
            Record(story, caption, applied, looks);
        SendScene(story);
    }

    private void PlayPage(StorySession story, StoryPage page, bool silent)
    {
        story.ReplayLooks = page.Looks;
        var batch = new Batch();
        foreach (var cue in page.Cues.Take(StorytellerParser.MaxPageCues))
            Apply(story, cue, batch);
        story.ReplayLooks = null;

        if (!silent)
            return;

        story.Actors.RemoveAll(a => a.Pose == StoryPose.Gone);
        story.Leaving.Clear();
        story.Effects.Clear();
    }

    private void PlaySetup(StorySession story, bool silent)
    {
        while (story.Script != null && story.ScriptIndex < story.Script.Count && story.Script[story.ScriptIndex].Caption.Length == 0)
            PlayPage(story, story.Script[story.ScriptIndex++], silent);
    }

    private static StoryLook Snapshot(StoryActorState actor)
    {
        return new StoryLook(actor.Kind, actor.Role, actor.Sex, actor.Appearance, actor.Copy);
    }

    private static string Summary(string line)
    {
        var builder = new StringBuilder(line.Length);
        var space = false;
        foreach (var c in line)
        {
            if (c is '—' or '–')
            {
                if (builder.Length == 0 || space)
                    continue;

                space = true;
                builder.Append(' ');
                continue;
            }

            if (c == ' ')
            {
                if (builder.Length == 0 || space)
                    continue;

                space = true;
                builder.Append(c);
                continue;
            }

            space = false;
            builder.Append(c);
        }

        if (builder.Length > 0 && builder[^1] == ' ')
            builder.Length--;

        return builder.ToString();
    }

    private void Record(StorySession story, string caption, List<StoryCue> cues, Dictionary<string, StoryLook> looks)
    {
        if (caption.Length == 0 && cues.Count == 0)
            return;

        if (caption.Length == 0 && story.Pages.Count > 0 && story.Pages[^1].Cues.Count + cues.Count <= StorytellerParser.MaxPageCues)
        {
            var last = story.Pages[^1];
            last.Cues.AddRange(cues);
            foreach (var (name, look) in looks)
                last.Looks[name] = look;
            return;
        }

        if (story.Pages.Count >= StorytellerParser.MaxPages)
            return;

        story.Pages.Add(new StoryPage { Caption = caption, Cues = cues, Looks = looks });
    }

    private bool Apply(StorySession story, StoryCue cue, Batch batch)
    {
        if (!ValidCue(ref cue))
            return false;

        var location = Location(story);
        switch (cue.Kind)
        {
            case StoryCueKind.Location:
            {
                if (!_prototypes.TryIndex<StoryLocationPrototype>(cue.Id, out var next))
                    return false;

                var follower = cue.Actor.Length > 0 ? Find(story, cue.Actor) : null;
                if (next.ID == story.Location)
                {
                    if (follower is not { Pose: StoryPose.Gone })
                        return false;

                    story.Leaving.RemoveAll(l => l.Id == follower.Id);
                    EnterFromDoor(story, next, follower, StoryMove.None, batch);
                    return true;
                }

                story.Location = next.ID;
                story.Zoom = 1f;
                story.Focus = 0;
                story.Light = "";
                story.Tint = "";
                ResetProps(story);
                if (follower != null)
                {
                    story.Actors.RemoveAll(a => a != follower);
                    story.Leaving.Clear();
                    EnterFromDoor(story, next, follower, StoryMove.None, batch);
                }
                else
                {
                    foreach (var actor in story.Actors)
                        Clamp(next, actor);
                }

                return true;
            }
            case StoryCueKind.Enter:
                return Enter(story, location, cue, batch);
            case StoryCueKind.Leave:
            {
                if (Find(story, cue.Actor) is not { } actor || actor.Pose == StoryPose.Gone)
                    return false;

                var door = Door(location);
                var time = Walk(actor, door, batch, false);
                actor.Pose = StoryPose.Gone;
                story.Leaving.Add((actor.Id, _timing.CurTime + TimeSpan.FromSeconds(time + Settings.FadeTime)));
                return true;
            }
            case StoryCueKind.Move:
            {
                if (Find(story, cue.Actor) is not { } actor || actor.Pose == StoryPose.Gone)
                    return false;

                var target = cue.Target.Length > 0 ? Find(story, cue.Target) : null;
                Vector2 dest;
                if (cue.Move == StoryMove.Target && target != null)
                    dest = Beside(location, actor, target);
                else
                    dest = MoveDest(location, actor, cue.Move);

                if (actor.Pose is not StoryPose.Stand)
                    actor.Pose = StoryPose.Stand;

                Walk(actor, dest, batch, cue.Run);
                if (target != null)
                    actor.Facing = Toward(actor.X, actor.Y, target.X, target.Y);
                return true;
            }
            case StoryCueKind.Pose:
            {
                if (Find(story, cue.Actor) is not { } actor || actor.Pose == StoryPose.Gone)
                    return false;

                actor.Pose = cue.Pose;
                if (cue.Pose is StoryPose.Fallen or StoryPose.Dead)
                    AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Fall, Actor = actor.Id, Delay = batch.At(actor.Id) });
                return true;
            }
            case StoryCueKind.Face:
            {
                if (Find(story, cue.Actor) is not { } actor)
                    return false;

                if (cue.Target.Length > 0 && Find(story, cue.Target) is { } target)
                    actor.Facing = Toward(actor.X, actor.Y, target.X, target.Y);
                else
                    actor.Facing = cue.Move switch
                    {
                        StoryMove.Left => Direction.West,
                        StoryMove.Right => Direction.East,
                        StoryMove.Up => Direction.North,
                        StoryMove.Down => Direction.South,
                        _ => Opposite(actor.Facing),
                    };
                return true;
            }
            case StoryCueKind.Say:
            {
                if (Find(story, cue.Actor) is not { } actor)
                    return false;

                if (!StorytellerParser.CleanText(cue.Text, StorytellerParser.MaxSayLength, out var said))
                    return false;

                AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Say, Actor = actor.Id, Text = said, Delay = batch.At(actor.Id) });
                batch.Push(actor.Id, 0.6f + said.Length * 0.03f);
                return true;
            }
            case StoryCueKind.Emote:
            {
                if (Find(story, cue.Actor) is not { } actor || !_prototypes.HasIndex<StoryEmotePrototype>(cue.Id))
                    return false;

                AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Emote, Actor = actor.Id, Text = cue.Id, Delay = batch.At(actor.Id) });
                batch.Push(actor.Id, 0.8f);
                return true;
            }
            case StoryCueKind.Hit:
            case StoryCueKind.Hug:
            case StoryCueKind.Kiss:
            case StoryCueKind.Give:
            {
                if (Find(story, cue.Actor) is not { } actor || Find(story, cue.Target) is not { } target || actor == target)
                    return false;

                if (actor.Pose is StoryPose.Dead or StoryPose.Gone)
                    return false;

                var item = ItemOr(cue.Id, actor.Held);
                if (cue.Kind == StoryCueKind.Give && item.Length == 0)
                    return false;

                actor.Pose = StoryPose.Stand;
                Walk(actor, Beside(location, actor, target), batch, cue.Kind == StoryCueKind.Hit);
                actor.Facing = Toward(actor.X, actor.Y, target.X, target.Y);
                if (cue.Kind != StoryCueKind.Hit && target.Pose is StoryPose.Stand)
                    target.Facing = Toward(target.X, target.Y, actor.X, actor.Y);

                var delay = batch.At(actor.Id);
                var kind = cue.Kind switch
                {
                    StoryCueKind.Hit => StoryEffectKind.Hit,
                    StoryCueKind.Hug => StoryEffectKind.Hug,
                    StoryCueKind.Kiss => StoryEffectKind.Kiss,
                    _ => StoryEffectKind.Give,
                };

                if (cue.Kind == StoryCueKind.Hit && item.Length > 0)
                    actor.Held = item;

                if (cue.Kind == StoryCueKind.Give)
                {
                    target.Held = item;
                    actor.Held = "";
                }

                AddEffect(story, new StoryEffect { Kind = kind, Actor = actor.Id, Target = target.Id, Item = item, Delay = delay });
                batch.Push(actor.Id, 0.5f);
                batch.Set(target.Id, batch.At(actor.Id));
                return true;
            }
            case StoryCueKind.Shoot:
            {
                if (Find(story, cue.Actor) is not { } actor || actor.Pose is StoryPose.Dead or StoryPose.Gone)
                    return false;

                var target = cue.Target.Length > 0 ? Find(story, cue.Target) : null;
                var item = IsGun(cue.Id) ? cue.Id : actor.Held;
                if (!IsGun(item))
                    return false;

                actor.Held = item;
                if (target != null)
                    actor.Facing = Toward(actor.X, actor.Y, target.X, target.Y);

                AddEffect(story, new StoryEffect
                {
                    Kind = StoryEffectKind.Shoot,
                    Actor = actor.Id,
                    Target = target?.Id ?? 0,
                    Item = item,
                    Delay = batch.At(actor.Id) + 0.25f,
                });
                batch.Push(actor.Id, 0.6f);
                if (target != null)
                    batch.Set(target.Id, batch.At(actor.Id));
                return true;
            }
            case StoryCueKind.Hold:
            {
                if (Find(story, cue.Actor) is not { } actor || !_prototypes.HasIndex<StoryItemPrototype>(cue.Id))
                    return false;

                actor.Held = cue.Id;
                return true;
            }
            case StoryCueKind.Point:
            {
                if (Find(story, cue.Actor) is not { } actor)
                    return false;

                var target = cue.Target.Length > 0 ? Find(story, cue.Target) : null;
                if (target != null)
                    actor.Facing = Toward(actor.X, actor.Y, target.X, target.Y);

                AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Point, Actor = actor.Id, Target = target?.Id ?? 0, Delay = batch.At(actor.Id) });
                return true;
            }
            case StoryCueKind.Dance:
            {
                if (Find(story, cue.Actor) is not { } actor || actor.Pose is not StoryPose.Stand)
                    return false;

                AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Dance, Actor = actor.Id, Delay = batch.At(actor.Id) });
                batch.Push(actor.Id, 2f);
                return true;
            }
            case StoryCueKind.Camera:
            {
                story.Zoom = cue.Run ? Settings.CloseUp : 1f;
                story.Focus = cue.Run && cue.Actor.Length > 0 && Find(story, cue.Actor) is { } focus ? focus.Id : (byte) 0;
                return true;
            }
            case StoryCueKind.Shake:
                AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Shake, Delay = batch.Max() });
                return true;
            case StoryCueKind.Light:
            {
                if (cue.Id.StartsWith(TintPrefix, StringComparison.Ordinal))
                {
                    if (!_prototypes.TryIndex<StoryTintPrototype>(cue.Id[TintPrefix.Length..], out var tint))
                        return false;

                    story.Tint = tint.Color == null ? "" : tint.ID;
                    return true;
                }

                if (!_prototypes.TryIndex<StoryLightPrototype>(cue.Id, out var light))
                    return false;

                story.Light = light.Darkness == null && light.Pulse.A <= 0f ? "" : light.ID;
                return true;
            }
            case StoryCueKind.Look:
                return Restyle(story, cue);
            case StoryCueKind.PropAdd:
            case StoryCueKind.PropMove:
            case StoryCueKind.PropTurn:
            case StoryCueKind.PropRemove:
            case StoryCueKind.PropReset:
            case StoryCueKind.PropClear:
                return EditProp(story, cue);
            default:
                return false;
        }
    }

    private bool EditProp(StorySession story, StoryCue cue)
    {
        var settings = Settings;
        var index = FindProp(story, cue);
        switch (cue.Kind)
        {
            case StoryCueKind.PropAdd:
            {
                if (story.Props.Count >= settings.MaxProps || !InStage(cue.X, cue.Y) || !ValidProp(cue.Id))
                    return false;

                story.Props.Add(new StoryPropState(NextProp(story), cue.Id, cue.X, cue.Y, Cardinal(cue.Dir)));
                return true;
            }
            case StoryCueKind.PropMove:
            {
                if (index < 0 || !InStage(cue.X, cue.Y))
                    return false;

                var prop = story.Props[index];
                prop.X = cue.X;
                prop.Y = cue.Y;
                story.Props[index] = prop;
                return true;
            }
            case StoryCueKind.PropTurn:
            {
                if (index < 0)
                    return false;

                var prop = story.Props[index];
                prop.Dir = prop.Dir switch
                {
                    Direction.South => Direction.West,
                    Direction.West => Direction.North,
                    Direction.North => Direction.East,
                    _ => Direction.South,
                };
                story.Props[index] = prop;
                return true;
            }
            case StoryCueKind.PropRemove:
                if (index < 0)
                    return false;

                story.Props.RemoveAt(index);
                return true;
            case StoryCueKind.PropReset:
                ResetProps(story);
                return true;
            case StoryCueKind.PropClear:
                story.Props.Clear();
                return true;
            default:
                return false;
        }
    }

    private static int FindProp(StorySession story, StoryCue cue)
    {
        var index = story.Props.FindIndex(p => p.Id == cue.Prop);
        if (cue.Id.Length == 0 || !TryOrigin(cue.Text, out var x, out var y))
            return index;

        if (index >= 0 && story.Props[index].Proto == cue.Id && story.Props[index].X == x && story.Props[index].Y == y)
            return index;

        var match = story.Props.FindIndex(p => p.Proto == cue.Id && p.X == x && p.Y == y);
        return match >= 0 ? match : index >= 0 && story.Props[index].Proto == cue.Id ? index : -1;
    }

    private static bool TryOrigin(string text, out int x, out int y)
    {
        x = 0;
        y = 0;
        var comma = text.IndexOf(',');
        return comma > 0 && int.TryParse(text.AsSpan(0, comma), out x) && int.TryParse(text.AsSpan(comma + 1), out y);
    }

    private bool InStage(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Settings.Width && y < Settings.Height;
    }

    private bool ValidProp(string id)
    {
        return id.Length > 0 && _prototypes.TryIndex<EntityPrototype>(id, out var proto) && !proto.Abstract;
    }

    private static Direction Cardinal(Direction dir)
    {
        return dir is Direction.North or Direction.East or Direction.West ? dir : Direction.South;
    }

    private void ResetProps(StorySession story)
    {
        story.Props.Clear();
        if (!_prototypes.TryIndex<StoryLocationPrototype>(story.Location, out var location))
            return;

        foreach (var prop in location.Props.Take(Settings.MaxProps))
        {
            story.Props.Add(new StoryPropState(NextProp(story), prop.Proto.Id, (int) MathF.Floor(prop.Pos.X),
                (int) MathF.Floor(prop.Pos.Y), Cardinal(prop.Dir)));
        }
    }

    private static ushort NextProp(StorySession story)
    {
        story.NextProp = (ushort) (story.NextProp == ushort.MaxValue ? 1 : story.NextProp + 1);
        return story.NextProp;
    }

    private bool Enter(StorySession story, StoryLocationPrototype location, StoryCue cue, Batch batch)
    {
        var self = cue.Text == "self";
        var name = cue.Actor;
        if (self)
            name = Name(story.Teller);

        if (Find(story, name) is { } existing && name.Length > 0)
        {
            if (existing.Pose == StoryPose.Gone)
            {
                story.Leaving.RemoveAll(l => l.Id == existing.Id);
                existing.Pose = StoryPose.Stand;
                EnterFromDoor(story, location, existing, cue.Move, batch);
                return true;
            }

            return false;
        }

        if (story.Actors.Count(a => a.Pose != StoryPose.Gone) >= Settings.MaxActors)
            return false;

        var kind = _prototypes.TryIndex<StoryKindPrototype>(cue.Id, out var kindProto) ? kindProto : _prototypes.Index(Settings.DefaultKind);
        var role = cue.Role.Length > 0 && _prototypes.HasIndex<StoryRolePrototype>(cue.Role) ? cue.Role : "";

        if (name.Length == 0)
            name = Loc.GetString(role.Length > 0 ? _prototypes.Index<StoryRolePrototype>(role).Name : kind.Name);

        if (!StorytellerParser.CleanName(name, out name))
            return false;

        name = Unique(story, name);

        var actor = new StoryActorState
        {
            Id = NextId(story),
            Look = ++story.Look,
            Name = name,
            Kind = kind.ID,
        };

        if (story.ReplayLooks != null && story.ReplayLooks.TryGetValue(name, out var stored))
        {
            Wear(actor, stored);
            story.Actors.Add(actor);
            EnterFromDoor(story, location, actor, cue.Move, batch);
            return true;
        }

        var source = self ? story.Teller : cue.Id.Length == 0 && role.Length == 0 ? FindPlayer(story.Teller, name) : null;
        if (source is { } body && BuildCopy(body) is { } copy)
        {
            actor.Copy = copy;
            actor.Sex = copy.Humanoid?.Sex ?? Sex.Unsexed;
            actor.Kind = KindOf(copy.Humanoid?.Species) ?? (copy.Humanoid == null ? "" : Settings.DefaultKind.Id);
        }
        else if (kind.Species is { } speciesId && _prototypes.TryIndex(speciesId, out SpeciesPrototype? species))
        {
            var guess = StorytellerParser.GuessSex(name, _lexicon);
            var sex = cue.HasSex && species.Sexes.Contains(cue.Sex) ? cue.Sex :
                species.Sexes.Contains(guess) ? guess : species.Sexes.FirstOrDefault(Sex.Unsexed);

            actor.Sex = sex;
            actor.Appearance = HumanoidCharacterAppearance.Random(species.ID, sex);
            actor.Role = role.Length > 0 ? role : Settings.DefaultRole.Id;
        }
        else
        {
            actor.Sex = Sex.Unsexed;
        }

        story.Actors.Add(actor);
        EnterFromDoor(story, location, actor, cue.Move, batch);
        return true;
    }

    private static void Wear(StoryActorState actor, StoryLook look)
    {
        actor.Kind = look.Kind;
        actor.Role = look.Role;
        actor.Sex = look.Sex;
        actor.Appearance = look.Appearance;
        actor.Copy = look.Copy;
    }

    private bool Restyle(StorySession story, StoryCue cue)
    {
        if (Find(story, cue.Actor) is not { } actor)
            return false;

        if (story.ReplayLooks != null && story.ReplayLooks.TryGetValue(actor.Name, out var stored))
        {
            Wear(actor, stored);
            actor.Look = ++story.Look;
            return true;
        }

        var changed = false;
        if (cue.Id.Length > 0 && cue.Id != actor.Kind && _prototypes.TryIndex<StoryKindPrototype>(cue.Id, out var kind))
        {
            actor.Kind = kind.ID;
            actor.Copy = null;
            actor.Appearance = null;
            actor.Role = "";
            if (kind.Species is { } speciesId && _prototypes.TryIndex(speciesId, out SpeciesPrototype? species))
            {
                var sex = cue.HasSex && species.Sexes.Contains(cue.Sex) ? cue.Sex :
                    species.Sexes.Contains(actor.Sex) ? actor.Sex : species.Sexes.FirstOrDefault(Sex.Unsexed);
                actor.Sex = sex;
                actor.Appearance = HumanoidCharacterAppearance.Random(species.ID, sex);
                actor.Role = Settings.DefaultRole;
            }
            else
            {
                actor.Sex = Sex.Unsexed;
            }

            changed = true;
        }

        if (cue.Role.Length > 0 && cue.Role != actor.Role && _prototypes.HasIndex<StoryRolePrototype>(cue.Role) &&
            (actor.Appearance != null || actor.Copy?.Humanoid != null))
        {
            actor.Role = cue.Role;
            if (actor.Copy != null)
                actor.Copy.Equipment = Array.Empty<RoundEndDollEquipment>();
            changed = true;
        }

        if (cue.HasSex && cue.Sex != actor.Sex && actor.Appearance != null &&
            _prototypes.TryIndex<StoryKindPrototype>(actor.Kind, out var current) && current.Species is { } currentSpecies &&
            _prototypes.TryIndex(currentSpecies, out SpeciesPrototype? currentProto) && currentProto.Sexes.Contains(cue.Sex))
        {
            actor.Sex = cue.Sex;
            actor.Appearance = HumanoidCharacterAppearance.Random(currentProto.ID, cue.Sex);
            changed = true;
        }

        if (changed)
            actor.Look = ++story.Look;

        return changed;
    }

    private bool IsGun(string id)
    {
        return id.Length > 0 && _prototypes.TryIndex<StoryItemPrototype>(id, out var item) && item.Use is StoryItemUse.Bullet or StoryItemUse.Laser;
    }

    private void EnterFromDoor(StorySession story, StoryLocationPrototype location, StoryActorState actor, StoryMove side, Batch batch)
    {
        var door = Door(location);
        actor.X = door.X;
        actor.Y = door.Y;
        actor.Pose = StoryPose.Stand;
        actor.Running = false;
        var spot = Spot(story, location, actor, side);
        AddEffect(story, new StoryEffect { Kind = StoryEffectKind.Enter, Actor = actor.Id, Delay = batch.At(actor.Id) });
        Walk(actor, spot, batch, false);
        actor.Facing = Direction.South;
    }

    private float Walk(StoryActorState actor, Vector2 dest, Batch batch, bool run)
    {
        var from = new Vector2(actor.X, actor.Y);
        var distance = Vector2.Distance(from, dest);
        actor.Running = run;
        if (distance < 0.05f)
            return 0f;

        actor.Facing = Toward(actor.X, actor.Y, dest.X, dest.Y);
        actor.X = dest.X;
        actor.Y = dest.Y;
        var time = distance / (run ? Settings.RunSpeed : Settings.WalkSpeed);
        batch.Push(actor.Id, time);
        return time;
    }

    private Vector2 Spot(StorySession story, StoryLocationPrototype location, StoryActorState self, StoryMove side)
    {
        Bounds(location, out var minX, out var maxX, out var minY, out var maxY);
        var midY = Math.Clamp((minY + maxY) / 2f, minY, maxY);
        var rows = new[] { midY, Math.Min(midY + 1.5f, maxY), Math.Max(midY - 1.5f, minY) };
        var xs = side switch
        {
            StoryMove.Left => Settings.Spots.OrderBy(x => x).ToArray(),
            StoryMove.Right => Settings.Spots.OrderByDescending(x => x).ToArray(),
            _ => Settings.Spots.ToArray(),
        };

        foreach (var y in rows)
        {
            foreach (var x in xs)
            {
                if (x < minX || x > maxX)
                    continue;

                var spot = new Vector2(x, y);
                if (story.Actors.All(a => a == self || a.Pose == StoryPose.Gone || Vector2.Distance(new Vector2(a.X, a.Y), spot) > 0.9f))
                    return spot;
            }
        }

        return new Vector2((minX + maxX) / 2f, midY);
    }

    private Vector2 Beside(StoryLocationPrototype location, StoryActorState actor, StoryActorState target)
    {
        Bounds(location, out var minX, out var maxX, out var minY, out var maxY);
        var side = actor.X <= target.X ? -1f : 1f;
        var x = target.X + side;
        if (x < minX || x > maxX)
            x = target.X - side;

        return new Vector2(Math.Clamp(x, minX, maxX), Math.Clamp(target.Y, minY, maxY));
    }

    private Vector2 MoveDest(StoryLocationPrototype location, StoryActorState actor, StoryMove move)
    {
        Bounds(location, out var minX, out var maxX, out var minY, out var maxY);
        var dest = move switch
        {
            StoryMove.Left => new Vector2(actor.X - Settings.StepX, actor.Y),
            StoryMove.Right => new Vector2(actor.X + Settings.StepX, actor.Y),
            StoryMove.Up => new Vector2(actor.X, actor.Y - Settings.StepY),
            StoryMove.Down => new Vector2(actor.X, actor.Y + Settings.StepY),
            StoryMove.Center => new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f),
            StoryMove.Door => Door(location) + new Vector2(1f, 0f),
            _ => new Vector2(actor.X, actor.Y) + Step(actor.Facing) * 2f,
        };

        return new Vector2(Math.Clamp(dest.X, minX, maxX), Math.Clamp(dest.Y, minY, maxY));
    }

    private static Vector2 Step(Direction facing)
    {
        return facing switch
        {
            Direction.North => new Vector2(0f, -1f),
            Direction.East => new Vector2(1f, 0f),
            Direction.West => new Vector2(-1f, 0f),
            _ => new Vector2(0f, 1f),
        };
    }

    private Vector2 Door(StoryLocationPrototype location)
    {
        return StoryStage.Door(Settings, location);
    }

    private void Clamp(StoryLocationPrototype location, StoryActorState actor)
    {
        Bounds(location, out var minX, out var maxX, out var minY, out var maxY);
        actor.X = Math.Clamp(actor.X, minX, maxX);
        actor.Y = Math.Clamp(actor.Y, minY, maxY);
    }

    private void Bounds(StoryLocationPrototype location, out float minX, out float maxX, out float minY, out float maxY)
    {
        StoryStage.Bounds(Settings, location, out minX, out maxX, out minY, out maxY);
    }

    private static Direction Toward(float fromX, float fromY, float toX, float toY)
    {
        var dx = toX - fromX;
        var dy = toY - fromY;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0f ? Direction.East : Direction.West;

        return dy >= 0f ? Direction.South : Direction.North;
    }

    private static Direction Opposite(Direction facing)
    {
        return facing switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };
    }

    private string ItemOr(string id, string held)
    {
        if (id.Length > 0 && _prototypes.HasIndex<StoryItemPrototype>(id))
            return id;

        return held;
    }

    private bool ValidCue(ref StoryCue cue)
    {
        if (!Enum.IsDefined(cue.Kind) || !Enum.IsDefined(cue.Pose) || !Enum.IsDefined(cue.Move) || !Enum.IsDefined(cue.Sex) ||
            !Enum.IsDefined(cue.Dir))
        {
            return false;
        }

        if (!float.IsFinite(cue.Zoom))
            return false;

        cue.Actor ??= "";
        cue.Target ??= "";
        cue.Id ??= "";
        cue.Role ??= "";
        cue.Text ??= "";

        if (cue.Actor.Length > StorytellerParser.MaxNameLength || cue.Target.Length > StorytellerParser.MaxNameLength ||
            cue.Id.Length > 64 || cue.Role.Length > 64 || cue.Text.Length > StorytellerParser.MaxSayLength)
        {
            return false;
        }

        return !StorytellerParser.HasControl(cue.Actor) && !StorytellerParser.HasControl(cue.Target) &&
               !StorytellerParser.HasControl(cue.Id) && !StorytellerParser.HasControl(cue.Role);
    }

    private StoryLocationPrototype Location(StorySession story)
    {
        if (_prototypes.TryIndex<StoryLocationPrototype>(story.Location, out var location))
            return location;

        story.Location = Settings.DefaultLocation;
        return _prototypes.Index(Settings.DefaultLocation);
    }

    private static StoryActorState? Find(StorySession story, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        foreach (var actor in story.Actors)
        {
            if (StorytellerParser.SameName(actor.Name, name))
                return actor;
        }

        var folded = StorytellerParser.Fold(name);
        foreach (var actor in story.Actors)
        {
            foreach (var part in actor.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (StorytellerParser.NameMatch(folded, part))
                    return actor;
            }
        }

        return null;
    }

    private static string Unique(StorySession story, string name)
    {
        if (story.Actors.All(a => !StorytellerParser.SameName(a.Name, name)))
            return name;

        for (var i = 2; i < 20; i++)
        {
            var candidate = $"{name} {i}";
            if (candidate.Length <= StorytellerParser.MaxNameLength && story.Actors.All(a => !StorytellerParser.SameName(a.Name, candidate)))
                return candidate;
        }

        return name;
    }

    private static byte NextId(StorySession story)
    {
        for (var i = 0; i < 255; i++)
        {
            var id = story.NextId++;
            if (id == 0)
                continue;

            if (story.Actors.All(a => a.Id != id))
                return id;
        }

        return 255;
    }

    private string? KindOf(string? species)
    {
        if (species == null)
            return null;

        foreach (var kind in _prototypes.EnumeratePrototypes<StoryKindPrototype>())
        {
            if (kind.Species == species)
                return kind.ID;
        }

        return null;
    }

    private EntityUid? FindPlayer(EntityUid teller, string name)
    {
        var folded = StorytellerParser.Fold(name);
        var single = !folded.Contains(' ');
        foreach (var (uid, _) in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(Transform(teller).Coordinates, Settings.LookRange))
        {
            if (!HasComp<ActorComponent>(uid))
                continue;

            var other = StorytellerParser.Fold(Name(uid));
            if (other == folded)
                return uid;

            if (single && other.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() == folded)
                return uid;
        }

        return null;
    }

    private RoundEndDollData? BuildCopy(EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return null;

        var data = new RoundEndDollData { BodyPrototype = MetaData(body).EntityPrototype?.ID };
        if (TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
        {
            data.Humanoid = new RoundEndHumanoidAppearance
            {
                Species = humanoid.Species,
                Markings = new MarkingSet(humanoid.MarkingSet),
                PermanentlyHidden = new HashSet<HumanoidVisualLayers>(humanoid.PermanentlyHidden),
                CustomBaseLayers = new Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo>(humanoid.CustomBaseLayers),
                Gender = humanoid.Gender,
                Age = humanoid.Age,
                SkinColor = humanoid.SkinColor,
                Sex = humanoid.Sex,
                EyeColor = humanoid.EyeColor,
                BodyDye = humanoid.BodyDye, // DS14
            };
        }

        if (TryComp<InventoryComponent>(body, out var inventory))
        {
            var equipment = new List<RoundEndDollEquipment>();
            var slots = _inventory.GetSlotEnumerator((body, inventory));
            while (slots.NextItem(out var item, out var slot))
            {
                if (MetaData(item).EntityPrototype?.ID is { } prototype)
                    equipment.Add(new RoundEndDollEquipment { Slot = slot.Name, Prototype = prototype });
            }

            data.Equipment = equipment.ToArray();
        }

        return data;
    }

    private string Label(StorySession story, StoryCue cue)
    {
        string Item(string id) => _prototypes.TryIndex<StoryItemPrototype>(id, out var p) ? $" ({Loc.GetString(p.Name)})" : "";
        var text = cue.Kind switch
        {
            StoryCueKind.Location => _prototypes.TryIndex<StoryLocationPrototype>(cue.Id, out var place)
                ? Loc.GetString(cue.Actor.Length > 0 ? "storyteller-cue-location-follow" : "storyteller-cue-location",
                    ("place", Loc.GetString(place.Name)), ("name", cue.Actor))
                : "",
            StoryCueKind.Enter => Loc.GetString("storyteller-cue-enter",
                ("name", cue.Text == "self" ? Name(story.Teller) : cue.Actor),
                ("look", Look(cue))),
            StoryCueKind.Leave => Loc.GetString("storyteller-cue-leave", ("name", cue.Actor)),
            StoryCueKind.Move => cue.Target.Length > 0
                ? Loc.GetString(cue.Run ? "storyteller-cue-run-to" : "storyteller-cue-walk-to", ("name", cue.Actor), ("target", cue.Target))
                : Loc.GetString(cue.Run ? "storyteller-cue-run" : "storyteller-cue-walk", ("name", cue.Actor), ("way", Loc.GetString("storyteller-move-" + cue.Move))),
            StoryCueKind.Pose => Loc.GetString("storyteller-cue-pose", ("name", cue.Actor), ("pose", Loc.GetString("storyteller-pose-" + cue.Pose))),
            StoryCueKind.Face => Loc.GetString("storyteller-cue-face", ("name", cue.Actor)),
            StoryCueKind.Say => Loc.GetString("storyteller-cue-say", ("name", cue.Actor), ("text", cue.Text)),
            StoryCueKind.Emote => Loc.GetString("storyteller-cue-emote", ("name", cue.Actor),
                ("emote", _prototypes.TryIndex<StoryEmotePrototype>(cue.Id, out var emote) ? Loc.GetString(emote.Name) : cue.Id)),
            StoryCueKind.Hit => Loc.GetString("storyteller-cue-hit", ("name", cue.Actor), ("target", cue.Target), ("item", Item(cue.Id))),
            StoryCueKind.Shoot => Loc.GetString(cue.Target.Length > 0 ? "storyteller-cue-shoot" : "storyteller-cue-shoot-air",
                ("name", cue.Actor), ("target", cue.Target), ("item", Item(cue.Id))),
            StoryCueKind.Hug => Loc.GetString("storyteller-cue-hug", ("name", cue.Actor), ("target", cue.Target)),
            StoryCueKind.Kiss => Loc.GetString("storyteller-cue-kiss", ("name", cue.Actor), ("target", cue.Target)),
            StoryCueKind.Give => Loc.GetString("storyteller-cue-give", ("name", cue.Actor), ("target", cue.Target), ("item", Item(cue.Id))),
            StoryCueKind.Hold => Loc.GetString("storyteller-cue-hold", ("name", cue.Actor), ("item", Item(cue.Id))),
            StoryCueKind.Point => Loc.GetString(cue.Target.Length > 0 ? "storyteller-cue-point" : "storyteller-cue-point-air",
                ("name", cue.Actor), ("target", cue.Target)),
            StoryCueKind.Dance => Loc.GetString("storyteller-cue-dance", ("name", cue.Actor)),
            StoryCueKind.Camera => Loc.GetString(cue.Run ? "storyteller-cue-camera-in" : "storyteller-cue-camera-out"),
            StoryCueKind.Shake => Loc.GetString("storyteller-cue-shake"),
            StoryCueKind.Look => Loc.GetString("storyteller-cue-look", ("name", cue.Actor), ("look", Look(cue))),
            StoryCueKind.Light when cue.Id.StartsWith(TintPrefix, StringComparison.Ordinal) =>
                _prototypes.TryIndex<StoryTintPrototype>(cue.Id[TintPrefix.Length..], out var tint)
                    ? Loc.GetString("storyteller-cue-tint", ("tint", Loc.GetString(tint.Name)))
                    : "",
            StoryCueKind.Light => _prototypes.TryIndex<StoryLightPrototype>(cue.Id, out var light)
                ? Loc.GetString("storyteller-cue-light", ("light", Loc.GetString(light.Name)))
                : "",
            _ => "",
        };

        return text.Length > 90 ? text[..90] : text;
    }

    private string Look(StoryCue cue)
    {
        var parts = new List<string>();
        if (_prototypes.TryIndex<StoryKindPrototype>(cue.Id, out var kind))
            parts.Add(Loc.GetString(kind.Name));
        if (cue.Role.Length > 0 && _prototypes.TryIndex<StoryRolePrototype>(cue.Role, out var role))
            parts.Add(Loc.GetString(role.Name));
        if (cue.HasSex && cue.Sex is Sex.Male or Sex.Female)
            parts.Add(Loc.GetString("storyteller-sex-" + cue.Sex));

        return parts.Count == 0 ? "" : $" ({string.Join(", ", parts)})";
    }

    #endregion

    #region Network

    private void AddEffect(StorySession story, StoryEffect effect)
    {
        if (story.Effects.Count < MaxEffects)
            story.Effects.Add(effect);
    }

    private void SendScene(StorySession story, ICommonSession? only = null)
    {
        story.Revision++;
        var props = story.Props.ToArray();
        var effects = only == null ? story.Effects.ToArray() : Array.Empty<StoryEffect>();
        var listeners = story.Audience
            .Where(s => s.AttachedEntity != null)
            .Select(s => new StoryListener(GetNetEntity(s.AttachedEntity!.Value), Name(s.AttachedEntity!.Value)))
            .ToArray();
        if (only == null)
            story.Effects.Clear();

        if (only != null)
        {
            SendSceneTo(story, only, props, effects, listeners);
            return;
        }

        SendSceneTo(story, story.TellerSession, props, effects, listeners);
        foreach (var listener in story.Audience)
            SendSceneTo(story, listener, props, effects, listeners);
    }

    private void SendSceneTo(StorySession story, ICommonSession session, StoryPropState[] props, StoryEffect[] effects, StoryListener[] listeners)
    {
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
            return;

        if (!story.SentLooks.TryGetValue(session.UserId, out var sent))
        {
            sent = new Dictionary<byte, ushort>();
            story.SentLooks[session.UserId] = sent;
        }

        var actors = new StoryActorState[story.Actors.Count];
        for (var i = 0; i < actors.Length; i++)
        {
            var actor = story.Actors[i];
            if (sent.TryGetValue(actor.Id, out var look) && look == actor.Look)
            {
                actors[i] = WithoutLook(actor);
                continue;
            }

            sent[actor.Id] = actor.Look;
            actors[i] = actor;
        }

        var ev = new StorySceneEvent
        {
            Teller = GetNetEntity(story.Teller),
            TellerName = Name(story.Teller),
            Caption = story.Caption,
            Location = story.Location,
            Light = story.Light,
            Tint = story.Tint,
            Zoom = story.Zoom,
            Focus = story.Focus,
            Revision = story.Revision,
            Actors = actors,
            Props = props,
            Effects = effects,
            Listeners = listeners,
            Pages = story.Pages.Count,
            ScriptPage = story.Script == null ? 0 : story.Script.Take(story.ScriptIndex).Count(p => p.Caption.Length > 0),
            ScriptPages = story.Script?.Count(p => p.Caption.Length > 0) ?? 0,
            Reading = story.Script != null,
            Title = story.Title,
        };

        RaiseNetworkEvent(ev, Filter.SinglePlayer(session), false);
    }

    private static StoryActorState WithoutLook(StoryActorState actor)
    {
        return new StoryActorState
        {
            Id = actor.Id,
            Look = actor.Look,
            Name = actor.Name,
            Kind = actor.Kind,
            Role = actor.Role,
            Sex = actor.Sex,
            X = actor.X,
            Y = actor.Y,
            Facing = actor.Facing,
            Pose = actor.Pose,
            Running = actor.Running,
            Held = actor.Held,
        };
    }

    private void Send(EntityEventArgs ev, ICommonSession session)
    {
        var filter = Filter.Empty();
        AddPlayer(filter, session);
        if (filter.Count > 0)
            RaiseNetworkEvent(ev, filter);
    }

    private static void AddPlayer(Filter filter, ICommonSession session)
    {
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
            return;

        filter.AddPlayer(session);
    }

    private bool Listening(ICommonSession session)
    {
        return _sessions.Values.Any(s => s.Audience.Contains(session));
    }

    private bool InRange(EntityUid origin, EntityUid other, float range, bool walls)
    {
        if (!TryComp(origin, out TransformComponent? from) || !TryComp(other, out TransformComponent? to))
            return false;

        if (!_transform.InRange(from.Coordinates, to.Coordinates, range))
            return false;

        if (!walls || HasComp<GhostComponent>(other))
            return true;

        return _interaction.InRangeUnobstructed(origin, other, range);
    }

    private bool Ready(ICommonSession session, int gate, float gap)
    {
        var key = (session.UserId, gate);
        var now = _timing.CurTime;
        if (_gates.TryGetValue(key, out var next) && now < next)
            return false;

        _gates[key] = now + TimeSpan.FromSeconds(gap);
        return true;
    }

    private static SpriteSpecifier BookIcon()
    {
        return new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Misc/books.rsi"), "book_icon");
    }

    #endregion

    private sealed class Batch
    {
        private readonly Dictionary<byte, float> _ready = new();

        public float At(byte id)
        {
            return _ready.GetValueOrDefault(id);
        }

        public void Push(byte id, float time)
        {
            _ready[id] = At(id) + time;
        }

        public void Set(byte id, float time)
        {
            _ready[id] = Math.Max(At(id), time);
        }

        public float Max()
        {
            return _ready.Count == 0 ? 0f : _ready.Values.Max();
        }
    }

    private sealed class StorySession
    {
        public EntityUid Teller;
        public ICommonSession TellerSession;
        public readonly HashSet<ICommonSession> Audience = new();
        public readonly List<StoryActorState> Actors = new();
        public readonly List<StoryPropState> Props = new();
        public readonly Dictionary<NetUserId, Dictionary<byte, ushort>> SentLooks = new();
        public Dictionary<string, StoryLook>? ReplayLooks;
        public ushort NextProp;
        public readonly List<(byte Id, TimeSpan At)> Leaving = new();
        public readonly List<StoryEffect> Effects = new();
        public readonly List<StoryPage> Pages = new();
        public List<StoryPage>? Script;
        public int ScriptIndex;
        public string Title = "";
        public string Location = "";
        public string Light = "";
        public string Tint = "";
        public float Zoom = 1f;
        public byte Focus;
        public string Caption = "";
        public string PendingCaption = "";
        public ushort Revision;
        public ushort Look;
        public byte NextId = 1;
        public TimeSpan NextRangeCheck;

        public StorySession(EntityUid teller, ICommonSession session, TimeSpan now)
        {
            Teller = teller;
            TellerSession = session;
            NextRangeCheck = now + TimeSpan.FromSeconds(1);
        }
    }
}
