using System.Linq;
using Content.Shared.DeadSpace.Ports.Jukebox;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Server.GameStates;
using Robust.Shared.Containers;
using Robust.Shared.Utility;
using Robust.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.Ports.Jukebox;

public sealed class JukeboxSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverrideSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly List<Entity<WhiteJukeboxComponent>> _playingJukeboxes = new() { };

    private const float UpdateTimerDefaultTime = 1f;
    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<JukeboxRequestSongPlay>(OnSongRequestPlay);
        SubscribeLocalEvent<WhiteJukeboxComponent, InteractUsingEvent>(OnInteract);
        SubscribeLocalEvent<WhiteJukeboxComponent, JukeboxStopRequest>(OnRequestStop);
        SubscribeLocalEvent<WhiteJukeboxComponent, JukeboxRepeatToggled>(OnRepeatToggled);
        SubscribeLocalEvent<WhiteJukeboxComponent, JukeboxEjectRequest>(OnEjectRequest);
        SubscribeLocalEvent<WhiteJukeboxComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<WhiteJukeboxComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnInit(EntityUid uid, WhiteJukeboxComponent component, ComponentInit args)
    {
        _pvsOverrideSystem.AddGlobalOverride(uid);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _playingJukeboxes.Clear();
    }

    private void OnEjectRequest(EntityUid uid, WhiteJukeboxComponent component, JukeboxEjectRequest args)
    {
        if (component.PlayingSongData != null) return;

        var containedEntities = component.TapeContainer.ContainedEntities;

        if (containedEntities.Count > 0)
        {
            _containerSystem.EmptyContainer(component.TapeContainer, true);
        }
    }

    private void OnGetVerb(Entity<WhiteJukeboxComponent> ent, ref GetVerbsEvent<Verb> ev)
    {
        if (ev.Hands == null) return;
        if (ent.Comp.PlayingSongData != null) return;
        if (ent.Comp.TapeContainer.ContainedEntities.Count == 0) return;

        var user = ev.User;

        var removeTapeVerb = new Verb
        {
            Text = "Вытащить касету",
            Priority = 10000,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/remove_tape.png")),
            Act = () =>
            {
                var tapes = ent.Comp.TapeContainer.ContainedEntities.ToList();
                _containerSystem.EmptyContainer(ent.Comp.TapeContainer, true);

                foreach (var tape in tapes)
                {
                    _handsSystem.PickupOrDrop(user, tape);
                }
            }
        };

        ev.Verbs.Add(removeTapeVerb);
    }

    private void OnRepeatToggled(EntityUid uid, WhiteJukeboxComponent component, JukeboxRepeatToggled args)
    {
        component.Playing = args.NewState;
        if (component.PlayingSongData is { } song)
        {
            var elapsed = Math.Max(0, (_timing.CurTime - song.StartedAt).TotalSeconds);
            song.EndsAt = args.NewState ? null : song.StartedAt + TimeSpan.FromSeconds(
                (Math.Floor(elapsed / song.ActualSongLengthSeconds) + 1) * song.ActualSongLengthSeconds);
        }
        Dirty(uid, component);
    }

    private void OnRequestStop(EntityUid uid, WhiteJukeboxComponent component, JukeboxStopRequest args)
    {
        component.PlayingSongData = null;
        Dirty(uid, component);
    }

    private void OnInteract(EntityUid uid, WhiteJukeboxComponent component, InteractUsingEvent args)
    {
        if (component.PlayingSongData != null) return;

        if (!HasComp<TapeComponent>(args.Used))
            return;

        var containedEntities = component.TapeContainer.ContainedEntities;

        if (containedEntities.Count >= 1)
        {
            var removedTapes = _containerSystem.EmptyContainer(component.TapeContainer, true).ToList();
            _containerSystem.Insert(args.Used, component.TapeContainer);

            foreach (var tapeUid in removedTapes)
            {
                _handsSystem.PickupOrDrop(args.User, tapeUid);
            }
        }
        else
        {
            _containerSystem.Insert(args.Used, component.TapeContainer);
        }
    }
    internal void OnSongRequestPlay(JukeboxRequestSongPlay msg, EntitySessionEventArgs args)
    {
        if (msg.Jukebox is not { } netEntity || !TryGetEntity(netEntity, out var entity) ||
            !TryComp<WhiteJukeboxComponent>(entity, out var jukebox) ||
            args.SenderSession.AttachedEntity is not { } actor ||
            !_ui.IsUiOpen(entity.Value, JukeboxUIKey.Key, actor) || msg.SongPath is not { } path)
            return;

        JukeboxSong? selected = null;
        foreach (var tape in jukebox.TapeContainer.ContainedEntities.Concat(jukebox.DefaultSongsContainer.ContainedEntities))
        {
            if (!TryComp<TapeComponent>(tape, out var component))
                continue;
            selected = component.Songs.FirstOrDefault(song => song.SongPath == path);
            if (selected != null)
                break;
        }
        if (selected == null)
            return;

        float duration;
        try
        {
            duration = (float) _audio.GetAudioLength(new ResolvedPathSpecifier(path)).TotalSeconds;
        }
        catch (Exception e)
        {
            Log.Warning($"Could not read jukebox song {path}: {e.Message}");
            return;
        }
        if (!float.IsFinite(duration) || duration <= 0f)
            return;
        jukebox.Playing = true;
        jukebox.PlayingSongData = new PlayingSongData
        {
            SongName = selected.SongName,
            SongPath = path,
            ActualSongLengthSeconds = duration,
            StartedAt = _timing.CurTime,
        };
        if (!_playingJukeboxes.Any(playing => playing.Owner == entity))
            _playingJukeboxes.Add((entity.Value, jukebox));
        Dirty(entity.Value, jukebox);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _updateTimer += frameTime;
        if (_updateTimer < UpdateTimerDefaultTime)
            return;
        _updateTimer = 0f;
        for (var i = _playingJukeboxes.Count - 1; i >= 0; i--)
        {
            var entity = _playingJukeboxes[i];
            if (TerminatingOrDeleted(entity) || entity.Comp.PlayingSongData is not { } song)
            {
                _playingJukeboxes.RemoveAt(i);
                continue;
            }
            if (song.EndsAt is { } end && _timing.CurTime >= end)
            {
                entity.Comp.PlayingSongData = null;
                _playingJukeboxes.RemoveAt(i);
                Dirty(entity);
            }
        }
    }
}
