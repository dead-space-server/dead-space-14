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
using Robust.Shared.Audio.Systems;
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

    public bool AcceptPrint(EntityUid monitor, CameraArchivePrintMessage message)
    {
        if (!TryComp<CameraArchiveConsoleComponent>(monitor, out var comp) || _timing.CurTime < comp.NextPrint)
            return false;

        if (!TryPrint(monitor, message.Actor, GetEntity(message.Camera), message.FrameIndex, message.Image, out _))
            return false;

        comp.NextPrint = _timing.CurTime + comp.PrintDelay;
        _audio.PlayPvs(comp.PrintSound, monitor);
        return true;
    }

    public bool TryPrint(
        EntityUid console,
        EntityUid actor,
        EntityUid camera,
        int index,
        byte[]? image,
        out EntityUid paper)
    {
        paper = default;
        if (!TryComp<CameraArchiveConsoleComponent>(console, out var comp))
            return false;

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

        text = text.Replace("{{AUTHOR.NAME}}", authorName);
        text = text.Replace("{{AUTHOR.JOB}}", authorJob);
        text = text.Replace("{{CAMERA.NAME}}", CameraLabel(camera));
        text = text.Replace("{{FRAME.TIME}}", frame.RoundTime.ToString("hh\\:mm\\:ss"));
        var chat = string.Empty;
        if (frame.Still.Chat.Count > 0)
        {
            const string rule = "═════════════════════════════════════";
            var title = Loc.GetString("camera-archive-chat");
            chat = $"\n{rule}\n[bold]{title}[/bold]\n{string.Join('\n', frame.Still.Chat)}\n{rule}";
        }

        text = text.Replace("{ЧАТ}", chat);

        paper = Spawn(form.PaperPrototype, Transform(console).Coordinates.Offset(new Vector2(0f, -0.7f)));
        if (TryComp<PaperComponent>(paper, out var paperComp))
            _paper.SetContent((paper, paperComp), text);

        if (image != null && image.Length > 0 && image.Length <= CameraArchivePrintMessage.MaxImageBytes)
        {
            var shot = EnsureComp<CameraArchivePrintComponent>(paper);
            shot.Image = image;
            Dirty(paper, shot);
        }

        if (actor.IsValid())
            _popup.PopupEntity(Loc.GetString("camera-archive-printed"), console, actor);

        return true;
    }

    public SurveillanceCameraArchiveBrowseMessage Browse(NetEntity selected, int index)
    {
        var state = Build(selected, index, 0);
        return new SurveillanceCameraArchiveBrowseMessage
        {
            Cameras = state.Cameras,
            Selected = state.Selected,
            FrameIndex = state.FrameIndex,
            FrameCount = state.FrameCount,
            FrameTime = state.FrameTime,
            Frame = state.Frame,
            Covered = state.Covered,
            BodyLog = !state.Selected.Equals(default(NetEntity)) && _stills.IsBodyCamera(GetEntity(state.Selected)),
        };
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
        if (countFrames > 0 && _stills.TryGetFrame(cameraUid, index, out var frame))
        {
            var resolved = index < 0 || index >= countFrames ? countFrames - 1 : index;
            state.FrameIndex = resolved;
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
