// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DamageState;
using Content.Client.Gameplay;
using Content.Client.Humanoid;
using Content.Client.Sirena.Animations;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Tag;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.DeadSpace.Psychiatry;
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
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Systems.DamageOverlays;
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
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly EmoteAnimationSystem _emotes = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IStateManager _state = default!;

    private static readonly SoundPathSpecifier SfxFallback = new("/Audio/Magic/fireball.ogg");

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
    private readonly Dictionary<EntityUid, int> _leaveSalt = new();
    private float _cowLookAccum;
    private readonly List<string> _itemHoverEmotes = new();
    private static readonly ProtoId<EmotePrototype> Polskorovit = "Polskorovit";
    private static readonly ProtoId<TagPrototype> CowTag = "Cow";
    private static readonly ResPath CowRsi = new("Mobs/Animals/cow.rsi");
    private const float CowLookRange = 4f;
    private const float CowLookGap = 0.4f;
    private const float ItemHoverEmoteChance = 0.1f;
    private float _sweepAccum;
    private float _shownPhantom;
    private bool _skipGarble;
    private TimeSpan _nextNearbyVoice;
    private readonly List<(string Path, string State)> _hallucinationKeys = new();
    private static readonly ProtoId<SoundCollectionPrototype> NearbyVoices = "PsychiatryNearby";
    private static readonly SoundSpecifier NearbyVoiceSound = new SoundCollectionSpecifier(NearbyVoices.Id);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(HumanoidAppearanceSystem));
        SubscribeNetworkEvent<PsychiatryWhisperEvent>(OnWhisper);
        SubscribeLocalEvent<PsychiatryRemapComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerDetachedEvent>(OnLeftBody);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalDetached);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobState);
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

        _sweepAccum += frameTime;
        var sweep = _sweepAccum >= 0.5f;
        if (sweep)
            _sweepAccum = 0f;

        if (!TryGetSubject(out var subject, out var schizo))
        {
            if (_shownPhantom != 0f && _player.LocalEntity is { } local)
            {
                _shownPhantom = 0f;
                _ui.GetUIController<DamageOverlayUiController>().RefreshLocal(local);
            }

            if (_activeSubject != null || sweep)
            {
                ClearVisuals();
                RemoveOverlays();
                _activeSubject = null;
                _lastParacusia = TimeSpan.Zero;
                if (sweep)
                    SweepStuckSprites();
            }
            return;
        }

        _activeSubject = subject;
        EnsureOverlays(schizo);
        NoteParacusia(subject);
        if (_scareOverlay != null)
        {
            _scareOverlay.ScareMinSec = _cfg.GetCVar(CCCCVars.PsychiatryScareMinSec);
            _scareOverlay.ScareMaxSec = _cfg.GetCVar(CCCCVars.PsychiatryScareMaxSec);
            _scareOverlay.Configure(subject, schizo);
            _scareOverlay.Tick(frameTime);
        }

        RefreshActiveRemaps();
        NoteCowLook(subject, schizo, frameTime);

        _discoverAccum += frameTime;
        if (_discoverAccum >= 0.2f)
        {
            _discoverAccum = 0f;
            RemapNearby(subject, schizo);
        }

        if (sweep)
            SweepStuckSprites();

        if (schizo.PhantomAmount != _shownPhantom)
        {
            _shownPhantom = schizo.PhantomAmount;
            _ui.GetUIController<DamageOverlayUiController>().RefreshLocal(subject);
        }

        if (schizo.Stage >= SchizophreniaStage.Acute && _timing.CurTime >= _nextNearbyVoice)
        {
            var min = _cfg.GetCVar(CCCCVars.PsychiatryParacusiaAcuteMinSec);
            var max = MathF.Max(min, _cfg.GetCVar(CCCCVars.PsychiatryParacusiaAcuteMaxSec));
            _nextNearbyVoice = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(min, max));
            var dist = _cfg.GetCVar(CCCCVars.PsychiatryParacusiaDistance);
            var offset = new Vector2(_random.NextFloat(-dist, dist), _random.NextFloat(-dist, dist));
            var coords = Transform(subject).Coordinates.Offset(offset);
            _audio.PlayStatic(NearbyVoiceSound, subject, coords, AudioParams.Default);
        }
    }

    private void NoteCowLook(EntityUid subject, SchizophreniaComponent schizo, float frameTime)
    {
        _cowLookAccum += frameTime;
        if (_cowLookAccum < CowLookGap)
            return;
        _cowLookAccum = 0f;

        if (!TryHovered(out var hovered) || hovered == subject)
            return;

        var here = _xform.GetMapCoordinates(subject);
        var there = _xform.GetMapCoordinates(hovered);
        if (there.MapId != here.MapId || (there.Position - here.Position).Length() > CowLookRange)
            return;

        if (LooksLikeCow(hovered))
        {
            if (schizo.Stage >= SchizophreniaStage.Latent)
                _emotes.PlayLocal(hovered, Polskorovit);
            return;
        }

        if (schizo.Stage < SchizophreniaStage.Acute || !HasComp<ItemComponent>(hovered))
            return;
        if (!_random.Prob(ItemHoverEmoteChance) || !TryPickItemHoverEmote(out var emote))
            return;

        _emotes.PlayLocal(hovered, emote);
    }

    private bool TryHovered(out EntityUid uid)
    {
        uid = default;
        if (_state.CurrentState is not GameplayStateBase screen || !_input.MouseScreenPosition.IsValid)
            return false;

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (screen.GetClickedEntity(mouse) is not { Valid: true } hovered)
            return false;

        uid = hovered;
        return true;
    }

    private bool TryPickItemHoverEmote(out string emote)
    {
        emote = string.Empty;
        if (_itemHoverEmotes.Count == 0)
            CollectItemHoverEmotes(_proto, _itemHoverEmotes);
        if (_itemHoverEmotes.Count == 0)
            return false;

        emote = _random.Pick(_itemHoverEmotes);
        return true;
    }

    internal static void CollectItemHoverEmotes(IPrototypeManager proto, List<string> into)
    {
        into.Clear();
        foreach (var emote in proto.EnumeratePrototypes<EmotePrototype>())
        {
            if (emote.ID == Polskorovit || emote.Steps is not { Count: > 0 })
                continue;
            into.Add(emote.ID);
        }
    }

    private bool LooksLikeCow(EntityUid uid)
    {
        if (_tags.HasTag(uid, CowTag))
            return true;

        if (TryComp<PsychiatryRemapComponent>(uid, out var remap)
            && remap.DrawState == "cow"
            && remap.DrawRsi == CowRsi)
            return true;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return false;

        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            if (layer.Visible && layer.RsiState == "cow" && layer.Rsi?.Path == CowRsi)
                return true;
        }

        return false;
    }

    private void EnsureOverlays(SchizophreniaComponent schizo)
    {
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
            Restore(uid, leftArea: true);
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
            var mobChance = HasComp<HumanoidAppearanceComponent>(uid)
                ? _cfg.GetCVar(CCCCVars.PsychiatryRemapMobHumanoid)
                : _cfg.GetCVar(CCCCVars.PsychiatryRemapMobAcute);

            if (!PsychiatryPattern.ShouldRemapMob(
                    uid.GetHashCode(),
                    schizo.Seed,
                    schizo.Stage,
                    0f,
                    0f,
                    mobChance))
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
                salt: 11 + LeaveSalt(uid),
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
            var pick = PickRemap(PsychiatryRemapPool.Item, uid.GetHashCode(), schizo.Seed, salt: 29 + LeaveSalt(uid), prefer: false);
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

    internal void ApplyRemap(EntityUid uid, SpriteComponent sprite, ResPath rsi, string state, string fakeName, string? kind)
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
            remap.LayerVisibility ??= new List<bool>();
            var seen = 0;
            foreach (ISpriteLayer layer in sprite.AllLayers)
            {
                if (!hasFake || seen != fake)
                {
                    remap.OriginalLayerVisible[LayerKey(layer, seen)] = layer.Visible;
                    remap.LayerVisibility.Add(layer.Visible);
                }

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

    internal void Restore(EntityUid uid, bool leftArea = false)
    {
        if (!TryComp(uid, out PsychiatryRemapComponent? remap))
            return;

        if (leftArea)
            NoteLeft(uid);

        if (!TryComp(uid, out SpriteComponent? sprite))
        {
            RemCompDeferred<PsychiatryRemapComponent>(uid);
            return;
        }

        StripRemapLayers(uid, sprite, remap);
        if (HasExtraRemapLayer(uid, sprite, remap))
            return;

        ApplySavedVisibility(uid, sprite, remap);
        RemCompDeferred<PsychiatryRemapComponent>(uid);
    }

    private void ApplySavedVisibility(EntityUid uid, SpriteComponent sprite, PsychiatryRemapComponent remap)
    {
        var ent = (uid, sprite);
        var savedLayers = remap.LayerVisibility;
        var i = 0;
        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            bool visible;
            if (remap.IsWall)
                visible = true;
            else if (savedLayers != null && i < savedLayers.Count)
                visible = savedLayers[i];
            else
                visible = !remap.OriginalLayerVisible.TryGetValue(LayerKey(layer, i), out var saved) || saved;
            _sprites.LayerSetVisible(ent, i, visible);
            i++;
        }
    }

    private void StripRemapLayers(EntityUid uid, SpriteComponent sprite, PsychiatryRemapComponent remap)
    {
        var ent = (uid, sprite);
        _sprites.RemoveLayer(ent, RemapLayer.Fake, false);

        var remove = new List<int>();
        CollectLooseExtras(ent, remap, remove);
        for (var n = 0; n < remove.Count; n++)
            _sprites.RemoveLayer(ent, remove[n], false);

        if (!HasExtraRemapLayer(uid, sprite, remap))
            return;

        RemoveUnmappedReplacement(ent, sprite, remap);
    }

    private void CollectLooseExtras(Entity<SpriteComponent> ent, PsychiatryRemapComponent remap, List<int> remove)
    {
        var keep = SnapshotMatches(remap, remap.DrawRsi, remap.DrawState);
        var seen = 0;
        var body = 0;
        var loose = new List<int>();
        foreach (ISpriteLayer layer in ent.Comp.AllLayers)
        {
            if (LayerMatches(layer, remap.DrawRsi, remap.DrawState))
            {
                if (IsMappedBody(ent.Owner, ent.Comp, seen))
                    body++;
                else
                    loose.Add(seen);
            }

            seen++;
        }

        var extras = loose.Count - Math.Max(0, keep - body);
        for (var n = loose.Count - 1; n >= 0 && extras > 0; n--)
        {
            remove.Add(loose[n]);
            extras--;
        }
    }

    private static int SnapshotMatches(PsychiatryRemapComponent remap, ResPath rsi, string state)
    {
        var n = 0;
        var wanted = rsi.ToString();
        foreach (var key in remap.OriginalLayerVisible.Keys)
        {
            if (SpriteKeyMatches(key, wanted, state))
                n++;
        }

        return n;
    }

    internal static bool SpriteKeyMatches(string key, string wantedPath, string state)
    {
        var split = key.Split(':', 3);
        if (split.Length < 3)
            return false;

        return split[2] == state && PathsMatch(split[1], wantedPath);
    }

    private bool HasExtraRemapLayer(EntityUid uid, SpriteComponent sprite, PsychiatryRemapComponent remap)
    {
        if (_sprites.LayerMapTryGet((uid, sprite), RemapLayer.Fake, out _, false))
            return true;

        var remove = new List<int>();
        CollectLooseExtras((uid, sprite), remap, remove);
        return remove.Count > 0;
    }

    private static bool LayerMatches(ISpriteLayer layer, ResPath rsi, string state)
    {
        var path = layer.Rsi?.Path.ToString() ?? string.Empty;
        return layer.RsiState == state && PathsMatch(path, rsi.ToString());
    }

    private static bool PathsMatch(string path, string wanted)
    {
        if (path.Length == 0 || wanted.Length == 0)
            return false;

        return path.Equals(wanted, StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(wanted, StringComparison.OrdinalIgnoreCase)
               || wanted.EndsWith(path, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsRealAnimal(EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid) && !HasComp<HumanoidAppearanceComponent>(uid);
    }

    private bool IsMappedBody(EntityUid uid, SpriteComponent sprite, int index)
    {
        return IndexMapped(uid, sprite, index, "movement")
               || IndexMapped(uid, sprite, index, DamageStateVisualLayers.Base)
               || IndexMapped(uid, sprite, index, DamageStateVisualLayers.BaseUnshaded);
    }

    private bool IndexMapped(EntityUid uid, SpriteComponent sprite, int index, string key)
    {
        return _sprites.LayerMapTryGet((uid, sprite), key, out var mapped, false) && mapped == index;
    }

    private bool IndexMapped(EntityUid uid, SpriteComponent sprite, int index, Enum key)
    {
        return _sprites.LayerMapTryGet((uid, sprite), key, out var mapped, false) && mapped == index;
    }

    internal void SweepStuckSprites()
    {
        if (_hallucinationKeys.Count == 0)
        {
            foreach (var proto in _proto.EnumeratePrototypes<PsychiatryRemapPrototype>())
            {
                if (proto.Sprite is not SpriteSpecifier.Rsi rsi)
                    continue;
                _hallucinationKeys.Add((rsi.RsiPath.ToString(), rsi.RsiState));
            }
        }

        var sprites = new List<EntityUid>();
        var query = EntityQueryEnumerator<SpriteComponent>();
        while (query.MoveNext(out var uid, out _))
            sprites.Add(uid);

        foreach (var uid in sprites)
            StripOrphanLayers(uid);
    }

    private void StripOrphanLayers(EntityUid uid)
    {
        if (HasComp<PsychiatryRemapComponent>(uid) || !TryComp(uid, out SpriteComponent? sprite))
            return;

        var ent = (uid, sprite);
        _sprites.RemoveLayer(ent, RemapLayer.Fake, false);
        if (IsRealAnimal(uid))
            return;

        var remove = new List<int>();
        var otherPath = false;
        var index = 0;
        foreach (ISpriteLayer layer in sprite.AllLayers)
        {
            if (IsHallucinationLayer(layer))
            {
                if (!IsMappedBody(uid, sprite, index))
                    remove.Add(index);
            }
            else if (layer.Rsi != null)
                otherPath = true;
            index++;
        }

        if (remove.Count > 0 && otherPath)
        {
            for (var n = remove.Count - 1; n >= 0; n--)
                _sprites.RemoveLayer(ent, remove[n], false);
        }

    }

    private bool IsHallucinationLayer(ISpriteLayer layer)
    {
        var path = layer.Rsi?.Path.ToString() ?? string.Empty;
        if (path.Length == 0)
            return false;

        foreach (var key in _hallucinationKeys)
        {
            if (layer.RsiState != key.State)
                continue;
            if (PathsMatch(path, key.Path))
                return true;
        }

        return false;
    }

    public void GarbleHeard(ChatMessage msg)
    {
        if (_skipGarble)
        {
            _skipGarble = false;
            return;
        }

        if (msg.Channel is not (ChatChannel.Local or ChatChannel.Whisper or ChatChannel.Radio))
            return;
        if (!TryGetSubject(out var self, out var schizo) || schizo.Stage < SchizophreniaStage.Simple)
            return;
        if (msg.SenderEntity == GetNetEntity(self))
            return;

        const string open = "[BubbleContent]";
        const string close = "[/BubbleContent]";
        var wrapped = msg.WrappedMessage;
        var start = wrapped.IndexOf(open, StringComparison.Ordinal);
        var end = start < 0 ? -1 : wrapped.IndexOf(close, start + open.Length, StringComparison.Ordinal);
        var spoken = start >= 0 && end > start
            ? wrapped.Substring(start + open.Length, end - start - open.Length)
            : msg.Message;
        if (string.IsNullOrWhiteSpace(spoken))
            return;

        var garbled = FitSyllables(spoken);
        msg.Message = garbled;
        var escaped = FormattedMessage.EscapeText(garbled);
        if (start >= 0 && end > start)
            msg.WrappedMessage = wrapped[..start] + open + escaped + wrapped[end..];
        else if (wrapped.Contains(spoken, StringComparison.Ordinal))
            msg.WrappedMessage = wrapped.Replace(spoken, escaped);
    }

    private string FitSyllables(string text)
    {
        var parts = text.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            var letters = 0;
            foreach (var ch in parts[i])
            {
                if (char.IsLetter(ch))
                    letters++;
            }

            if (letters == 0)
                continue;

            parts[i] = Nonsense(Math.Max(2, letters));
        }

        return string.Join(' ', parts);
    }

    private static readonly ProtoId<PsychiatryPhrasesPrototype> DefaultPhrases = "PsychiatryDefault";

    private string Nonsense(int length)
    {
        if (!_proto.TryIndex(DefaultPhrases, out PsychiatryPhrasesPrototype? phrases) || phrases.Syllables.Count == 0)
            return "";

        var built = "";
        while (built.Length < length)
            built += _random.Pick(phrases.Syllables);

        return built.Length == length ? built : built.Substring(0, length);
    }

    public bool HidesFromMenu(EntityUid uid)
    {
        return HasComp<PsychiatryRemapComponent>(uid)
               && TryGetSubject(out _, out var schizo)
               && schizo.Stage >= SchizophreniaStage.Acute;
    }

    private void OnMobState(MobStateChangedEvent args)
    {
        if (_player.LocalEntity != args.Target || args.NewMobState != MobState.Dead)
            return;

        ClearVisuals();
        SweepStuckSprites();
        RemoveOverlays();
        _activeSubject = null;
        _lastParacusia = TimeSpan.Zero;
    }

    private int LeaveSalt(EntityUid uid)
    {
        return _leaveSalt.TryGetValue(uid, out var salt) ? salt : 0;
    }

    private void NoteLeft(EntityUid uid)
    {
        _leaveSalt.TryGetValue(uid, out var salt);
        _leaveSalt[uid] = salt + 1;
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

    internal void ClearVisuals()
    {
        var toClear = new List<EntityUid>();
        var q = EntityQueryEnumerator<PsychiatryRemapComponent>();
        while (q.MoveNext(out var uid, out _))
            toClear.Add(uid);
        foreach (var uid in toClear)
            Restore(uid);

        toClear.Clear();
        q = EntityQueryEnumerator<PsychiatryRemapComponent>();
        while (q.MoveNext(out var uid, out _))
            toClear.Add(uid);
        foreach (var uid in toClear)
            ForceClearRemap(uid);

        SweepStuckSprites();
    }

    private void ForceClearRemap(EntityUid uid)
    {
        if (!TryComp(uid, out PsychiatryRemapComponent? remap))
            return;

        if (TryComp(uid, out SpriteComponent? sprite))
        {
            var ent = (uid, sprite);
            _sprites.RemoveLayer(ent, RemapLayer.Fake, false);
            var remove = new List<int>();
            var otherPath = false;
            var index = 0;
            foreach (ISpriteLayer layer in sprite.AllLayers)
            {
                if (LayerMatches(layer, remap.DrawRsi, remap.DrawState))
                {
                    if (!IsMappedBody(uid, sprite!, index))
                        remove.Add(index);
                }
                else if (layer.Rsi != null)
                    otherPath = true;
                index++;
            }

            if (!IsRealAnimal(uid) && (otherPath || HasComp<HumanoidAppearanceComponent>(uid)))
            {
                for (var n = remove.Count - 1; n >= 0; n--)
                    _sprites.RemoveLayer(ent, remove[n], false);
            }

            ApplySavedVisibility(uid, sprite, remap);
        }

        RemCompDeferred<PsychiatryRemapComponent>(uid);
    }

    private void OnGetStatusIcons(Entity<PsychiatryRemapComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!TryGetSubject(out _, out _))
            return;
        args.StatusIcons.Clear();
    }

    private void OnWhisper(PsychiatryWhisperEvent ev)
    {
        if (!TryGetSubject(out _, out var schizo) || schizo.Stage < SchizophreniaStage.Latent)
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

        _skipGarble = true;
        var msg = new ChatMessage(
            channel,
            ev.Message,
            wrapped,
            GetNetEntity(source),
            null);

        _ui.GetUIController<ChatUIController>().ProcessChatMessage(msg, speechBubble: false);
        // fake lines stay readable; GarbleHeard skips messages this system just posted
        _skipGarble = false;

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

        return Loc.GetString("chat-radio-message-wrap",
            ("channel-color", channelColor),
            ("fontType", "Default"),
            ("fontSize", 12),
            ("verb", Loc.GetString("psychiatry-radio-verb")),
            ("channel", $"\\[{Loc.GetString("chat-radio-common")}\\]"),
            ("headset-name", FormattedMessage.EscapeText(ev.SpeakerName)),
            ("message", FormattedMessage.EscapeText(ev.Message)),
            ("headset-color", headset),
            ("job", job));
    }
}
