// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Paper;
using Content.Server.Pinpointer;
using Content.Server.SurveillanceCamera;
using Content.Server.Station.Systems;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.CameraArchives;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using System.IO;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.CameraArchives;

public sealed class CameraArchiveConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly CameraArchiveSystem _stills = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly NavMapSystem _nav = default!;
    [Dependency] private readonly SurveillanceCameraSystem _cameras = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustSerializer _serializer = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CameraArchiveSystem.CameraArchiveHistoryEvent>(OnHistory);
    }

    public bool AcceptPrint(EntityUid monitor, CameraArchivePrintMessage message)
    {
        if (!TryComp<CameraArchiveConsoleComponent>(monitor, out var comp) || _timing.CurTime < comp.NextPrint)
            return false;

        var camera = GetEntity(message.Camera);
        var index = message.FrameIndex;
        if (message.FrameId != 0 && !_stills.TryFindFrame(camera, message.FrameId, out index))
            return false;

        if (!TryPrint(monitor, message.Actor, camera, index, out var paper))
            return false;

        if (TryComp<CameraArchivePrintComponent>(paper, out var shot))
        {
            shot.Magnification = Math.Clamp(message.Magnification, 1f, 4f);
            shot.Pan = message.Pan;
            Dirty(paper, shot);
        }

        comp.NextPrint = _timing.CurTime + comp.PrintDelay;
        _audio.PlayPvs(comp.PrintSound, monitor);
        return true;
    }

    public bool TryPrint(
        EntityUid console,
        EntityUid actor,
        EntityUid camera,
        int index,
        out EntityUid paper)
    {
        paper = default;
        if (!TryComp<CameraArchiveConsoleComponent>(console, out var comp))
            return false;

        if (index < 0)
            index = _stills.Count(camera) - 1;

        if (!_stills.TryGetFrame(camera, index, out var frame))
            return false;

        if (!_prototypes.TryIndex(comp.Form, out PaperworkFormPrototype? form))
            return false;

        using var reader = _resources.ContentFileReadText(form.Text);
        var text = reader.ReadToEnd();
        var stationName = _station.GetOwningStation(console) is { } station ? Name(station) : null;
        text = PaperworkTextSubstitutions.ApplyBase(text, Loc.GetString(form.Name), _ticker.RoundDuration(), stationName);

        var authorName = Loc.GetString("camera-archive-unknown-officer");
        var authorJob = string.Empty;
        if (actor.IsValid())
        {
            authorName = Name(actor);
            if (_idCard.TryFindIdCard(actor, out var card))
                authorJob = card.Comp.LocalizedJobTitle ?? string.Empty;
        }

        text = text.Replace("{{AUTHOR.NAME}}", BreakLongWords(authorName));
        text = text.Replace("{{AUTHOR.JOB}}", BreakLongWords(authorJob));
        text = text.Replace("{{CAMERA.NAME}}", BreakLongWords(CameraLabel(camera)));
        text = text.Replace("{{FRAME.TIME}}", frame.RoundTime.ToString("hh\\:mm\\:ss"));
        var chat = string.Empty;
        if (frame.Still.Chat.Count > 0)
        {
            var title = Loc.GetString("camera-archive-chat");
            var lines = new string[frame.Still.Chat.Count];
            for (var i = 0; i < lines.Length; i++)
                lines[i] = BreakLongWords(frame.Still.Chat[i]);
            chat = $"\n[bold]{title}[/bold]\n{string.Join('\n', lines)}\n";
        }

        text = text.Replace("{ЧАТ}", chat);

        paper = Spawn(form.PaperPrototype, Transform(console).Coordinates.Offset(new Vector2(0f, -0.7f)));
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), text);

        var shot = EnsureComp<CameraArchivePrintComponent>(paper);
        using (var stream = new MemoryStream())
        {
            _serializer.SerializeDirect(stream, frame.Still);
            shot.Still = stream.ToArray();
        }

        Dirty(paper, shot);

        if (actor.IsValid())
            _popup.PopupEntity(Loc.GetString("camera-archive-printed"), console, actor);

        return true;
    }

    private static string BreakLongWords(string text)
    {
        const int limit = 24;
        var built = new char[text.Length + text.Length / limit + 1];
        var count = 0;
        var run = 0;
        foreach (var c in text)
        {
            if (c is ' ' or '\n' or '-' or '\t')
            {
                run = 0;
                built[count++] = c;
                continue;
            }

            if (run == limit)
            {
                built[count++] = ' ';
                run = 0;
            }

            built[count++] = c;
            run++;
        }

        return new string(built, 0, count);
    }

    public void Opened(EntityUid monitor)
    {
        if (!TryComp<CameraArchiveConsoleComponent>(monitor, out var comp))
            return;

        comp.ArchiveOpen = true;
        comp.Watching = default;
        comp.WatchingId = 0;
        comp.WatchingIndex = -1;
    }

    public void Closed(EntityUid monitor)
    {
        if (!TryComp<CameraArchiveConsoleComponent>(monitor, out var comp))
            return;

        comp.ArchiveOpen = false;
        comp.Watching = default;
        comp.WatchingId = 0;
        comp.WatchingIndex = -1;
    }

    public SurveillanceCameraArchiveBrowseMessage Answer(EntityUid monitor, SurveillanceCameraArchivePickMessage message)
    {
        var camera = GetEntity(message.Camera);
        var index = message.FrameIndex;
        if (message.FrameId != 0)
        {
            if (!_stills.TryFindFrame(camera, message.FrameId, out var current))
                index = int.MaxValue;
            else
                index = current + message.FrameIndex;
        }

        var browse = Browse(message.Camera, index);
        Note(monitor, camera, browse);
        return browse;
    }

    public SurveillanceCameraArchiveBrowseMessage Browse(NetEntity selected, int index)
    {
        var state = Build(selected, index, 0);
        return new SurveillanceCameraArchiveBrowseMessage
        {
            Cameras = state.Cameras,
            Selected = state.Selected,
            FrameIndex = state.FrameIndex,
            FrameId = state.FrameId,
            FrameCount = state.FrameCount,
            FrameTime = state.FrameTime,
            Frame = state.Frame,
            Covered = state.Covered,
            BodyLog = !state.Selected.Equals(default(NetEntity)) && _stills.IsBodyCamera(GetEntity(state.Selected)),
        };
    }

    private void OnHistory(ref CameraArchiveSystem.CameraArchiveHistoryEvent ev)
    {
        var query = EntityQueryEnumerator<CameraArchiveConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.ArchiveOpen || comp.WatchingId == 0)
                continue;

            if (ev.Camera is { } camera && camera != comp.Watching)
                continue;

            if (!Exists(comp.Watching) || !_stills.TryFindFrame(comp.Watching, comp.WatchingId, out var index))
            {
                if (Exists(comp.Watching) && _stills.TryFindNearest(comp.Watching, comp.WatchingTime, out var nearest))
                {
                    var browse = Browse(GetNetEntity(comp.Watching), nearest);
                    Note(uid, comp.Watching, browse);
                    Send(uid, browse);
                    continue;
                }

                comp.Watching = default;
                comp.WatchingId = 0;
                comp.WatchingIndex = -1;
                Send(uid, Browse(default, int.MaxValue));
                continue;
            }

            comp.WatchingIndex = index;
        }
    }

    private void Note(EntityUid monitor, EntityUid camera, SurveillanceCameraArchiveBrowseMessage browse)
    {
        if (!TryComp<CameraArchiveConsoleComponent>(monitor, out var comp))
            return;

        comp.ArchiveOpen = true;
        comp.Watching = camera;
        comp.WatchingId = browse.FrameId;
        comp.WatchingIndex = browse.Frame == null ? -1 : browse.FrameIndex;
        comp.WatchingTime = browse.FrameTime;
    }

    private void Send(EntityUid monitor, SurveillanceCameraArchiveBrowseMessage browse)
    {
        _ui.ServerSendUiMessage(monitor, SurveillanceCameraMonitorUiKey.Key, browse);
    }

    private CameraArchiveState Build(NetEntity selected, int index, int request)
    {
        var entries = new List<CameraArchiveEntry>();
        var query = EntityQueryEnumerator<SurveillanceCameraComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out _))
        {
            if (xform.GridUid == null || !_stills.Listed(uid))
                continue;

            var count = _stills.Count(uid);
            entries.Add(new CameraArchiveEntry
            {
                Camera = GetNetEntity(uid),
                Name = CameraLabel(uid),
                Count = count,
                Offline = !_stills.IsOnline(uid) || !_cfg.GetCVar(CCVars.CameraArchiveActive),
                Covered = HasComp<CameraArchiveSealedComponent>(uid),
                Burned = HasComp<CameraArchiveBurnedComponent>(uid),
            });
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var state = new CameraArchiveState
        {
            Cameras = entries.ToArray(),
            Request = request,
            ServerDown = !_cfg.GetCVar(CCVars.CameraArchiveActive),
        };
        CameraArchiveEntry? picked = null;
        foreach (var entry in entries)
        {
            if (entry.Camera == selected)
            {
                picked = entry;
                break;
            }
        }

        if (picked == null)
            return state;

        var cameraUid = GetEntity(picked.Camera);
        var countFrames = _stills.Count(cameraUid);
        state.Selected = picked.Camera;
        state.FrameCount = countFrames;
        state.Covered = picked.Covered;
        if (index < 0)
        {
            if (countFrames > 0 && _stills.TryGetFrame(cameraUid, countFrames - 1, out var latest))
            {
                state.FrameIndex = countFrames - 1;
                state.FrameId = latest.Id;
                state.FrameTime = latest.RoundTime;
                state.Frame = latest.Still;
            }
        }
        else if (_stills.TryGetFrame(cameraUid, index, out var frame))
        {
            state.FrameIndex = index;
            state.FrameId = frame.Id;
            state.FrameTime = frame.RoundTime;
            state.Frame = frame.Still;
        }

        return state;
    }

    private string CameraLabel(EntityUid uid)
    {
        if (_cameras.GetConfiguredName(uid) is { } configured)
            return StripDigits(configured);

        var beacon = StripDigits(FormattedMessage.RemoveMarkupOrThrow(_nav.GetNearestBeaconString(uid)));
        var missing = StripDigits(Loc.GetString("nav-beacon-pos-no-beacons"));
        if (string.IsNullOrWhiteSpace(beacon) || beacon == missing)
            beacon = Name(uid);

        if (Transform(uid).GridUid is { } grid
            && !HasComp<StationMemberComponent>(grid))
            beacon += ", " + Loc.GetString("camera-archive-off-station");

        return beacon;
    }

    private static string StripDigits(string text)
    {
        var chars = new char[text.Length];
        var count = 0;
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
                continue;

            chars[count++] = c;
        }

        return new string(chars, 0, count).Trim();
    }
}
