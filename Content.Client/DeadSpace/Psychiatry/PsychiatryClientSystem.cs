// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Examine;
using Content.Client.Humanoid;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
using Content.Shared.Examine;
using Content.Shared.Follower.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Wall;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Psychiatry;

public sealed class PsychiatryClientSystem : SharedPsychiatrySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    private static readonly SoundPathSpecifier SfxFallback = new("/Audio/Magic/fireball.ogg");

    private PsychiatryFloorOverlay? _floorOverlay;
    private PsychiatryWallOverlay? _wallOverlay;
    private PsychiatryScareOverlay? _scareOverlay;

    private enum RemapLayer
    {
        Fake,
    }
    private EntityUid? _activeSubject;
    private float _discoverAccum;
    private bool _fxEnabled = true;
    private bool _typingSent;
    private TimeSpan _nextTypingSend;
    private string _chatSnapshot = "";
    private bool _chatReady;
    private TimeSpan _lastEdit;
    private TimeSpan _lastParacusia;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(HumanoidAppearanceSystem));
        SubscribeNetworkEvent<PsychiatryWhisperEvent>(OnWhisper);
        SubscribeLocalEvent<PsychiatryRemapComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PsychiatryRemapComponent, ClientExaminedEvent>(OnClientExamined);
        SubscribeLocalEvent<PsychiatryRemapComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerDetachedEvent>(OnLeftBody);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalDetached);
        Subs.CVar(_cfg, CCCCVars.PsychiatryClientFx, v =>
        {
            _fxEnabled = v;
            if (!v)
            {
                ClearVisuals();
                RemoveOverlays();
            }
        }, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_timing.IsFirstTimePredicted)
            return;

        var text = "";
        foreach (var chat in _ui.GetUIController<ChatUIController>().Chats)
            text += chat.ChatInput.Input.Text;

        if (!_chatReady)
        {
            _chatSnapshot = text;
            _chatReady = true;
        }
        else if (!string.Equals(text, _chatSnapshot, StringComparison.Ordinal))
        {
            _chatSnapshot = text;
            _lastEdit = _timing.CurTime;
        }

        var typing = _lastEdit != TimeSpan.Zero && _timing.CurTime - _lastEdit < TimeSpan.FromSeconds(0.5);

        if (typing == _typingSent && (!typing || _timing.CurTime < _nextTypingSend))
            return;

        _typingSent = typing;
        _nextTypingSend = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        RaiseNetworkEvent(new PsychiatryTypingRequestEvent(typing));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ClearVisuals();
        RemoveOverlays();
    }

    public bool TryGetSubject(out EntityUid subject, out SchizophreniaComponent schizo)
    {
        subject = default!;
        schizo = null!;

        if (!_cfg.GetCVar(CCCCVars.PsychiatryEnabled) || !_fxEnabled)
            return false;

        var local = _player.LocalEntity;
        if (local == null)
            return false;

        if (HasComp<GhostComponent>(local.Value) || HasComp<VisitingMindComponent>(local.Value))
            return false;

        if (TryComp<MobStateComponent>(local.Value, out var mob) && mob.CurrentState == MobState.Dead)
            return false;

        if (TryComp(local.Value, out SchizophreniaComponent? self) && IsIll(self.Stage))
        {
            subject = local.Value;
            schizo = self;
            return true;
        }

        if (TryComp(local.Value, out FollowerComponent? follower) &&
            TryComp(follower.Following, out SchizophreniaComponent? followed) &&
            IsIll(followed.Stage))
        {
            subject = follower.Following;
            schizo = followed;
            return true;
        }

        return false;
    }

    private void OnLocalDetached(LocalPlayerDetachedEvent args)
    {
        ClearVisuals();
        RemoveOverlays();
        _activeSubject = null;
        _lastParacusia = TimeSpan.Zero;
    }

    private void OnLeftBody(EntityUid uid, SchizophreniaComponent comp, LocalPlayerDetachedEvent args)
    {
        ClearVisuals();
        RemoveOverlays();
        _activeSubject = null;
        _lastParacusia = TimeSpan.Zero;
    }

    private static bool IsIll(SchizophreniaStage stage)
    {
        return stage is >= SchizophreniaStage.Latent and <= SchizophreniaStage.Acute;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!TryGetSubject(out var subject, out var schizo))
        {
            if (_activeSubject != null)
            {
                ClearVisuals();
                RemoveOverlays();
                _activeSubject = null;
                _lastParacusia = TimeSpan.Zero;
            }
            return;
        }

        _activeSubject = subject;
        EnsureOverlays(schizo);
        _floorOverlay?.UpdateClusters(subject, schizo, _cfg.GetCVar(CCCCVars.PsychiatryRemapRadius));
        _wallOverlay?.Update(subject, schizo, frameTime, _cfg.GetCVar(CCCCVars.PsychiatryRemapRadius));
        NoteParacusia(subject);
        if (_scareOverlay != null)
        {
            _scareOverlay.ScareMinSec = _cfg.GetCVar(CCCCVars.PsychiatryScareMinSec);
            _scareOverlay.ScareMaxSec = _cfg.GetCVar(CCCCVars.PsychiatryScareMaxSec);
            _scareOverlay.Configure(subject, schizo);
            _scareOverlay.Tick(frameTime);
        }

        RefreshActiveRemaps();

        _discoverAccum += frameTime;
        if (_discoverAccum >= 0.2f)
        {
            _discoverAccum = 0f;
            RemapNearby(subject, schizo);
        }
    }

    private void EnsureOverlays(SchizophreniaComponent schizo)
    {
        if (_floorOverlay == null)
        {
            _floorOverlay = new PsychiatryFloorOverlay(EntityManager, _map);
            _overlays.AddOverlay(_floorOverlay);
        }

        if (schizo.Stage >= SchizophreniaStage.Simple && _wallOverlay == null)
        {
            _wallOverlay = new PsychiatryWallOverlay(EntityManager, _map, _xform, _lookup);
            _overlays.AddOverlay(_wallOverlay);
        }
        else if (schizo.Stage < SchizophreniaStage.Simple && _wallOverlay != null)
        {
            _wallOverlay.Clear();
            _overlays.RemoveOverlay(_wallOverlay);
            _wallOverlay = null;
        }

        if (schizo.Stage >= SchizophreniaStage.Acute && _scareOverlay == null)
        {
            _scareOverlay = new PsychiatryScareOverlay(EntityManager, _xform, _timing, _random, _proto);
            _scareOverlay.PlaySound = PlayScareSound;
            _overlays.AddOverlay(_scareOverlay);
        }
        else if (schizo.Stage < SchizophreniaStage.Acute && _scareOverlay != null)
        {
            _scareOverlay.PlaySound = null;
            _scareOverlay.Clear();
            _overlays.RemoveOverlay(_scareOverlay);
            _scareOverlay = null;
        }
    }

    private void PlayScareSound(SoundSpecifier? sound)
    {
        if (!TryPlayLocal(sound) && !TryPlayLocal(SfxFallback))
            return;

        RaiseNetworkEvent(new PsychiatryUnrealSoundEvent(0.9f, 0.9f));
    }

    private bool TryPlayLocal(SoundSpecifier? sound)
    {
        if (sound == null)
            return false;

        return _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-3f)) != null;
    }

    private void NoteParacusia(EntityUid subject)
    {
        if (!TryComp<ParacusiaComponent>(subject, out var paracusia))
        {
            _lastParacusia = TimeSpan.Zero;
            return;
        }

        if (_lastParacusia != TimeSpan.Zero && paracusia.NextIncidentTime > _lastParacusia)
            RaiseNetworkEvent(new PsychiatryUnrealSoundEvent(0.9f, 0.85f));

        _lastParacusia = paracusia.NextIncidentTime;
    }

    private void RemoveOverlays()
    {
        if (_floorOverlay != null)
        {
            _floorOverlay.Clear();
            _overlays.RemoveOverlay(_floorOverlay);
            _floorOverlay = null;
        }

        if (_wallOverlay != null)
        {
            _wallOverlay.Clear();
            _overlays.RemoveOverlay(_wallOverlay);
            _wallOverlay = null;
        }

        if (_scareOverlay != null)
        {
            _scareOverlay.PlaySound = null;
            _scareOverlay.Clear();
            _overlays.RemoveOverlay(_scareOverlay);
            _scareOverlay = null;
        }
    }

    private void RemapNearby(EntityUid subject, SchizophreniaComponent schizo)
    {
        var keep = new HashSet<EntityUid>();
        var origin = _xform.GetMapCoordinates(subject);

        foreach (var uid in _lookup.GetEntitiesInRange(subject, _cfg.GetCVar(CCCCVars.PsychiatryRemapRadius)))
        {
            if (uid == subject || !TryComp(uid, out SpriteComponent? sprite) || !TryComp(uid, out MetaDataComponent? meta))
                continue;
            if (!TryComp(uid, out TransformComponent? xform) || xform.MapID != origin.MapId)
                continue;

            if (!TryPickRemap(uid, meta, schizo, out var rsi, out var state, out var fakeName, out var kind))
                continue;

            keep.Add(uid);
            ApplyRemap(uid, sprite, rsi, state, fakeName, kind);
        }

        var stale = new List<EntityUid>();
        var remapQuery = EntityQueryEnumerator<PsychiatryRemapComponent>();
        while (remapQuery.MoveNext(out var uid, out _))
        {
            if (!keep.Contains(uid))
                stale.Add(uid);
        }

        foreach (var uid in stale)
            Restore(uid);
    }

    private void RefreshActiveRemaps()
    {
        var release = new List<EntityUid>();
        var q = EntityQueryEnumerator<PsychiatryRemapComponent, SpriteComponent>();
        while (q.MoveNext(out var uid, out var remap, out var sprite))
        {
            if (remap.IsWall)
            {
                release.Add(uid);
                continue;
            }

            SyncRemapLayer(uid, sprite, remap);
        }

        foreach (var uid in release)
            Restore(uid);
    }

    private bool TryPickRemap(EntityUid uid, MetaDataComponent meta, SchizophreniaComponent schizo,
        out ResPath rsi, out string state, out string fakeName, out string? kind)
    {
        rsi = default;
        state = string.Empty;
        fakeName = meta.EntityName;
        kind = null;

        if (HasComp<WallComponent>(uid))
            return false;

        if (HasComp<HumanoidAppearanceComponent>(uid) || HasComp<MobStateComponent>(uid))
        {
            if (!PsychiatryPattern.ShouldRemapMob(
                    uid.GetHashCode(),
                    schizo.Seed,
                    schizo.Stage,
                    _cfg.GetCVar(CCCCVars.PsychiatryRemapMobLatent),
                    _cfg.GetCVar(CCCCVars.PsychiatryRemapMobSimple),
                    _cfg.GetCVar(CCCCVars.PsychiatryRemapMobAcute)))
                return false;

            var pool = PsychiatryPattern.PickPool(
                schizo.Stage,
                uid.GetHashCode(),
                schizo.Seed,
                _cfg.GetCVar(CCCCVars.PsychiatryRemapMonsterChance));
            var chefLike = meta.EntityName.Contains("Chef", StringComparison.OrdinalIgnoreCase)
                           || meta.EntityName.Contains("Cook", StringComparison.OrdinalIgnoreCase)
                           || meta.EntityName.Contains("Повар", StringComparison.OrdinalIgnoreCase);
            var pick = PickRemap(
                pool,
                uid.GetHashCode(),
                schizo.Seed,
                salt: 11,
                prefer: chefLike || PsychiatryPattern.PreferCow(uid.GetHashCode(), schizo.Seed));
            return TryVisual(pick, out rsi, out state, out fakeName, out kind);
        }

        if (HasComp<ItemComponent>(uid) &&
            Transform(uid).GridUid != null &&
            !_containers.IsEntityInContainer(uid) &&
            PsychiatryPattern.ShouldRemapItem(
                uid.GetHashCode(),
                schizo.Seed,
                schizo.Stage,
                _cfg.GetCVar(CCCCVars.PsychiatryRemapItemLatent),
                _cfg.GetCVar(CCCCVars.PsychiatryRemapItemSimple),
                _cfg.GetCVar(CCCCVars.PsychiatryRemapItemAcute)))
        {
            var pick = PickRemap(PsychiatryRemapPool.Item, uid.GetHashCode(), schizo.Seed, salt: 29, prefer: false);
            return TryVisual(pick, out rsi, out state, out fakeName, out kind);
        }

        return false;
    }

    private PsychiatryRemapPrototype? PickRemap(PsychiatryRemapPool pool, int entityHash, int seed, int salt, bool prefer)
    {
        var matched = new List<PsychiatryRemapPrototype>();
        foreach (var proto in _proto.EnumeratePrototypes<PsychiatryRemapPrototype>())
        {
            if (proto.Wall || proto.Weight <= 0 || proto.Sprite is not SpriteSpecifier.Rsi)
                continue;
            if (!proto.Pools.Contains(pool))
                continue;
            matched.Add(proto);
        }

        if (prefer)
        {
            var prefs = matched.FindAll(p => p.Prefer);
            if (prefs.Count > 0)
                matched = prefs;
        }

        if (matched.Count == 0)
            return null;

        matched.Sort((a, b) => string.CompareOrdinal(a.ID, b.ID));
        var sum = 0;
        foreach (var proto in matched)
            sum += proto.Weight;

        var cursor = (HashCode.Combine(entityHash, seed, salt) & int.MaxValue) % sum;
        foreach (var proto in matched)
        {
            cursor -= proto.Weight;
            if (cursor < 0)
                return proto;
        }

        return matched[^1];
    }

    private bool TryVisual(PsychiatryRemapPrototype? pick, out ResPath rsi, out string state, out string fakeName, out string? kind)
    {
        rsi = default;
        state = string.Empty;
        fakeName = string.Empty;
        kind = null;
        if (pick?.Sprite is not SpriteSpecifier.Rsi sprite)
            return false;

        rsi = sprite.RsiPath;
        state = sprite.RsiState;
        fakeName = Loc.GetString(pick.Name);
        kind = pick.ID;
        return true;
    }

    private void ApplyRemap(EntityUid uid, SpriteComponent sprite, ResPath rsi, string state, string fakeName, string? kind)
    {
        var remap = EnsureComp<PsychiatryRemapComponent>(uid);
        remap.FakeName = fakeName;
        remap.IsWall = false;
        remap.TileIndex = null;
        remap.RemapId = kind;
        remap.DrawRsi = rsi;
        remap.DrawState = state;
        remap.DrawColor = Color.White;
        SyncRemapLayer(uid, sprite, remap);
    }

    private void SyncRemapLayer(EntityUid uid, SpriteComponent sprite, PsychiatryRemapComponent remap)
    {
        if (remap.DrawState.Length == 0)
            return;

        var ent = (uid, sprite);
        var hasFake = _sprites.LayerMapTryGet(ent, RemapLayer.Fake, out var fake, false);
        if (remap.OriginalLayerVisible.Count == 0)
        {
            var seen = 0;
            foreach (ISpriteLayer layer in sprite.AllLayers)
            {
                if (!hasFake || seen != fake)
                    remap.OriginalLayerVisible[LayerKey(layer, seen)] = layer.Visible;
                seen++;
            }
        }

        if (!hasFake)
        {
            fake = _sprites.AddLayer(ent, new SpriteSpecifier.Rsi(remap.DrawRsi, remap.DrawState));
            _sprites.LayerMapSet(ent, RemapLayer.Fake, fake);
        }
        else if (!FakeLayerMatches(sprite, fake, remap))
        {
            _sprites.LayerSetRsi(ent, RemapLayer.Fake, remap.DrawRsi, remap.DrawState);
        }

        // Спрайт со щелчком по сторонам света принимает только один кадр направления.
        // Картинка подмены обычно смотрит в четыре стороны, и отладочная проверка рамки роняет клиент,
        // как только телепорт двигает пешку и рамки пересчитываются.
        if (sprite.SnapCardinals)
        {
            sprite.GranularLayersRendering = true;
            _sprites.LayerSetRenderingStrategy(ent, RemapLayer.Fake, LayerRenderingStrategy.NoRotation);
        }

        _sprites.LayerSetColor(ent, RemapLayer.Fake, remap.DrawColor);
        _sprites.LayerSetVisible(ent, RemapLayer.Fake, true);
        if (!_sprites.LayerMapTryGet(ent, RemapLayer.Fake, out fake, false))
            return;

        var i = 0;
        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            if (i != fake && layer.Visible)
                _sprites.LayerSetVisible(ent, i, false);
            i++;
        }
    }

    private static bool FakeLayerMatches(SpriteComponent sprite, int index, PsychiatryRemapComponent remap)
    {
        var seen = 0;
        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            if (seen == index)
            {
                return layer.RsiState == remap.DrawState
                       && layer.Rsi?.Path.ToString() == remap.DrawRsi.ToString();
            }

            seen++;
        }

        return false;
    }

    private void Restore(EntityUid uid)
    {
        if (!TryComp(uid, out PsychiatryRemapComponent? remap))
            return;

        if (TryComp(uid, out SpriteComponent? sprite))
        {
            var ent = (uid, sprite);
            if (!_sprites.RemoveLayer(ent, RemapLayer.Fake, false))
                RemoveUnmappedReplacement(ent, sprite, remap);

            var i = 0;
            foreach (ISpriteLayer layer in sprite.AllLayers)
            {
                // Wall remaps used to hide real layers; always show them again.
                // Other remaps restore the snapshot, or stay visible if the key is unknown.
                var visible = remap.IsWall
                    || !remap.OriginalLayerVisible.TryGetValue(LayerKey(layer, i), out var saved)
                    || saved;
                _sprites.LayerSetVisible(ent, i, visible);
                i++;
            }
        }

        RemCompDeferred<PsychiatryRemapComponent>(uid);
    }

    private void RemoveUnmappedReplacement(Entity<SpriteComponent> ent, SpriteComponent sprite, PsychiatryRemapComponent remap)
    {
        var last = LayerCount(sprite) - 1;
        if (last < 0)
            return;

        var index = 0;
        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            if (index == last && !remap.OriginalLayerVisible.ContainsKey(LayerKey(layer, last)))
            {
                SpriteComponent? layerSprite = sprite;
                _sprites.RemoveLayer((ent.Owner, layerSprite), last);
            }

            index++;
        }
    }

    private static string LayerKey(ISpriteLayer layer, int index)
    {
        var path = layer.Rsi?.Path.ToString() ?? string.Empty;
        return $"{index}:{path}:{layer.RsiState}";
    }

    private static int LayerCount(SpriteComponent sprite)
    {
        var n = 0;
        foreach (var _ in sprite.AllLayers)
            n++;
        return n;
    }

    private void ClearVisuals()
    {
        var toClear = new List<EntityUid>();
        var q = EntityQueryEnumerator<PsychiatryRemapComponent>();
        while (q.MoveNext(out var uid, out _))
            toClear.Add(uid);
        foreach (var uid in toClear)
            Restore(uid);
    }

    private void OnGetStatusIcons(Entity<PsychiatryRemapComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!TryGetSubject(out _, out _))
            return;
        args.StatusIcons.Clear();
    }

    private void OnExamined(Entity<PsychiatryRemapComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetSubject(out _, out _))
            return;
        args.PushMarkup(Loc.GetString("psychiatry-examine-appears", ("name", ent.Comp.FakeName)));
    }

    private void OnClientExamined(Entity<PsychiatryRemapComponent> ent, ref ClientExaminedEvent ev)
    {
        var fake = ent.Comp.FakeName;
        if (string.IsNullOrEmpty(fake))
            return;

        foreach (var child in _ui.ModalRoot.Children)
        {
            if (child is not Popup popup)
                continue;
            ReplaceExamineTitle(popup, fake);
        }
    }

    private static void ReplaceExamineTitle(Control root, string fakeName)
    {
        foreach (var child in root.Children)
        {
            if (child is RichTextLabel label)
            {
                label.SetMessage(FormattedMessage.FromMarkupPermissive($"[bold]{FormattedMessage.EscapeText(fakeName)}[/bold]"));
                return;
            }

            ReplaceExamineTitle(child, fakeName);
        }
    }

    private void OnWhisper(PsychiatryWhisperEvent ev)
    {
        if (!TryGetSubject(out _, out var schizo) || schizo.Stage < SchizophreniaStage.Simple)
            return;

        var source = EntityUid.Invalid;
        if (ev.Source is { Valid: true } net)
            source = GetEntity(net);

        var channel = ev.AsRadio ? ChatChannel.Radio : ChatChannel.Local;
        var wrapped = ev.AsRadio
            ? RadioWhisper(ev)
            : Loc.GetString(
                "chat-manager-entity-whisper-wrap-message",
                ("entityName", FormattedMessage.EscapeText(ev.SpeakerName)),
                ("message", FormattedMessage.EscapeText(ev.Message)));

        if (!ev.AsRadio && wrapped.StartsWith("chat-manager", StringComparison.Ordinal))
        {
            wrapped = $"[color=#AAAAAA][italic]{FormattedMessage.EscapeText(ev.SpeakerName)}[/italic] whispers, \"{FormattedMessage.EscapeText(ev.Message)}\"[/color]";
        }

        var msg = new ChatMessage(
            channel,
            ev.Message,
            wrapped,
            GetNetEntity(source),
            null);

        _ui.GetUIController<ChatUIController>().ProcessChatMessage(msg, speechBubble: false);

        var brainEv = new PsychiatryBrainActivityEvent(PsychiatryBrainRegion.Hearing, 0.8f);
        RaiseLocalEvent(ref brainEv);
        var voiceEv = new PsychiatryBrainActivityEvent(PsychiatryBrainRegion.Voice, 0.6f);
        RaiseLocalEvent(ref voiceEv);
        var arousalEv = new PsychiatryBrainActivityEvent(PsychiatryBrainRegion.Arousal, 0.5f);
        RaiseLocalEvent(ref arousalEv);
    }

    private string RadioWhisper(PsychiatryWhisperEvent ev)
    {
        const string channelColor = "#32cd32";
        var headset = string.IsNullOrEmpty(ev.JobColor) ? channelColor : ev.JobColor;
        var job = string.IsNullOrEmpty(ev.Job)
            ? ""
            : $"\\[{FormattedMessage.EscapeText(ev.Job)}\\] ";

        return Loc.GetString("chat-radio-message-wrap-lang",
            ("channel-color", channelColor),
            ("fontType", "Default"),
            ("fontSize", 12),
            ("verb", Loc.GetString("psychiatry-radio-verb")),
            ("language", Loc.GetString("psychiatry-radio-language")),
            ("channel", $"\\[{Loc.GetString("chat-radio-common")}\\]"),
            ("name", FormattedMessage.EscapeText(ev.SpeakerName)),
            ("message", FormattedMessage.EscapeText(ev.Message)),
            ("headset-color", headset),
            ("job", job));
    }
}
