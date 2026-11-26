// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.Light.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Camera;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// The cardinal's screams. Everything is decided here from the cardinal's own position: the client only asks to
/// scream and draws what it is sent.
/// </summary>
public sealed partial class CardinalScreamSystem : EntitySystem
{
    private static readonly Color RallyColor = Color.FromHex("#ff3030");
    private static readonly Color HuntColor = Color.FromHex("#40ff60");
    private static readonly TimeSpan OverrideInterval = TimeSpan.FromSeconds(0.25);
    private static readonly float[] ShakeSteps = [0f, 0.12f, 0.24f, 0.36f, 0.5f];

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _camera = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PoweredLightSystem _lights = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly List<(EntityUid Light, TimeSpan Until)> _blinking = new();
    private readonly List<(EntityUid Victim, TimeSpan At, float Strength)> _shakes = new();
    private readonly HashSet<(EntityUid Marked, ICommonSession Session)> _overrides = new();
    private readonly HashSet<(EntityUid Marked, ICommonSession Session)> _wanted = new();
    private readonly HashSet<Entity<CircleSquadComponent>> _squad = new();
    private readonly HashSet<Entity<PoweredLightComponent>> _lamps = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private TimeSpan _nextOverrides;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardinalScreamComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CardinalScreamComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CardinalScreamComponent, CardinalScreamActionEvent>(OnAction);
        SubscribeNetworkEvent<CardinalScreamChoiceEvent>(OnChoice);

        SubscribeLocalEvent<CardinalRallyComponent, DidEquipHandEvent>(OnRallyEquip);
        SubscribeLocalEvent<CardinalRallyComponent, DidUnequipHandEvent>(OnRallyUnequip);
        SubscribeLocalEvent<CardinalRallyComponent, ComponentRemove>(OnRallyRemove); // shutdown is taken by the shared system
        SubscribeLocalEvent<CardinalHuntComponent, ComponentShutdown>(OnHuntShutdown);
        SubscribeLocalEvent<CardinalMarkedComponent, ComponentShutdown>(OnMarkedShutdown);

