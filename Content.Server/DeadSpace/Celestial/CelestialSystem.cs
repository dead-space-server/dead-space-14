using Content.Server.Audio;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Celestial;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server.DeadSpace.Celestial;

/// <summary>
/// Поведение босса Селестиала: медленно движется, разрушает всё под собой
/// (как сингулярность), говорит субтитрами, атакует лучами и сферами.
/// </summary>
public sealed class CelestialSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TileSystem _tiles = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _globalSound = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly HashSet<EntityUid> _nearbySpheres = new();

    private static readonly DamageSpecifier _sphereDamage = MakeSphereDamage();
    private static readonly DamageSpecifier _beamDamage = MakeBeamDamage();

    private static float AngleDiff(float from, float to)
    {
        var d = (to - from) % MathF.Tau;
        if (d > MathF.PI)
            d -= MathF.Tau;
        if (d < -MathF.PI)
            d += MathF.Tau;
        return d;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq < 0.0001f)
            return (p - a).Length();
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        return (p - (a + ab * t)).Length();
    }

    private static DamageSpecifier MakeSphereDamage()
    {
        var d = new DamageSpecifier();
        d.DamageDict.TryAdd("Heat", 10f);
        d.DamageDict.TryAdd("Blunt", 10f);
        return d;
    }

    private static DamageSpecifier MakeBeamDamage()
    {
        var d = new DamageSpecifier();
        d.DamageDict.TryAdd("Heat", 40f);
        d.DamageDict.TryAdd("Blunt", 20f);
        d.DamageDict.TryAdd("Structural", 50f);
        return d;
    }

    private const float DestroyInterval = 0.25f;
    private const float DestroyRadius = 1.4f;

    private readonly HashSet<EntityUid> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CelestialComponent, MapInitEvent>(OnBossMapInit);
    }

    private void OnBossMapInit(Entity<CelestialComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.CutscenePlayed)
            return;
        ent.Comp.CutscenePlayed = true;
        PlayIntroCutscene(ent);
    }

    // ------------------------------------------------------------------
    // Стартовая катсцена
    // ------------------------------------------------------------------

    private static readonly string[] CutscenePhrases =
    {
        "ТЫ НАПОМИНАЕШЬ МНЕ ЦВЕТОК, ЧТО РАСЦВЕЛ В ПОЛНУЮ СИЛУ В САДАХ.",
        "ОН ГНАЛСЯ ЗА НЕВОЗМОЖНЫМ, РАСПУСКАЯСЬ В САМЫХ СУРОВЫХ УСЛОВИЯХ.",
        "И ВСЕ ЖЕ ИМЕННО ЭТА ГОРДЫНЯ ЗАСТАВИЛА ЕГО СГНИТЬ ИЗНУТРИ.",
        "ДАЖЕ ЕСЛИ ТЫ ВЕРИШЬ В СОБСТВЕННЫЕ ЛЕПЕСТКИ...",
        "ТЕБЕ СУЖДЕНО УВЯНУТЬ В ЭТОМ ТАНЦЕ СО МНОЙ.",
    };

    private const float CutscenePhraseDuration = 3f;
    private const float CutsceneIntroTime = 4f;

    private void PlayIntroCutscene(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;

        // клиент рисует оверлей катсцены сам
        RaiseCelestial(ent, new CelestialCutsceneStartEvent());
        RaiseCelestial(ent, new CelestialSpiritStageEvent(0));

        // фразы подряд, как в mytalkingcelestial
        for (var i = 0; i < CutscenePhrases.Length; i++)
        {
            var phrase = CutscenePhrases[i];
            var delay = CutsceneIntroTime + i * CutscenePhraseDuration;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                if (comp.LocalizedEvents)
                    SpeakFor(ent, phrase, CutscenePhraseDuration);
                else
                    SpeakGlobal(phrase, CutscenePhraseDuration);

                // на последней фразе: особый звук и открытые глаза
                if (phrase == CutscenePhrases[^1])
                {
                    _globalSound.PlayGlobalOnStation(ent,
                        _audio.ResolveSound(new SoundPathSpecifier(
                            "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_Talk_4.ogg")),
                        new AudioParams { Volume = -2f });
                    RaiseCelestial(ent, new CelestialSpiritStageEvent(1));
                }
            });
        }

        // после последней фразы: разрыв кокона
        var breachTime = CutsceneIntroTime + CutscenePhrases.Length * CutscenePhraseDuration;
        Timer.Spawn(TimeSpan.FromSeconds(breachTime), () =>
        {
            RaiseCelestial(ent, new CelestialSpiritStageEvent(2));
        });

        // конец катсцены: финальный крик
        var endTime = breachTime + 3f;
        Timer.Spawn(TimeSpan.FromSeconds(endTime), () =>
        {
            _globalSound.PlayGlobalOnStation(ent,
                _audio.ResolveSound(new SoundPathSpecifier(
                    "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_screams_really_loudly.ogg")),
                new AudioParams { Volume = 2f });
            RaiseCelestial(ent, new CelestialCutsceneEndEvent());

            if (!TerminatingOrDeleted(ent))
                comp.AttackTimer = 8f;
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateOrbs(frameTime);
        UpdateCircles(frameTime);

        var query = EntityQueryEnumerator<CelestialComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            DestroySurroundings((uid, comp), frameTime);

            // вторая фаза: 500к урона -> рёв и бессмертие
            if (!comp.Phase2 && TryComp<DamageableComponent>(uid, out var dmgCheck) && dmgCheck.TotalDamage >= 500000f)
            {
                comp.Phase2 = true;

                _globalSound.PlayGlobalOnStation(uid,
                    _audio.ResolveSound(new SoundPathSpecifier(
                        "/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Roar_Phase2.ogg")),
                    new AudioParams { Volume = 2f });

                // крик "ВЫХОДА НЕТ" + атакует чаще и по большему числу людей
                SpeakFor((uid, comp), "ВЫХОДА НЕТ.", 4f);
                comp.ExtraTargets = comp.Phase2ExtraTargets;
            }

            // бессмертие: сбрасываем урон только если его реально нанесли
            if (comp.Phase2
                && TryComp<DamageableComponent>(uid, out var dmgImmortal)
                && dmgImmortal.TotalDamage > 0f)
            {
                _damage.SetDamage((uid, dmgImmortal), new DamageSpecifier());
            }

            comp.AttackTimer -= frameTime;

            // пока идёт атака - таймер не даёт мгновенного перезапуска
            if (comp.Attacking)
                comp.AttackTimer = MathF.Max(comp.AttackTimer, 3f);

            if (comp.AttackTimer <= 0f && !comp.Attacking)
            {
                // некому атаковать на карте босса - ждём
                if (GetTargets((uid, comp), 1).Count == 0)
                {
                    comp.AttackTimer = 5f;
                    continue;
                }

                comp.AttackTimer = comp.Phase2
                    ? _random.NextFloat(comp.Phase2AttackMinDelay, comp.Phase2AttackMaxDelay)
                    : _random.NextFloat(comp.AttackMinDelay, comp.AttackMaxDelay);

                // перед каждой атакой Селестиал телепортируется к целям или в случайную точку
                TeleportBoss((uid, comp));

                switch (comp.AttackCycle % 5)
                {
                    case 0:
                        StartAttack((uid, comp)); // ПАДИ
                        break;
                    case 1:
                        StartFutility((uid, comp)); // ТЩЕТНО
                        break;
                    case 2:
                        StartRaznesu((uid, comp)); // РАЗНЕСУ
                        break;
                    case 3:
                        StartFreeze((uid, comp)); // ЗАМРИ
                        break;
                    default:
                        StartFlash((uid, comp)); // ВСПЫХНИ ВО ТЬМУ
                        // StartBloom((uid, comp)); // УВЯНЬ В ЦВЕТУ (временно выключена)
                        break;
                }
                comp.AttackCycle++;
            }
        }

        // сферы: стоят на месте, растут, наносят урон внутри
        var spheres = EntityQueryEnumerator<CelestialSphereComponent>();
        while (spheres.MoveNext(out var uid, out var comp))
        {
            comp.Elapsed += frameTime;
            if (comp.Elapsed >= comp.Lifetime)
            {
                QueueDel(uid);
                continue;
            }

            comp.DamageAccumulator += frameTime;
            if (comp.DamageAccumulator < comp.DamageInterval)
                continue;
            comp.DamageAccumulator -= comp.DamageInterval;

            // радиус сферы по кривой роста (совпадает с клиентской отрисовкой)
            var t = Math.Clamp(comp.Elapsed / comp.Lifetime, 0f, 1f);
            var radius = MathHelper.Lerp(comp.StartScale, comp.EndScale, MathF.Pow(t, comp.GrowthExponent));
            var sphereXform = Transform(uid);
            _nearbySpheres.Clear();
            _lookup.GetEntitiesInRange(sphereXform.Coordinates, radius, _nearbySpheres, LookupFlags.Uncontained);
            foreach (var victim in _nearbySpheres)
            {
                if (victim == uid || TerminatingOrDeleted(victim))
                    continue;
                _damage.TryChangeDamage(victim, _sphereDamage, true);
            }
        }
    }

    private void DestroySurroundings(Entity<CelestialComponent> ent, float frameTime)
    {
        ent.Comp.DestroyAccumulator += frameTime;
        if (ent.Comp.DestroyAccumulator < DestroyInterval)
            return;
        ent.Comp.DestroyAccumulator -= DestroyInterval;

        var xform = Transform(ent);
        if (xform.MapUid == null)
            return;

        // крушим всё под собой, КРОМЕ людей/мобов, снарядов и пола
        var damage = new DamageSpecifier();
        damage.DamageDict.TryAdd("Structural", 200f);
        damage.DamageDict.TryAdd("Heat", 100f);

        _nearbySpheres.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, DestroyRadius, _nearbySpheres, LookupFlags.Uncontained);
        foreach (var candidate in _nearbySpheres)
        {
            if (candidate == ent.Owner || TerminatingOrDeleted(candidate))
                continue;
            if (HasComp<MobStateComponent>(candidate))
                continue;
            if (HasComp<ProjectileComponent>(candidate))
                continue;
            if (HasComp<CelestialComponent>(candidate))
                continue;
            _damage.TryChangeDamage(candidate, damage, true);
        }
    }

    // ------------------------------------------------------------------
    // Речь: только крик атаки
    // ------------------------------------------------------------------

    private MapId? _deathCutsceneMap;

    /// <summary>Катсцена смерти: глобально или на карте убитого босса.</summary>
    public void BroadcastDeath(EntityUid? boss)
    {
        _deathCutsceneMap = null;
        if (boss != null
            && TryComp<CelestialComponent>(boss.Value, out var bossComp)
            && bossComp.LocalizedEvents
            && !TerminatingOrDeleted(boss.Value))
        {
            _deathCutsceneMap = Transform(boss.Value).MapID;
        }

        RaiseToMapOrAll(new CelestialDeathEvent());
    }

    public void BroadcastDeathSpeak(string text, float duration)
    {
        RaiseToMapOrAll(new CelestialDeathSpeakEvent(text, duration));
    }

    public void BroadcastDeathEnd()
    {
        RaiseToMapOrAll(new CelestialDeathEndEvent());
    }

    private void RaiseToMapOrAll(EntityEventArgs ev)
    {
        if (_deathCutsceneMap == null)
        {
            RaiseNetworkEvent(ev);
            return;
        }

        var mapId = _deathCutsceneMap.Value;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || TerminatingOrDeleted(player))
                continue;
            if (Transform(player).MapID != mapId)
                continue;
            RaiseNetworkEvent(ev, session.Channel);
        }
    }

    private void Speak(string text, float duration)
    {
        RaiseNetworkEvent(new CelestialSpeakEvent(text, duration));
    }

    /// <summary>События босса: глобально или только на его карте.</summary>
    private void RaiseCelestial(Entity<CelestialComponent> ent, EntityEventArgs ev)
    {
        if (!ent.Comp.LocalizedEvents)
        {
            RaiseNetworkEvent(ev);
            return;
        }

        var mapId = Transform(ent).MapID;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || TerminatingOrDeleted(player))
                continue;
            if (Transform(player).MapID != mapId)
                continue;
            RaiseNetworkEvent(ev, session.Channel);
        }
    }

    private void SpeakFor(Entity<CelestialComponent> ent, string text, float duration)
    {
        RaiseCelestial(ent, new CelestialSpeakEvent(text, duration));
    }

    /// <summary>Публичный показ субтитров со звуком (для консольной команды).</summary>
    public void SpeakGlobal(string text, float duration)
    {
        if (_talkSounds.Count > 0)
            _globalSound.PlayAnnonceGlobal(Filter.Broadcast(), _audio.ResolveSound(_talkSounds[_random.Next(_talkSounds.Count)]),
                new AudioParams { Volume = -8f });
        Speak(text, duration);
    }

    private readonly List<SoundSpecifier> _talkSounds = new()
    {
        new SoundPathSpecifier("/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_Talk_1.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_Talk_2.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/TEMP_FOR_EVENT/Ivan_KuvalDROID/sounds/Celestial_Talk_3.ogg"),
    };

    private void SpeakAttack(Entity<CelestialComponent> ent, CelestialComponent comp)
    {
        if (comp.TalkSounds.Count > 0)
        {
            var sound = _random.Pick(comp.TalkSounds);
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(sound),
                new AudioParams { Volume = comp.AttackVolume });
        }

        SpeakFor(ent, comp.AttackLine, 4.5f);
    }

    private void StartAttack(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;
        var firstTargets = GetTargets(ent, 1);
        if (firstTargets.Count == 0)
            return;

        comp.Attacking = true;
        RunBeamPhase(ent, comp);

        Timer.Spawn(TimeSpan.FromSeconds(comp.BeamCount * comp.BeamDelay + 1f + comp.SpheresAfterAttackDelay), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            SummonSpheres(ent, comp);
            comp.Attacking = false;
        });
    }

    /// <summary>
    /// Лучевая фаза: крик "ПАДИ.", серия одиночных лучей и залп.
    /// </summary>
    private void RunBeamPhase(Entity<CelestialComponent> ent, CelestialComponent comp, bool speak = true)
    {
        if (speak)
            SpeakAttack(ent, comp);

        for (var i = 0; i < comp.BeamCount; i++)
        {
            var delay = i * comp.BeamDelay;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                Sound(comp.VariationSound ?? comp.BeamSound, ent);
                BeamStrike(ent, comp, 1);
            });
        }

        var finaleDelay = comp.BeamCount * comp.BeamDelay + 1f;
        Timer.Spawn(TimeSpan.FromSeconds(finaleDelay), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            Sound(comp.VolleySound, ent);
            FinalVolley(ent, comp);
        });
    }

    private void BeamStrike(Entity<CelestialComponent> ent, CelestialComponent comp, int beamCount, bool telegraph = true)
    {
        var targets = GetTargets(ent, beamCount + comp.ExtraTargets);
        foreach (var target in targets)
        {
            FireBeamsAround(ent, comp, target, beamCount, telegraph);
        }
    }

    private void FinalVolley(Entity<CelestialComponent> ent, CelestialComponent comp)
    {
        var targets = GetTargets(ent, 1 + comp.ExtraTargets);
        var count = Math.Max(1, comp.FinalBeamCount);
        foreach (var target in targets)
        {
            var baseAngle = _random.NextFloat(0f, MathF.Tau);
            for (var i = 0; i < count; i++)
            {
                var fixedAngle = baseAngle + i * (MathF.Tau / count);
                FireBeamsAround(ent, comp, target, 1, telegraph: false, forcedAngle: fixedAngle);
            }
        }
    }

    private void FireBeamsAround(Entity<CelestialComponent> ent, CelestialComponent comp,
        EntityUid tgt, int beamCount, bool telegraph = true, float? forcedAngle = null)
    {
        var targetPos = _transform.GetMapCoordinates(tgt);
        var aimPos = targetPos.Position;

        for (var i = 0; i < beamCount; i++)
        {
            var spawnAngle = forcedAngle ?? _random.NextFloat(0f, MathF.Tau);
            var spawnDist = _random.NextFloat(comp.BeamStartMinDist, comp.BeamStartMaxDist);
            var spawnDir = new Vector2(MathF.Cos(spawnAngle), MathF.Sin(spawnAngle));
            var startPos = aimPos - spawnDir * spawnDist;

            var end = aimPos + spawnDir * comp.BeamOvershoot;
            var beamStart = startPos;
            var beamEnd = end;

            RaiseNetworkEvent(new CelestialBeamVisualEvent(
                startPos, end, (int) targetPos.MapId, comp.BeamChargeTime, comp.DarkBeamDuration));

            var strikeMap = new MapCoordinates(aimPos, targetPos.MapId);

            Timer.Spawn(TimeSpan.FromSeconds(comp.BeamChargeTime), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                Sound(comp.BeamSound ?? comp.VariationSound, ent);

                // урон всем, кто стоит НА ЛИНИИ луча (в 1 м от отрезка)
                var beamMid = (beamStart + beamEnd) / 2f;
                var beamHalfLen = (beamEnd - beamStart).Length() / 2f + 1.2f;
                var beamMidMap = new MapCoordinates(beamMid, strikeMap.MapId);
                _nearbySpheres.Clear();
                foreach (var victim in _lookup.GetEntitiesInRange(beamMidMap, beamHalfLen, LookupFlags.Uncontained))
                {
                    if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                        continue;

                    var victimPos = _transform.GetMapCoordinates(victim).Position;
                    if (DistanceToSegment(victimPos, beamStart, beamEnd) > 1f)
                        continue;

                    _damage.TryChangeDamage(victim, _beamDamage, true);
                }

                // ломаем пол под ударом
                foreach (var gridEnt in _mapManager.GetAllGrids(strikeMap.MapId))
                {
                    foreach (var tile in _map.GetTilesIntersecting(gridEnt.Owner, gridEnt.Comp, new Circle(strikeMap.Position, 0.9f)))
                    {
                        _tiles.PryTile(tile);
                    }
                }
            });
        }
    }

    // ------------------------------------------------------------------
    // Вторая атака: ТЩЕТНО
    // ------------------------------------------------------------------

    private void StartFutility(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;
        var targets = GetTargets(ent, comp.FutilityTargets);
        if (targets.Count == 0)
            return;

        comp.Attacking = true;

        // говор Селестиала перед атакой
        if (comp.TalkSounds.Count > 0)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.TalkSounds)),
                new AudioParams { Volume = -8f });

        SpeakFor(ent, "ТЩЕТНО.", 4f);

        // шары ДЕЛЯТСЯ: у каждой цели одновременно гоняются не больше
        // OrbsPerCrack шаров; погиб — из нового рифта выходит следующий
        var orbPhase = comp.FutilityRepeats * comp.FutilityRepeatDelay + 3f;
        var quota = comp.OrbsPerCrack * comp.FutilityRepeats;
        foreach (var target in targets)
        {
            var spawned = 0;
            var alive = new List<EntityUid>();
            var check = 0f;
            while (check < orbPhase)
            {
                var delay = check;
                var targetRef = target;
                var aliveRef = alive;
                Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                {
                    if (TerminatingOrDeleted(ent))
                        return;

                    // чистим исчезнувшие шары
                    aliveRef.RemoveAll(o => o == EntityUid.Invalid || TerminatingOrDeleted(o) || !Exists(o));

                    if (spawned >= quota || aliveRef.Count >= comp.OrbsPerCrack)
                        return;
                    if (TerminatingOrDeleted(targetRef))
                        return;

                    if (SpawnRiftAndOrb(ent, comp, targetRef, aliveRef, 1))
                        spawned++;
                });
                check += 1f;
            }
        }

        var afterOrbs = orbPhase;

        // Cutter по тем, кто НЕ был целью (с рандомным смещением), 4 повтора
        var cutterTargets = GetTargets(ent, 1 + comp.ExtraTargets + comp.FutilityTargets);
        var nonTargets = cutterTargets.Where(t => !targets.Contains(t)).ToList();
        for (var r = 0; r < comp.CutterRepeats; r++)
        {
            var delay = afterOrbs + r * comp.CutterRepeatDelay;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                // во время каттера она ничего не говорит
                foreach (var target in nonTargets)
                {
                    if (TerminatingOrDeleted(target))
                        continue;
                    RunCutter(ent, comp, target, final: r == comp.CutterRepeats - 1);
                }
            });
        }

        // лучи СРАЗУ после шаров (без "ПАДИ"), потом сферы
        var afterCutter = afterOrbs + comp.CutterRepeats * comp.CutterRepeatDelay + 0.5f;
        Timer.Spawn(TimeSpan.FromSeconds(afterCutter), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            RunBeamPhase(ent, comp, speak: false);
        });

        var afterBeams = afterCutter + comp.BeamCount * comp.BeamDelay + 1f + 0.3f;
        Timer.Spawn(TimeSpan.FromSeconds(afterBeams), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            SummonSpheres(ent, comp);
        });

        Timer.Spawn(TimeSpan.FromSeconds(afterBeams + 4f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            comp.Attacking = false;
        });
    }

    private bool SpawnRiftAndOrb(Entity<CelestialComponent> ent, CelestialComponent comp,
        EntityUid target, List<EntityUid> alive, int orbsToSpawn)
    {
        var targetPos = _transform.GetMapCoordinates(target);
        var spawnedAny = false;

        // рифты вокруг цели, из каждого выходит свой шар
        for (var i = 0; i < orbsToSpawn; i++)
        {
            var riftAngle = _random.NextFloat(0f, MathF.Tau);
            var riftDist = _random.NextFloat(1.5f, 3.5f);
            var riftCenter = targetPos.Position +
                new Vector2(MathF.Cos(riftAngle) * riftDist, MathF.Sin(riftAngle) * riftDist);

            // рифт: текстура телепорта + звук появления
            Spawn("CelestialRift", new MapCoordinates(riftCenter, targetPos.MapId));
            if (comp.FutilityCrackSound != null)
                _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.FutilityCrackSound),
                    new AudioParams { Volume = -4f });

            // шар выходит из этого рифта
            var riftMap = new MapCoordinates(riftCenter, targetPos.MapId);
            Timer.Spawn(TimeSpan.FromSeconds(0.8f), () =>
            {
                if (TerminatingOrDeleted(target) || TerminatingOrDeleted(ent))
                    return;

                if (comp.FutilityOrbSounds.Count > 0)
                    _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.FutilityOrbSounds)),
                        new AudioParams { Volume = -8f });

                var orb = Spawn("CelestialOrb", riftMap);
                var orbComp = EnsureComp<CelestialOrbComponent>(orb);
                orbComp.Target = target;
                orbComp.Phase = _random.NextFloat(0f, MathF.Tau);
                orbComp.Lifetime = comp.OrbLifetime;
                orbComp.Speed = comp.OrbSpeed;
                orbComp.VelocityDir = Vector2.Normalize(
                    _transform.GetMapCoordinates(target).Position - riftCenter);
                alive.Add(orb);
            });
            spawnedAny = true;
        }

        return spawnedAny;
    }

    private void FireSmallBeam(Vector2 from, MapId mapId)
    {
        var angle = _random.NextFloat(0f, MathF.Tau);
        var length = _random.NextFloat(4f, 7f);
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var start = from;
        var end = from + dir * length;

        // тонкий луч стоит: короткая розовая фаза, затем чёрный
        RaiseNetworkEvent(new CelestialBeamVisualEvent(start, end, (int) mapId, 0.35f, 0.45f, 0.4f));

        // мелкий урон (10-20) всем на линии
        var damage = new DamageSpecifier();
        var amount = _random.NextFloat(7f, 14f);
        damage.DamageDict.TryAdd("Heat", amount * 0.5f);
        damage.DamageDict.TryAdd("Blunt", amount * 0.5f);

        var mid = (start + end) / 2f;
        var halfLen = length / 2f + 0.8f;
        foreach (var victim in _lookup.GetEntitiesInRange(new MapCoordinates(mid, mapId), halfLen, LookupFlags.Uncontained))
        {
            if (TerminatingOrDeleted(victim))
                continue;
            if (DistanceToSegment(_transform.GetMapCoordinates(victim).Position, start, end) > 0.8f)
                continue;
            _damage.TryChangeDamage(victim, damage, true);
        }
    }

    // ------------------------------------------------------------------
    // Cutter: 8 лучей, вращаются вокруг центра и вспыхивают
    // ------------------------------------------------------------------

    private void RunCutter(Entity<CelestialComponent> ent, CelestialComponent comp, EntityUid target, bool final = false)
    {
        if (comp.CutterChargeSound != null)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.CutterChargeSound),
                new AudioParams { Volume = -4f });

        var bossPos = _transform.GetMapCoordinates(ent);
        var targetPos = _transform.GetMapCoordinates(target);

        // Cutter разворачивается ПРЯМО У КУЧИ игроков
        var offsetAngle = _random.NextFloat(0f, MathF.Tau);
        var offsetDist = _random.NextFloat(3f, 6f);
        var center = targetPos.Position +
            new Vector2(MathF.Cos(offsetAngle) * offsetDist, MathF.Sin(offsetAngle) * offsetDist);
        var baseAngle = _random.NextFloat(0f, MathF.Tau);

        RaiseNetworkEvent(new CelestialCutterEvent(
            center, (int) bossPos.MapId, baseAngle, comp.CutterLength,
            comp.CutterRotateTime, comp.CutterFireTime));

        // урон в момент вспышки: все на линиях лучей
        Timer.Spawn(TimeSpan.FromSeconds(comp.CutterRotateTime), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;

            if (final && comp.CutterFinalSound != null)
                _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.CutterFinalSound),
                    new AudioParams { Volume = -2f });
            else if (comp.CutterImpactSound != null)
                _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.CutterImpactSound),
                    new AudioParams { Volume = -4f });

            var damage = new DamageSpecifier();
            damage.DamageDict.TryAdd("Heat", comp.CutterDamage * 0.6f);
            damage.DamageDict.TryAdd("Blunt", comp.CutterDamage * 0.4f);

            var halfLen = comp.CutterLength / 2f + 2f;
            _nearbySpheres.Clear();
            foreach (var victim in _lookup.GetEntitiesInRange(new MapCoordinates(center, bossPos.MapId), halfLen, LookupFlags.Uncontained))
            {
                if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                    continue;

                var victimPos = _transform.GetMapCoordinates(victim).Position;
                var victimRel = victimPos - center;

                // у самого центра бьёт гарантированно
                if (victimRel.Length() <= 2.5f)
                {
                    _damage.TryChangeDamage(victim, damage, true);
                    continue;
                }

                // 8 лучей-отрезков из центра (4 линии, противоположные пары)
                var hit = false;
                for (var k = 0; k < 8; k++)
                {
                    var ang = baseAngle + k * MathF.PI / 4f;
                    var rayEnd = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (comp.CutterLength / 2f);
                    if (DistanceToSegment(victimPos, center, rayEnd) <= 1.5f)
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                    _damage.TryChangeDamage(victim, damage, true);
            }
        });
    }

    // ------------------------------------------------------------------
    // Шары из трещин (ТЩЕТНО)
    // ------------------------------------------------------------------

    private void UpdateOrbs(float frameTime)
    {
        var query = EntityQueryEnumerator<CelestialOrbComponent>();
        while (query.MoveNext(out var uid, out var orb))
        {
            orb.Elapsed += frameTime;
            if (orb.Elapsed >= orb.Lifetime)
            {
                QueueDel(uid);
                continue;
            }

            if (orb.Target == null || TerminatingOrDeleted(orb.Target.Value))
            {
                QueueDel(uid);
                continue;
            }

            // петляющее движение к игроку
            var orbPos = _transform.GetMapCoordinates(uid).Position;
            var targetPos = _transform.GetMapCoordinates(orb.Target.Value).Position;
            var toTarget = targetPos - orbPos;
            var dist = toTarget.Length();
            if (dist < 0.8f)
            {
                QueueDel(uid);
                continue;
            }

            // шар НИКОГДА не останавливается: постоянная скорость, поворот
            // с ограниченной скоростью - уйти можно, уводя шар в сторону
            var desired = toTarget / dist;

            if (orb.VelocityDir.LengthSquared() < 0.001f)
                orb.VelocityDir = desired;

            var angle = MathF.Atan2(orb.VelocityDir.Y, orb.VelocityDir.X);
            var targetAngle = MathF.Atan2(desired.Y, desired.X);
            var diff = AngleDiff(angle, targetAngle);
            var maxTurn = orb.TurnRate * frameTime;
            angle += Math.Clamp(diff, -maxTurn, maxTurn);
            orb.VelocityDir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            // лёгкое петляние поверх траектории
            var perpendicular = new Vector2(-orb.VelocityDir.Y, orb.VelocityDir.X);
            var weave = MathF.Sin(orb.Elapsed * orb.WeaveFreq + orb.Phase) * orb.WeaveAmp;
            var moveDir = Vector2.Normalize(orb.VelocityDir + perpendicular * weave);

            var step = orb.Speed * frameTime;
            _transform.SetWorldPosition(uid, orbPos + moveDir * step);

            // стрельба мелкими лучами вдоль пути
            orb.FireAccumulator += frameTime;
            if (orb.FireAccumulator >= orb.FireInterval)
            {
                orb.FireAccumulator -= orb.FireInterval;
                FireSmallBeam(orbPos, _transform.GetMapCoordinates(uid).MapId);
            }
        }
    }

    // ------------------------------------------------------------------
    // Третья атака: РАЗНЕСУ
    // ------------------------------------------------------------------

    private void StartRaznesu(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;
        comp.Attacking = true;

        // говор Селестиала перед атакой
        if (comp.TalkSounds.Count > 0)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.TalkSounds)),
                new AudioParams { Volume = -8f });

        SpeakFor(ent, "РАЗНЕСУ.", 4f);

        var bossPos = _transform.GetMapCoordinates(ent);

        // много кружков по станции и округе
        for (var i = 0; i < comp.CircleCount; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dist = _random.NextFloat(comp.CircleMinDist, comp.CircleMaxDist);
            var pos = bossPos.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
            var circle = Spawn("CelestialCircle", new MapCoordinates(pos, bossPos.MapId));
            if (TryComp<CelestialCircleComponent>(circle, out var circleComp))
            {
                circleComp.Radius = _random.NextFloat(1.8f, 3.4f);
                var dirAngle = _random.NextFloat(0f, MathF.Tau);
                circleComp.WanderDir = new Vector2(MathF.Cos(dirAngle), MathF.Sin(dirAngle));
            }
        }

        // после дрейфа и чёрной фазы - сферы, затем Cutter по ВСЕМ игрокам
        var circlesEnd = comp.CircleWanderTime + 1.4f + 1f;
        Timer.Spawn(TimeSpan.FromSeconds(circlesEnd), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            SummonSpheres(ent, comp);

            Timer.Spawn(TimeSpan.FromSeconds(1.5f), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                // Cutter на всех игроков
                var everyone = GetTargets(ent, 100);
                for (var r = 0; r < comp.CutterRepeats; r++)
                {
                    var delay = r * comp.CutterRepeatDelay;
                    Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                    {
                        if (TerminatingOrDeleted(ent))
                            return;
                        foreach (var target in everyone)
                        {
                            if (TerminatingOrDeleted(target))
                                continue;
                            RunCutter(ent, comp, target, final: r == comp.CutterRepeats - 1);
                        }
                    });
                }

                Timer.Spawn(TimeSpan.FromSeconds(comp.CutterRepeats * comp.CutterRepeatDelay + 0.5f), () =>
                {
                    if (TerminatingOrDeleted(ent))
                        return;
                    comp.Attacking = false;
                });
            });
        });
    }

    private void UpdateCircles(float frameTime)
    {
        var query = EntityQueryEnumerator<CelestialCircleComponent>();
        while (query.MoveNext(out var uid, out var circle))
        {
            circle.Elapsed += frameTime;
            if (circle.Elapsed >= circle.WanderTime + circle.DarkTime)
            {
                QueueDel(uid);
                continue;
            }

            // дрейф: медленно движется, изредка меняя направление
            if (circle.Elapsed < circle.WanderTime)
            {
                var xform = Transform(uid);
                circle.WanderTimer += frameTime;
                if (circle.WanderTimer >= circle.WanderChangeInterval)
                {
                    circle.WanderTimer -= circle.WanderChangeInterval;
                    var ang = MathF.Atan2(circle.WanderDir.Y, circle.WanderDir.X)
                        + _random.NextFloat(-1.2f, 1.2f);
                    circle.WanderDir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
                }

                var pos = _transform.GetMapCoordinates(uid).Position;
                var move = circle.WanderDir * circle.WanderSpeed * frameTime;

                // небольшой магнит: кружок подтягивается к ближайшему игроку
                EntityUid? nearest = null;
                var nearestDist = float.MaxValue;
                foreach (var session in _players.Sessions)
                {
                    if (session.AttachedEntity is not { } player || TerminatingOrDeleted(player))
                        continue;
                    if (Transform(player).MapID != xform.MapID)
                        continue;
                    var d = (_transform.GetMapCoordinates(player).Position - pos).LengthSquared();
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = player;
                    }
                }

                if (nearest != null)
                {
                    var toPlayer = _transform.GetMapCoordinates(nearest.Value).Position - pos;
                    if (toPlayer.Length() > 1.5f)
                        move += Vector2.Normalize(toPlayer) * 0.8f * frameTime;
                }

                _transform.SetWorldPosition(uid, pos + move);
            }
            else
            {
                // чёрная фаза: урон всем внутри, ПЕРВЫЙ тик - сразу при почернении
                if (!circle.DarkStarted)
                {
                    circle.DarkStarted = true;
                    circle.DamageAccumulator = circle.DamageInterval;
                }

                circle.DamageAccumulator += frameTime;
                if (circle.DamageAccumulator < circle.DamageInterval)
                    continue;
                circle.DamageAccumulator -= circle.DamageInterval;

                var radius = circle.Radius * 0.85f;
                var xform = Transform(uid);
                _nearbySpheres.Clear();
                _lookup.GetEntitiesInRange(xform.Coordinates, radius, _nearbySpheres, LookupFlags.Uncontained);
                var dmg = new DamageSpecifier();
                dmg.DamageDict.TryAdd("Heat", circle.DamagePerTick * 0.5f);
                dmg.DamageDict.TryAdd("Blunt", circle.DamagePerTick * 0.5f);
                foreach (var victim in _nearbySpheres)
                {
                    if (victim == uid || TerminatingOrDeleted(victim))
                        continue;
                    _damage.TryChangeDamage(victim, dmg, true);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Сферы вокруг игроков
    // ------------------------------------------------------------------

    private void SummonSpheres(Entity<CelestialComponent> ent, CelestialComponent comp, float multiplier = 1f)
    {
        Sound(comp.TeleportSound, ent);

        // основные цели плюс дубликаты на дополнительных игроков
        var targets = GetTargets(ent, comp.SphereTargets + comp.ExtraTargets);
        foreach (var target in targets)
        {
            var count = (int) MathF.Round(_random.Next(comp.SpheresPerTargetMin, comp.SpheresPerTargetMax + 1) * multiplier);
            for (var i = 0; i < count; i++)
            {
                // сферы стоят на месте: спавним сразу на орбитальной позиции
                var angle = _random.NextFloat(0f, MathF.Tau);
                var dist = _random.NextFloat(comp.SphereSpawnMinDist, comp.SphereSpawnMaxDist);
                var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                var coords = Transform(target).Coordinates.Offset(offset);
                var sphere = Spawn("CelestialAttackSphere", coords);

                // у каждой сферы свой размер и время жизни
                if (TryComp<CelestialSphereComponent>(sphere, out var sphereComp))
                {
                    sphereComp.StartScale = _random.NextFloat(0.3f, 0.6f);
                    sphereComp.EndScale = _random.NextFloat(1.4f, 2.4f);
                    sphereComp.Lifetime = _random.NextFloat(comp.SphereLifetimeMin, comp.SphereLifetimeMax);
                    sphereComp.Arrived = true; // не двигаются
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Пятая атака: ВСПЫХНИ ВО ТЬМУ
    // ------------------------------------------------------------------

    private void StartFlash(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;
        comp.Attacking = true;

        if (comp.TalkSounds.Count > 0)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.TalkSounds)),
                new AudioParams { Volume = -8f });

        SpeakFor(ent, comp.FlashLine, 3f);
        SummonSpheres(ent, comp, comp.FlashSphereMultiplier);

        // (УВЯНЬ В ЦВЕТУ временно выключена - вернуть вызов StartBloom здесь)
        Timer.Spawn(TimeSpan.FromSeconds(comp.SphereLifetimeMax + 0.7f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            comp.Attacking = false;
        });
    }

    // ------------------------------------------------------------------
    // Шестая атака: УВЯНЬ В ЦВЕТУ
    // ------------------------------------------------------------------

    private void StartBloom(Entity<CelestialComponent> ent, CelestialComponent? compOverride = null)
    {
        var comp = compOverride ?? ent.Comp;
        comp.Attacking = true;

        if (comp.TalkSounds.Count > 0)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.TalkSounds)),
                new AudioParams { Volume = -8f });

        SpeakFor(ent, comp.BloomLine, 4f);

        if (comp.BloomChargeSound != null)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.BloomChargeSound),
                new AudioParams { Volume = -2f });

        // три луча от тела Селестиала, каждый плавно следит за своей целью
        var bossMapPos = _transform.GetMapCoordinates(ent);
        var targets = GetTargets(ent, 3);
        var sweeps = new List<(EntityUid target, float angle, float damageAcc)>();
        foreach (var target in targets)
        {
            var startPos = _transform.GetMapCoordinates(target).Position;
            var initAngle = MathF.Atan2(startPos.Y - bossMapPos.Position.Y, startPos.X - bossMapPos.Position.X);
            sweeps.Add((target, initAngle, 0f));
        }

        Timer.Spawn(TimeSpan.FromSeconds(comp.BloomChargeTime), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;

            if (comp.BloomFireSound != null)
                _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(comp.BloomFireSound),
                    new AudioParams { Volume = 0f });

            // обход карты: медленный тик 0.25с, луч неторопливо преследует игрока
            var ticks = (int) (comp.BloomFireTime / 0.25f);
            for (var tick = 1; tick <= ticks; tick++)
            {
                var delay = (tick - 1) * 0.25f;
                Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                {
                    if (TerminatingOrDeleted(ent))
                        return;

                    var bossNow = _transform.GetMapCoordinates(ent);

                    for (var i = 0; i < sweeps.Count; i++)
                    {
                        var (target, angle, damageAcc) = sweeps[i];
                        if (TerminatingOrDeleted(target))
                            continue;

                        var targetPos = _transform.GetMapCoordinates(target);

                        // экстраполяция: целимся туда, где игрок видит сам себя (учёт тикрейта)
                        var targetVel = Vector2.Zero;
                        if (TryComp<PhysicsComponent>(target, out var targetPhysics))
                            targetVel = targetPhysics.LinearVelocity;
                        var aimPos = targetPos.Position + targetVel * 0.15f;

                        // луч очень плавно доворачивается на игрока
                        var desired = MathF.Atan2(
                            aimPos.Y - bossNow.Position.Y,
                            aimPos.X - bossNow.Position.X);
                        var diff = AngleDiff(angle, desired);
                        angle += Math.Clamp(diff, -0.4f * 0.25f, 0.4f * 0.25f);

                        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                        var start = bossNow.Position;
                        var dist = (aimPos - start).Length();
                        var end = start + dir * (dist + 45f);

                        // массивный чёрный луч с розовой обводкой, постоянно обновляется
                        RaiseNetworkEvent(new CelestialBeamVisualEvent(
                            start, end, (int) bossNow.MapId, 0.06f, 0.3f, 2.2f));

                        // урон вдоль линии
                        damageAcc += 0.25f;
                        if (damageAcc >= comp.BloomDamageInterval)
                        {
                            damageAcc = 0f;
                            var bloomDamage = new DamageSpecifier();
                            bloomDamage.DamageDict.TryAdd("Heat", comp.BloomBeamDamage * 0.6f);
                            bloomDamage.DamageDict.TryAdd("Piercing", comp.BloomBeamDamage * 0.4f);

                            _nearbySpheres.Clear();
                            foreach (var victim in _lookup.GetEntitiesInRange(
                                new MapCoordinates(bossNow.Position, bossNow.MapId), dist + 47f, LookupFlags.Uncontained))
                            {
                                if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                                    continue;

                                // проверяем по позиции, экстраполированной вперёд: летящий игрок
                                // видит себя впереди серверной позиции - не бьём по расхождению
                                var victimVel = Vector2.Zero;
                                if (TryComp<PhysicsComponent>(victim, out var victimPhysics))
                                    victimVel = victimPhysics.LinearVelocity;
                                var victimPos = _transform.GetMapCoordinates(victim).Position + victimVel * 0.15f;

                                if (DistanceToSegment(victimPos, start, end) > 1.2f)
                                    continue;

                                _damage.TryChangeDamage(victim, bloomDamage, true);
                            }
                        }

                        sweeps[i] = (target, angle, damageAcc);
                    }
                });
            }
        });

        // атака заканчивается после обхода
        Timer.Spawn(TimeSpan.FromSeconds(comp.BloomChargeTime + comp.BloomFireTime + 0.5f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            comp.Attacking = false;
        });
    }

    // ------------------------------------------------------------------
    // Четвёртая атака: ЗАМРИ
    // ------------------------------------------------------------------

    private void StartFreeze(Entity<CelestialComponent> ent)
    {
        var comp = ent.Comp;
        comp.Attacking = true;

        if (comp.TalkSounds.Count > 0)
            _globalSound.PlayGlobalOnStation(ent, _audio.ResolveSound(_random.Pick(comp.TalkSounds)),
                new AudioParams { Volume = -8f });

        SpeakFor(ent, comp.FreezeLine, 3f);

        var bossPos = _transform.GetMapCoordinates(ent);
        var freezeBeams = new List<(Vector2 start, Vector2 end)>();

        // ОЧЕНЬ много замерших лучей по всей округе
        for (var i = 0; i < comp.FreezeBeamCount; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dist = _random.NextFloat(4f, 40f);
            var center = bossPos.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

            var beamAngle = _random.NextFloat(0f, MathF.Tau);
            var length = _random.NextFloat(16f, 40f);
            var dir = new Vector2(MathF.Cos(beamAngle), MathF.Sin(beamAngle));
            var start = center - dir * (length / 2f);
            var end = center + dir * (length / 2f);

            // 2 секунды розовые (замерли), затем чернеют
            RaiseNetworkEvent(new CelestialBeamVisualEvent(start, end, (int) bossPos.MapId, 2f, 1.5f));
            freezeBeams.Add((start, end));
        }

        // через 2 секунды: урон на линиях + ОДНОВРЕМЕННО cutter и кружки
        Timer.Spawn(TimeSpan.FromSeconds(2f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            Sound(comp.BeamSound ?? comp.VariationSound, ent);

            var freezeDamage = new DamageSpecifier();
            freezeDamage.DamageDict.TryAdd("Heat", comp.FreezeBeamDamage * 0.3f);
            freezeDamage.DamageDict.TryAdd("Blunt", comp.FreezeBeamDamage * 0.3f);

            // урон всем, кто на линии хотя бы одного замершего луча
            var candidates = _lookup.GetEntitiesInRange(new MapCoordinates(bossPos.Position, bossPos.MapId), 60f, LookupFlags.Uncontained);
            foreach (var victim in candidates)
            {
                if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                    continue;

                var victimPos = _transform.GetMapCoordinates(victim).Position;
                foreach (var (bs, be) in freezeBeams)
                {
                    if (DistanceToSegment(victimPos, bs, be) <= 1f)
                    {
                        _damage.TryChangeDamage(victim, freezeDamage, true);
                        break;
                    }
                }
            }

            // cutter на ВСЕХ игроков
            var everyone = GetTargets(ent, 100);
            foreach (var target in everyone)
            {
                if (TerminatingOrDeleted(target))
                    continue;
                RunCutter(ent, comp, target);
            }

            // кружки, чернеющие синхронно с Cutter
            for (var i = 0; i < comp.FreezeCircleCount; i++)
            {
                var angle = _random.NextFloat(0f, MathF.Tau);
                var dist = _random.NextFloat(5f, 38f);
                var pos = bossPos.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                var circle = Spawn("CelestialCircle", new MapCoordinates(pos, bossPos.MapId));
                if (TryComp<CelestialCircleComponent>(circle, out var circleComp))
                {
                    circleComp.Radius = _random.NextFloat(1.8f, 3.4f);
                    var dirAngle = _random.NextFloat(0f, MathF.Tau);
                    circleComp.WanderDir = new Vector2(MathF.Cos(dirAngle), MathF.Sin(dirAngle));
                    circleComp.WanderTime = comp.CutterRotateTime; // чернеют одновременно со вспышкой Cutter
                }
            }
        });

        // атака заканчивается после одновременного взрыва
        Timer.Spawn(TimeSpan.FromSeconds(2f + comp.CutterRotateTime + 0.4f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            comp.Attacking = false;
        });
    }

    /// <summary>
    /// Телепорт перед атакой: к случайной цели (вплотную) или в случайную
    /// точку рядом с игроками, с эффектом и звуком на выходе и входе.
    /// </summary>
    private void TeleportBoss(Entity<CelestialComponent> ent)
    {
        var targets = GetTargets(ent, 5);
        if (targets.Count == 0)
            return;

        var anchor = _random.Pick(targets);
        var anchorPos = _transform.GetMapCoordinates(anchor).Position;
        var oldPos = _transform.GetMapCoordinates(ent).Position;

        // 50% - вплотную к цели, 50% - случайная точка невдалеке от игроков
        var angle = _random.NextFloat(0f, MathF.Tau);
        var dist = _random.Prob(0.5f)
            ? _random.NextFloat(5f, 8f)
            : _random.NextFloat(15f, 30f);
        var dest = anchorPos + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        // эффекты на выходе и входе
        Spawn("CelestialTeleportEffect", new MapCoordinates(oldPos, _transform.GetMapCoordinates(ent).MapId));
        Spawn("CelestialTeleportEffect", new MapCoordinates(dest, _transform.GetMapCoordinates(anchor).MapId));

        if (TryComp<CelestialComponent>(ent, out var celestialComp) && celestialComp.TeleportSound != null)
            Sound(celestialComp.TeleportSound, ent);

        _transform.SetWorldPosition(ent, dest);

        // погасить инерцию после прыжка
        if (TryComp<PhysicsComponent>(ent, out var physics))
            _physics.SetLinearVelocity(ent, Vector2.Zero, body: physics);
    }

    // ------------------------------------------------------------------
    // Утилиты
    // ------------------------------------------------------------------

    private List<EntityUid> GetTargets(Entity<CelestialComponent> ent, int max)
    {
        var mapId = Transform(ent).MapID;
        var candidates = new List<EntityUid>();
        foreach (var session in _players.Sessions)
        {
            var mob = session.AttachedEntity;
            if (mob == null || TerminatingOrDeleted(mob.Value))
                continue;
            if (Transform(mob.Value).MapID != mapId)
                continue;
            if (!HasComp<MobStateComponent>(mob.Value))
                continue;
            // только живые, трупы не атакуем
            if (!_mobState.IsAlive(mob.Value))
                continue;
            candidates.Add(mob.Value);
        }

        _random.Shuffle(candidates);
        return candidates.Take(max).ToList();
    }

    private void Sound(SoundSpecifier? sound, EntityUid source)
    {
        if (sound == null)
            return;
        _globalSound.PlayGlobalOnStation(source, _audio.ResolveSound(sound),
            new AudioParams { Volume = -8f });
    }

}
