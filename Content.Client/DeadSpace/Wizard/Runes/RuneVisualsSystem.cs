// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Content.Shared.Ghost;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneImpact
{
    public MapCoordinates At;
    public string Element = string.Empty;
    public string? Second;
    public float Radius;
    public bool Strong;
    public float Start;
    public int Seed;
}

public sealed class RuneAim
{
    public EntityUid Caster;
    public float Range;
    public RuneTargeting Targeting;
    public Color Color;
    public MapCoordinates Point;
    public readonly List<EntityUid> Targets = new();
    public bool InRange;
    public bool Valid;
    public string Problem = "rune-cast-no-target";
}

public sealed class RuneSigilTrack
{
    public TimeSpan Started;
    public readonly List<float> Appeared = new();
    public EntityUid? Light;
}

public sealed class RuneVisualsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public const float ImpactLength = 0.9f;
    private const string FormLayer = "rune-form";
    private const string ElementLayer = "rune-element";
    private const float TintStrength = 0.6f;
    private const float FlashLength = 0.5f;

    public readonly List<RuneImpact> Impacts = new();
    public RuneAim? Aim;
    public readonly Dictionary<EntityUid, RuneSigilTrack> Tracks = new();
    private readonly List<(EntityUid Light, float Start, float Energy)> _flashes = new();
    private readonly List<EntityUid> _gone = new();
    private readonly Dictionary<RuneMark, ProtoId<RuneMarkIconPrototype>> _icons = new();
    private int _seed;

    public float Now => (float) _timing.RealTime.TotalSeconds;

    public override void Initialize()
    {
        base.Initialize();
        for (var mark = RuneMark.Burning; mark <= RuneMark.Displaced; mark++)
            _icons[mark] = "RuneMarkIcon" + mark;

        SubscribeLocalEvent<RuneMarkComponent, GetStatusIconsEvent>(OnMarkIcons);
        SubscribeLocalEvent<RuneScrollComponent, ComponentStartup>((uid, comp, _) => UpdateScroll(uid, comp));
        SubscribeLocalEvent<RuneScrollComponent, AfterAutoHandleStateEvent>((uid, comp, _) => UpdateScroll(uid, comp));
        SubscribeLocalEvent<RuneTintComponent, ComponentStartup>((uid, comp, _) => UpdateTint(uid, comp.Color));
        SubscribeLocalEvent<RuneTintComponent, AfterAutoHandleStateEvent>((uid, comp, _) => UpdateTint(uid, comp.Color));
        SubscribeLocalEvent<RuneTintComponent, ComponentShutdown>((uid, _, _) => UpdateTint(uid, Color.White));
        SubscribeLocalEvent<RuneSigilComponent, ComponentShutdown>(OnSigilShutdown);
        SubscribeNetworkEvent<RuneImpactEvent>(OnImpact);
        _overlays.AddOverlay(new RuneSigilOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<RuneSigilOverlay>();
        foreach (var track in Tracks.Values)
        {
            if (track.Light is { } light)
                QueueDel(light);
        }

        Tracks.Clear();
        Impacts.Clear();
    }

    private void OnMarkIcons(Entity<RuneMarkComponent> ent, ref GetStatusIconsEvent args)
    {
        if (ent.Comp.Mark == RuneMark.None || ent.Comp.End <= _timing.CurTime)
            return;

        var viewer = _player.LocalEntity;
        if (!HasComp<RuneCasterComponent>(viewer) && !HasComp<GhostComponent>(viewer))
            return;

        if (_icons.TryGetValue(ent.Comp.Mark, out var id) && _proto.TryIndex(id, out var icon))
            args.StatusIcons.Add(icon);
    }

    private void UpdateScroll(EntityUid uid, RuneScrollComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        SetGlyph((uid, sprite), FormLayer, "form-", comp.Runes.Count > 0 ? comp.Runes[0] : (ProtoId<RunePrototype>?) null);
        SetGlyph((uid, sprite), ElementLayer, "element-", comp.Runes.Count > 1 ? comp.Runes[1] : (ProtoId<RunePrototype>?) null);
    }

    private void SetGlyph(Entity<SpriteComponent> sprite, string map, string prefix, ProtoId<RunePrototype>? rune)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), map, out var layer, false))
            return;

        if (rune is not { } id || !_proto.TryIndex(id, out var proto))
        {
            _sprite.LayerSetVisible(sprite.AsNullable(), layer, false);
            return;
        }

        _sprite.LayerSetRsiState(sprite.AsNullable(), layer, prefix + id.Id);
        _sprite.LayerSetColor(sprite.AsNullable(), layer, proto.Color);
        _sprite.LayerSetVisible(sprite.AsNullable(), layer, true);
    }

    private void UpdateTint(EntityUid uid, Color color)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            _sprite.SetColor((uid, sprite), color == Color.White ? Color.White : Color.InterpolateBetween(Color.White, color, TintStrength));
    }

    private void OnSigilShutdown(Entity<RuneSigilComponent> ent, ref ComponentShutdown args)
    {
        if (!Tracks.Remove(ent, out var track) || track.Light is not { } light)
            return;

        QueueDel(light);
    }

    private void OnImpact(RuneImpactEvent ev)
    {
        var at = _transform.ToMapCoordinates(GetCoordinates(ev.Point));
        if (at.MapId == MapId.Nullspace)
            return;

        Impacts.Add(new RuneImpact
        {
            At = at,
            Element = ev.Element,
            Second = ev.Second,
            Radius = ev.Radius,
            Strong = ev.Strong,
            Start = Now,
            Seed = ++_seed,
        });

        if (!_proto.TryIndex<RunePrototype>(ev.Element, out var element))
            return;

        var flash = Spawn(null, at);
        var light = _lights.EnsureLight(flash);
        var energy = ev.Strong ? 4f : 2.6f;
        _lights.SetColor(flash, element.Color, light);
        _lights.SetRadius(flash, MathF.Max(2.5f, ev.Radius * 2.2f), light);
        _lights.SetEnergy(flash, energy, light);
        _lights.SetCastShadows(flash, false, light);
        _flashes.Add((flash, Now, energy));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var now = Now;

        Impacts.RemoveAll(i => now - i.Start > ImpactLength);

        for (var i = _flashes.Count - 1; i >= 0; i--)
        {
            var (light, start, energy) = _flashes[i];
            var t = (now - start) / FlashLength;
            if (t >= 1f || TerminatingOrDeleted(light))
            {
                QueueDel(light);
                _flashes.RemoveAt(i);
                continue;
            }

            _lights.SetEnergy(light, energy * (1f - t) * (1f - t));
        }

        _gone.Clear();
        foreach (var uid in Tracks.Keys)
        {
            if (!HasComp<RuneSigilComponent>(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
        {
            if (Tracks.Remove(uid, out var stale) && stale.Light is { } light)
                QueueDel(light);
        }

        var query = EntityQueryEnumerator<RuneSigilComponent>();
        while (query.MoveNext(out var uid, out var sigil))
        {
            if (!Tracks.TryGetValue(uid, out var track))
            {
                track = new RuneSigilTrack { Started = sigil.Started };
                Tracks[uid] = track;
            }

            if (track.Started != sigil.Started)
            {
                track.Started = sigil.Started;
                track.Appeared.Clear();
            }

            while (track.Appeared.Count > sigil.Chain.Count)
                track.Appeared.RemoveAt(track.Appeared.Count - 1);
            while (track.Appeared.Count < sigil.Chain.Count)
                track.Appeared.Add(now);

            UpdateGlow(uid, sigil, track);
        }
    }

    private void UpdateGlow(EntityUid uid, RuneSigilComponent sigil, RuneSigilTrack track)
    {
        if (track.Light == null || TerminatingOrDeleted(track.Light.Value))
        {
            var spawned = Spawn(null, new EntityCoordinates(uid, Vector2.Zero));
            var comp = _lights.EnsureLight(spawned);
            _lights.SetCastShadows(spawned, false, comp);
            _lights.SetSoftness(spawned, 1.5f, comp);
            track.Light = spawned;
        }

        var light = track.Light.Value;
        var color = SigilColor(sigil);
        var age = (float) (_timing.CurTime - sigil.Changed).TotalSeconds;
        var energy = 0.6f + 0.25f * sigil.Chain.Count;
        switch (sigil.State)
        {
            case RuneSigilState.Cast:
                energy *= 2.2f * MathF.Max(0f, 1f - age / 0.7f);
                break;
            case RuneSigilState.Fizzle:
                color = Color.FromHex("#ff3b4a");
                energy *= 1.5f * MathF.Max(0f, 1f - age / 0.9f);
                break;
            case RuneSigilState.Fade:
                energy *= MathF.Max(0f, 1f - age / 0.6f);
                break;
        }

        _lights.SetColor(light, color);
        _lights.SetEnergy(light, energy);
        _lights.SetRadius(light, 1.6f + 0.3f * sigil.Chain.Count);
    }

    public Color SigilColor(RuneSigilComponent sigil)
    {
        if (sigil.Chain.Count > 1 && _proto.TryIndex<RunePrototype>(sigil.Chain[1], out var element))
            return element.Color;

        if (sigil.Chain.Count > 0 && _proto.TryIndex<RunePrototype>(sigil.Chain[0], out var form))
            return form.Color;

        return Color.FromHex("#b89cff");
    }
}