        SubscribeLocalEvent<CardinalScreamWaveComponent, MapInitEvent>(OnWaveInit);
    }

    private void OnMapInit(Entity<CardinalScreamComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<CardinalScreamComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnWaveInit(Entity<CardinalScreamWaveComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.InitTime = _timing.CurTime;
        Dirty(ent);
    }

    /// <summary>
    /// The action only opens the menu on the cardinal's client; here it opens the short window in which a pick counts.
    /// </summary>
    private void OnAction(Entity<CardinalScreamComponent> ent, ref CardinalScreamActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.ChoiceExpires = _timing.CurTime + TimeSpan.FromSeconds(10);
    }

    private void OnChoice(CardinalScreamChoiceEvent msg, EntitySessionEventArgs args)
    {
        var uid = GetEntity(msg.Cardinal);
        if (args.SenderSession.AttachedEntity != uid || !TryComp<CardinalScreamComponent>(uid, out var scream) ||
            scream.ChoiceExpires is not { } expires || _timing.CurTime > expires)
            return;

        scream.ChoiceExpires = null;
        var ent = (uid, scream);
        switch (msg.Kind)
        {
            case CardinalScreamKind.Rally:
                Rally(ent);
                break;
            case CardinalScreamKind.Hunt:
                Hunt(ent);
                break;
        }
    }

    private void Rally(Entity<CardinalScreamComponent> ent)
    {
        if (!TryScream(ent, ent.Comp.RallyWave, ent.Comp.RallySound, RallyColor))
            return;

        var end = _timing.CurTime + ent.Comp.RallyDuration;
        foreach (var fighter in Squad(ent))
        {
            RallyFighter(fighter, ent.Comp, end);
        }
    }

    private void Hunt(Entity<CardinalScreamComponent> ent)
    {
        if (!TryScream(ent, ent.Comp.HuntWave, ent.Comp.HuntSound, HuntColor))
            return;

        var now = _timing.CurTime;
        var hunters = Squad(ent);
        if (!hunters.Contains(ent.Owner))
            hunters.Add(ent.Owner);

        foreach (var hunter in hunters)
        {
            var hunt = EnsureComp<CardinalHuntComponent>(hunter);
            hunt.EndsAt = Later(hunt.EndsAt, now + ent.Comp.HuntDuration);
            Dirty(hunter, hunt);
            _alerts.ShowAlert(hunter, hunt.Alert, cooldown: (now, hunt.EndsAt));
        }

        // Every outsider on the screen of the cardinal or of a fighter the scream reached.
        var map = Transform(ent).MapID;
        var views = new List<Vector2>(hunters.Count);
        foreach (var hunter in hunters)
        {
            var xform = Transform(hunter);
            if (xform.MapID == map)
                views.Add(_transform.GetWorldPosition(xform));
        }

        var half = ent.Comp.ScreenHalfExtents;
        var reach = half.Length();
        _mobs.Clear();
        foreach (var view in views)
        {
            _lookup.GetEntitiesInRange(new MapCoordinates(view, map), reach, _mobs);
        }

        foreach (var (mob, _) in _mobs)
        {
            if (HasComp<CircleSquadComponent>(mob) || !HasComp<ActorComponent>(mob) || _mobState.IsDead(mob))
                continue;

            var at = _transform.GetWorldPosition(mob);
            if (!OnAnyScreen(at, views, half))
                continue;

            var mark = EnsureComp<CardinalMarkedComponent>(mob);
            mark.EndsAt = Later(mark.EndsAt, now + ent.Comp.MarkDuration);
            Dirty(mob, mark);
            _alerts.ShowAlert(mob, mark.Alert, cooldown: (now, mark.EndsAt));
        }

        _nextOverrides = TimeSpan.Zero;
    }

    private static bool OnAnyScreen(Vector2 at, List<Vector2> views, Vector2 half)
    {
        foreach (var view in views)
        {
            var delta = at - view;
            if (MathF.Abs(delta.X) <= half.X && MathF.Abs(delta.Y) <= half.Y)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The part both screams share: the cooldown, the sound, the rings, blinking lamps and shaking screens.
    /// </summary>
    private bool TryScream(Entity<CardinalScreamComponent> ent, EntProtoId wave, SoundSpecifier sound, Color color)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextScream || _mobState.IsIncapacitated(ent))
            return false;

        ent.Comp.NextScream = now + ent.Comp.Cooldown;
        Dirty(ent);
        _actions.SetCooldown(ent.Comp.ActionEntity, now, ent.Comp.NextScream);

        _audio.PlayPvs(sound, ent);
        var spawned = SpawnAttachedTo(wave, new EntityCoordinates(ent, Vector2.Zero));
        if (TryComp<CardinalScreamWaveComponent>(spawned, out var rings))
        {
            rings.Color = color;
            rings.Radius = ent.Comp.Radius;
            Dirty(spawned, rings);
        }

        var coords = Transform(ent).Coordinates;
        _lamps.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Radius, _lamps);
        foreach (var (lamp, light) in _lamps)
        {
            if (light.IsBlinking)
                continue;

            _lights.ToggleBlinkingLight(lamp, light, true);
            _blinking.Add((lamp, now + ent.Comp.LightBlink));
        }

        _mobs.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Radius, _mobs);
        var center = _transform.GetWorldPosition(ent);
        foreach (var (mob, _) in _mobs)
        {
            if (!HasComp<CameraRecoilComponent>(mob))
                continue;

            var distance = Vector2.Distance(_transform.GetWorldPosition(mob), center);
            var strength = ent.Comp.ShakeStrength * (1f - 0.6f * Math.Clamp(distance / ent.Comp.Radius, 0f, 1f));
            for (var i = 0; i < ShakeSteps.Length; i++)
            {
                var fade = 1f - (float) i / ShakeSteps.Length;
                _shakes.Add((mob, now + TimeSpan.FromSeconds(ShakeSteps[i]), strength * fade));
            }
        }

        return true;
    }

    /// <summary>
    /// Living fighters of the squad within the scream's reach, the cardinal among them.
    /// </summary>
    private List<EntityUid> Squad(Entity<CardinalScreamComponent> ent)
    {
        _squad.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Radius, _squad);
        var list = new List<EntityUid>(_squad.Count + 1);
        foreach (var (fighter, _) in _squad)
        {
            if (!_mobState.IsDead(fighter))
                list.Add(fighter);
        }

        if (HasComp<CircleSquadComponent>(ent) && !list.Contains(ent.Owner))
            list.Add(ent.Owner);

        return list;
    }

    private void RallyFighter(EntityUid fighter, CardinalScreamComponent scream, TimeSpan end)
    {
        var rally = EnsureComp<CardinalRallyComponent>(fighter);
        rally.EndsAt = Later(rally.EndsAt, end);
        rally.SpeedModifier = scream.SpeedModifier;
        rally.FireRateBonus = scream.FireRateBonus;
        rally.EnsnareRelief = scream.EnsnareRelief;
        Dirty(fighter, rally);
        _alerts.ShowAlert(fighter, rally.Alert, cooldown: (_timing.CurTime, rally.EndsAt));

        foreach (var held in _hands.EnumerateHeld(fighter))
        {
            BoostGun((fighter, rally), held);
        }
    }

    private void BoostGun(Entity<CardinalRallyComponent> fighter, EntityUid gun)
    {
        if (!HasComp<GunComponent>(gun))
            return;

        var boost = EnsureComp<CardinalRallyGunComponent>(gun);
        boost.FireRateBonus = fighter.Comp.FireRateBonus;
        Dirty(gun, boost);
        fighter.Comp.BoostedGuns.Add(gun);
        _gun.RefreshModifiers(gun);
    }

    private void UnboostGun(EntityUid gun)
    {
        if (TerminatingOrDeleted(gun) || !RemComp<CardinalRallyGunComponent>(gun))
            return;

        _gun.RefreshModifiers(gun);
    }

    private void OnRallyEquip(Entity<CardinalRallyComponent> ent, ref DidEquipHandEvent args)
    {
        if (_timing.CurTime < ent.Comp.EndsAt)
            BoostGun(ent, args.Equipped);
    }

    private void OnRallyUnequip(Entity<CardinalRallyComponent> ent, ref DidUnequipHandEvent args)
    {
        if (ent.Comp.BoostedGuns.Remove(args.Unequipped))
            UnboostGun(args.Unequipped);
    }

    private void OnRallyRemove(Entity<CardinalRallyComponent> ent, ref ComponentRemove args)
    {
        foreach (var gun in ent.Comp.BoostedGuns)
        {
            UnboostGun(gun);
        }

        ent.Comp.BoostedGuns.Clear();
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnHuntShutdown(Entity<CardinalHuntComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        _nextOverrides = TimeSpan.Zero;
    }

    private void OnMarkedShutdown(Entity<CardinalMarkedComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
        _nextOverrides = TimeSpan.Zero;
    }

    private static TimeSpan Later(TimeSpan a, TimeSpan b) => a > b ? a : b;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var rallies = EntityQueryEnumerator<CardinalRallyComponent>();
        while (rallies.MoveNext(out var uid, out var rally))
        {
            if (now >= rally.EndsAt)
                RemCompDeferred<CardinalRallyComponent>(uid);
        }

        var hunts = EntityQueryEnumerator<CardinalHuntComponent>();
        while (hunts.MoveNext(out var uid, out var hunt))
        {
            if (now >= hunt.EndsAt)
                RemCompDeferred<CardinalHuntComponent>(uid);
        }

        var marks = EntityQueryEnumerator<CardinalMarkedComponent>();
        while (marks.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.EndsAt)
                RemCompDeferred<CardinalMarkedComponent>(uid);
        }

        for (var i = _blinking.Count - 1; i >= 0; i--)
        {
            var (lamp, until) = _blinking[i];
            if (now < until)
                continue;

            _blinking.RemoveAt(i);
            if (TryComp<PoweredLightComponent>(lamp, out var light))
                _lights.ToggleBlinkingLight(lamp, light, false);
        }

        for (var i = _shakes.Count - 1; i >= 0; i--)
        {
            var (victim, at, strength) = _shakes[i];
            if (now < at)
                continue;

            _shakes.RemoveAt(i);
            if (!TerminatingOrDeleted(victim))
                _camera.KickCamera(victim, _random.NextAngle().ToVec() * strength);
        }

        if (now >= _nextOverrides)
        {
            _nextOverrides = now + OverrideInterval;
            UpdateOverrides();
        }
    }

    /// <summary>
    /// Hunting fighters are sent every marked outsider, however far, so they can follow them off their screen.
    /// </summary>
    private void UpdateOverrides()
    {
        _wanted.Clear();
        var hunts = EntityQueryEnumerator<CardinalHuntComponent, TransformComponent>();
        while (hunts.MoveNext(out var hunter, out var hunt, out var hunterXform))
        {
            hunt.Targets.Clear();
            hunt.TargetsMap = GetNetEntity(hunterXform.MapUid) ?? NetEntity.Invalid;
            TryComp<ActorComponent>(hunter, out var actor);
            var marks = EntityQueryEnumerator<CardinalMarkedComponent, TransformComponent>();
            while (marks.MoveNext(out var marked, out _, out var markedXform))
            {
                if (actor != null)
                    _wanted.Add((marked, actor.PlayerSession));

                if (markedXform.MapID == hunterXform.MapID)
                    hunt.Targets.Add(_transform.GetWorldPosition(markedXform));
            }

            Dirty(hunter, hunt);
        }

        foreach (var pair in _overrides)
        {
            if (!_wanted.Contains(pair) && !TerminatingOrDeleted(pair.Marked))
                _pvs.RemoveSessionOverride(pair.Marked, pair.Session);
        }

        _overrides.RemoveWhere(pair => !_wanted.Contains(pair));
        foreach (var pair in _wanted)
        {
            if (_overrides.Add(pair))
                _pvs.AddSessionOverride(pair.Marked, pair.Session);
        }
    }
}
