using Content.Server.Audio;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.DeadSpace.Lavaland.Celestial;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Server.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;

namespace Content.Server.DeadSpace.Lavaland.Celestial;

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
    [Dependency] private readonly IChatManager _chat = default!;

    private readonly HashSet<EntityUid> _nearbySpheres = new();

    private static readonly DamageSpecifier BeamDamage = MakeBeamDamage();

    // DS14: фразы, когда игрока добивает Селестиал
    private static readonly string[] KillPhrases =
    {
        "ВАША БОРЬБА ТЩЕТНА.",
        "ПРОЩАЙ.",
        "ПАЛ.",
        "ЕЩЁ ОДИН.",
        "СМЕРТНЫЙ.",
        "ХРУПОК.",
    };

    private static readonly SoundSpecifier KillSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Kill_Sound.ogg");

    // подложка субтитров: старт, повтор (пока висят), конец
    private const float DialogueIdleInterval = 2f;

    // голос Селестиала идёт на -8, поэтому подложка должна быть заметно тише
    private const float DialogueStartVolume = -13f;
    private const float DialogueIdleVolume = -10f;
    private const float DialogueEndVolume = -12f;

    // end стартует чуть раньше исчезновения: попадает в затухание субтитров
    // и не глохнет, если следом сразу начинаются следующие
    private const float DialogueEndLead = 1f;

    private static readonly SoundSpecifier DialogueStartSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Dialogue_Start.ogg");

    private static readonly SoundSpecifier DialogueIdleSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Dialogue_Idle.ogg");

    private static readonly SoundSpecifier DialogueEndSound =
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Dialogue_End.ogg");

    // чей сейчас диалог: старые таймеры подложки глохнут на новых субтитрах
    private int _dialogueToken;
    private EntityUid? _dialogueIdleSound;

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        if (lenSq < 0.0001f)
            return (p - a).Length();
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        return (p - (a + ab * t)).Length();
    }

    private static DamageSpecifier MakeBeamDamage()
    {
        var d = new DamageSpecifier();
        d.DamageDict.TryAdd("Heat", 32f);
        d.DamageDict.TryAdd("Blunt", 16f);
        d.DamageDict.TryAdd("Structural", 40f);
        return d;
    }

    private const float DestroyInterval = 0.25f;
    private const float DestroyRadius = 1.4f;

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

        // локальный вариант: и события, и объявления идут только на карту босса.
        // Запоминаем ДО таймеров: босса могут удалить, а объявление всё равно
        // не должно улетать на другие карты.
        var localized = comp.LocalizedEvents;
        var bossMap = Transform(ent).MapID;

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
                if (localized)
                {
                    // у локального варианта голос + субтитры, но только на его карте
                    SpeakVoice(ent, -8f);
                    SpeakFor(ent, phrase, CutscenePhraseDuration);
                }
                else
                    SpeakGlobal(phrase, CutscenePhraseDuration);

                // на последней фразе: особый звук и открытые глаза
                if (phrase == CutscenePhrases[^1])
                {
                    Announce(
                        new SoundPathSpecifier(
                            "/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_4.ogg"),
                        new AudioParams { Volume = -2f },
                        localized,
                        bossMap);
                    RaiseCelestial(ent, new CelestialSpiritStageEvent(1));
                }
            });
        }

        // после последней фразы: разрыв кокона
        var breachTime = CutsceneIntroTime + CutscenePhrases.Length * CutscenePhraseDuration;
        Timer.Spawn(TimeSpan.FromSeconds(breachTime), () =>
        {
            // катсцена клиентская: продолжается даже без босса,
            // локальные события при этом остаются на карте босса
            RaiseToBossMapOrAll(new CelestialSpiritStageEvent(2), localized, bossMap);
        });

        // конец катсцены: финальный крик
        var endTime = breachTime + 3f;
        Timer.Spawn(TimeSpan.FromSeconds(endTime), () =>
        {
            // финальный крик - без привязки к ентити (босс мог быть удалён)
            Announce(
                new SoundPathSpecifier(
                    "/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_screams_really_loudly.ogg"),
                new AudioParams { Volume = -4f },
                localized,
                bossMap);
            RaiseToBossMapOrAll(new CelestialCutsceneEndEvent(), localized, bossMap);

            if (Exists(ent) && !TerminatingOrDeleted(ent))
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
            if (!comp.Phase2 && TryComp<DamageableComponent>(uid, out var dmgCheck) && dmgCheck.TotalDamage >= comp.Phase2Damage)
            {
                comp.Phase2 = true;

                Sound(
                    new SoundPathSpecifier(
                        "/Audio/_DeadSpace/Lavaland/Celestial/sounds/Roar_Phase2.ogg"),
                    uid,
                    -4f);

                // крик "ВЫХОДА НЕТ" + атакует чаще и по большему числу людей
                SpeakFor((uid, comp), "ВЫХОДА НЕТ.", 4f);
                comp.ExtraTargets = comp.Phase2ExtraTargets;
            }

            // бессмертие: сбрасываем урон только если его реально нанесли
            if (comp.Phase2
                && comp.Phase2Immortal
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

            // радиус сферы по кривой роста (совпадает с клиентской отрисовкой),
            // радиус УРОНА отдельно меньше визуального
            var t = Math.Clamp(comp.Elapsed / comp.Lifetime, 0f, 1f);
            var radius = MathHelper.Lerp(comp.StartScale, comp.EndScale, MathF.Pow(t, comp.GrowthExponent))
                * comp.DamageRadiusScale;
            var sphereXform = Transform(uid);
            _nearbySpheres.Clear();
            _lookup.GetEntitiesInRange(sphereXform.Coordinates, radius, _nearbySpheres, LookupFlags.Uncontained);
            var sphereDmg = new DamageSpecifier();
            sphereDmg.DamageDict.TryAdd("Heat", comp.DamagePerTick * 0.5f);
            sphereDmg.DamageDict.TryAdd("Blunt", comp.DamagePerTick * 0.5f);
            foreach (var victim in _nearbySpheres)
            {
                // Its own attacks never hurt the Celestial.
                if (victim == uid || TerminatingOrDeleted(victim) || HasComp<CelestialComponent>(victim))
                    continue;
                Hurt(victim, sphereDmg);
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
            _damage.TryChangeDamage(candidate, damage);
        }
    }

    // ------------------------------------------------------------------
    // Речь: только крик атаки
    // ------------------------------------------------------------------

    private MapId? _deathCutsceneMap;

    /// <summary>Катсцена смерти: глобально или на карте убитого босса.</summary>
    public void BroadcastDeath(bool localized, MapId? bossMap)
    {
        _deathCutsceneMap = localized ? bossMap : null;

        RaiseToMapOrAll(new CelestialDeathEvent());
    }

    public void BroadcastDeathSpeak(string text, float duration)
    {
        RaiseToMapOrAll(new CelestialDeathSpeakEvent(text, duration));
    }

    public void BroadcastDeathInvert()
    {
        RaiseToMapOrAll(new CelestialDeathInvertEvent());
    }

    public void BroadcastDeathEnd()
    {
        RaiseToMapOrAll(new CelestialDeathEndEvent());
    }

    // DS14-start: секретный босс лаваленда - катсцена смерти только выбранным игрокам
    private static readonly string[] DeathPhrases =
    {
        "ТЫ ВЛАДЕЕШЬ ТЕМ, БЕЗ ЧЕГО ТОТ ЦВЕТОК СГНИЛ,",
        "СВЕТОМ, ЗАТМЕВАЮЩИМ САМ СВЕТ.",
        "КОРНЯМИ, ЧТО УХОДЯТ ГЛУБЖЕ САМОЙ БЕЗДНЫ.",
        "НО ЭТОМУ ПОРОЧНОМУ ФАРСУ НИКОГДА НЕ ДАТЬ ВСХОДОВ.",
        "ОСЛЕПЛЕННЫЙ СОБСТВЕННЫМ ЖЕ СВЕЧЕНИЕМ,",
        "ТЫ НЕ ВЕДАЕШЬ О СУДЬБЕ, ЧТО ЖДЕТ ТЕБЯ В ПУСТОТЕ ВНИЗУ.",
        "КОГДА ТВОИ ЛЕПЕСТКИ БУДУТ ПОГЛОЩЕНЫ ИЗНУТРИ,",
        "Я ПРЕБУДУ ЗДЕСЬ И ДАЛЬШЕ.",
        "НАБЛЮДАЯ.",
        "ВЫЖИДАЯ.",
    };

    /// <summary>Катсцена смерти для заданных игроков, даже если они сменят карту. Возвращает её длину.</summary>
    public TimeSpan PlayDeathCutscene(Filter recipients, float phraseDuration = 6f)
    {
        RaiseNetworkEvent(new CelestialDeathEvent(), recipients);
        for (var i = 0; i < DeathPhrases.Length; i++)
        {
            var phrase = DeathPhrases[i];
            Timer.Spawn(TimeSpan.FromSeconds(i * phraseDuration),
                () => RaiseNetworkEvent(new CelestialDeathSpeakEvent(phrase, phraseDuration), recipients));
        }

        var length = TimeSpan.FromSeconds(DeathPhrases.Length * phraseDuration);
        Timer.Spawn(length, () => RaiseNetworkEvent(new CelestialDeathEndEvent(), recipients));
        return length;
    }
    // DS14-end

    private void RaiseToMapOrAll(EntityEventArgs ev)
    {
        RaiseToBossMapOrAll(ev, _deathCutsceneMap != null, _deathCutsceneMap ?? MapId.Nullspace);
    }

    /// <summary>
    /// Событие только игрокам указанной карты (локальный вариант) либо всем.
    /// Не зависит от того, жив ли ещё ентити босса.
    /// </summary>
    private void RaiseToBossMapOrAll(EntityEventArgs ev, bool localized, MapId mapId)
    {
        if (!localized || mapId == MapId.Nullspace)
        {
            RaiseNetworkEvent(ev);
            return;
        }

        RaiseNetworkEvent(ev, Filter.BroadcastMap(mapId));
    }

    /// <summary>
    /// Announcement-звук (то, что игроки слышат как "объяву") - глобально
    /// или только игрокам карты босса у локального варианта.
    /// </summary>
    private void Announce(SoundSpecifier sound, AudioParams audioParams, bool localized, MapId mapId)
    {
        var filter = localized && mapId != MapId.Nullspace
            ? Filter.BroadcastMap(mapId)
            : Filter.Broadcast();

        _globalSound.PlayAnnonceGlobal(filter, _audio.ResolveSound(sound), audioParams);
    }

    private void Speak(string text, float duration, int style = 0)
    {
        RaiseNetworkEvent(new CelestialSpeakEvent(text, duration, style: style));
    }

    /// <summary>Фраза в палитре смерти: клиент сам рисует белые блоки и играет Celestial_Talk_5.</summary>
    private void SpeakDeath(string text, float duration)
    {
        RaiseNetworkEvent(new CelestialDeathSpeakEvent(text, duration));
    }

    // ------------------------------------------------------------------
    // Смерть игрока от Селестиала: фраза в чат, субтитры и звук
    // ------------------------------------------------------------------

    /// <summary>
    /// Урон от Селестиала: если после него игрок умер — кричим фразу в чат и субтитрами.
    /// </summary>
    private void Hurt(EntityUid victim, DamageSpecifier damage)
    {
        if (TerminatingOrDeleted(victim))
            return;

        var wasAlive = !_mobState.IsDead(victim);

        _damage.TryChangeDamage(victim, damage);

        if (!wasAlive || TerminatingOrDeleted(victim) || !_mobState.IsDead(victim))
            return;

        AnnounceKill(victim);
    }

    private void AnnounceKill(EntityUid victim)
    {
        // только игроки: у NPC нет сессии
        if (!_players.TryGetSessionByEntity(victim, out _))
            return;

        var boss = FindCelestial(victim);

        var localized = false;
        var mapId = MapId.Nullspace;
        if (boss is { } bossUid)
        {
            mapId = Transform(bossUid).MapID;
            localized = TryComp<CelestialComponent>(bossUid, out var bossComp) && bossComp.LocalizedEvents;
        }

        var phrase = _random.Pick(KillPhrases);

        // чат: чистая фраза, шрифт EuroStyle, цвет d05e8b
        var wrapped = $"[font=\"EuroStyle\"][color=#D05E8B]{phrase}[/color][/font]";
        var filter = localized && mapId != MapId.Nullspace
            ? Filter.BroadcastMap(mapId)
            : Filter.Broadcast();

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Server, phrase, wrapped,
            boss ?? victim, false, true, null);

        // субтитры
        if (boss is { } subtitleBoss && TryComp<CelestialComponent>(subtitleBoss, out var subtitleComp))
            RaiseCelestial((subtitleBoss, subtitleComp), new CelestialSpeakEvent(phrase, 4f));
        else
            RaiseNetworkEvent(new CelestialSpeakEvent(phrase, 4f));

        // звук фразы
        Announce(KillSound, new AudioParams { Volume = -2f }, localized, mapId);
    }

    /// <summary>Селестиал на карте жертвы: нужен для карты и локализации.</summary>
    private EntityUid? FindCelestial(EntityUid victim)
    {
        var mapId = Transform(victim).MapID;

        var query = EntityQueryEnumerator<CelestialComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (TerminatingOrDeleted(uid))
                continue;
            if (Transform(uid).MapID == mapId)
                return uid;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Подложка субтитров: старт / повтор / конец
    // ------------------------------------------------------------------

    /// <summary>
    /// Старт — когда субтитры появились, idle — повтор пока они стоят, end — когда исчезли.
    /// Новые субтитры обрывают предыдущую последовательность.
    /// </summary>
    private void PlayDialogueCues(EntityUid? source, float duration)
    {
        if (duration < 1f)
            return;

        // хвост предыдущей подложки не должен забивать новую реплику
        StopDialogueIdle();

        var token = ++_dialogueToken;
        PlayDialogueCue(source, DialogueStartSound, DialogueStartVolume);

        for (var at = DialogueIdleInterval; at < duration - DialogueEndLead; at += DialogueIdleInterval)
        {
            var delay = at;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (token != _dialogueToken)
                    return;
                _dialogueIdleSound = PlayDialogueCue(source, DialogueIdleSound, DialogueIdleVolume);
            });
        }

        var endAt = MathF.Max(0.1f, duration - DialogueEndLead);
        Timer.Spawn(TimeSpan.FromSeconds(endAt), () =>
        {
            if (token != _dialogueToken)
                return;

            // idle длится 4.7 с и перекрывал end: гасим перед звуком конца
            StopDialogueIdle();
            PlayDialogueCue(source, DialogueEndSound, DialogueEndVolume);
        });
    }

    private void StopDialogueIdle()
    {
        if (_dialogueIdleSound is not { } idle)
            return;

        _audio.Stop(idle);
        _dialogueIdleSound = null;
    }

    private EntityUid? PlayDialogueCue(EntityUid? source, SoundSpecifier sound, float volume)
    {
        var resolved = _audio.ResolveSound(sound);
        var filter = source is { } uid && Exists(uid) && !TerminatingOrDeleted(uid)
            ? Filter.BroadcastMap(Transform(uid).MapID)
            : Filter.Broadcast();

        var played = _audio.PlayGlobal(resolved, filter, false, new AudioParams { Volume = volume });
        return played?.Entity;
    }

    /// <summary>События босса: глобально или только на его карте.</summary>
    private void RaiseCelestial(Entity<CelestialComponent> ent, EntityEventArgs ev)
    {
        if (!Exists(ent) || TerminatingOrDeleted(ent) || !ent.Comp.LocalizedEvents)
        {
            RaiseNetworkEvent(ev);
            return;
        }

        RaiseNetworkEvent(ev, Filter.BroadcastMap(Transform(ent).MapID));
    }

    private void SpeakFor(Entity<CelestialComponent> ent, string text, float duration)
    {
        RaiseCelestial(ent, new CelestialSpeakEvent(text, duration));
        PlayDialogueCues(ent.Owner, duration);
    }

    private void RaiseVisual(MapId mapId, EntityEventArgs ev, bool localized)
    {
        if (!localized)
        {
            RaiseNetworkEvent(ev);
            return;
        }

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || TerminatingOrDeleted(player))
                continue;
            if (Transform(player).MapID != mapId)
                continue;
            RaiseNetworkEvent(ev, session.Channel);
        }
    }

    /// <summary>
    /// Публичный показ субтитров со звуком (для консольной команды).
    /// </summary>
    /// <param name="mode">
    /// 0 - обычный розовый текст и звук Celestial_Talk_1..3;
    /// 1 - обычный текст и звук Celestial_Talk_6..8;
    /// 2 - палитра смерти (белые блоки/чёрные буквы) и Celestial_Talk_5, как в катсцене killcelestial;
    /// 3 - красные буквы и звук Level100Start_Final.
    /// </param>
    public void SpeakGlobal(string text, float duration, int mode = 0)
    {
        var clamped = Math.Clamp(mode, 0, 3);

        // режим 2 - это штатное событие смерти: клиент сам рисует палитру смерти
        // и сам проигрывает Celestial_Talk_5, поэтому серверный звук тут не нужен
        if (clamped == 2)
        {
            SpeakDeath(text, duration);
            return;
        }

        var sounds = clamped == 1 ? _talk6Sounds : clamped == 3 ? _specialSounds : _talkSounds;
        if (sounds.Count > 0)
        {
            _globalSound.PlayAnnonceGlobal(Filter.Broadcast(),
                _audio.ResolveSound(_random.Pick(sounds)),
                new AudioParams { Volume = -8f });
        }

        Speak(text, duration, clamped == 3 ? 3 : 0);
        PlayDialogueCues(null, duration);
    }

    private readonly List<SoundSpecifier> _talkSounds = new()
    {
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_1.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_2.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_3.ogg"),
    };

    private readonly List<SoundSpecifier> _talk6Sounds = new()
    {
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_6.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_7.ogg"),
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Celestial_Talk_8.ogg"),
    };

    private readonly List<SoundSpecifier> _specialSounds = new()
    {
        new SoundPathSpecifier("/Audio/_DeadSpace/Lavaland/Celestial/sounds/Level100Start_Final.ogg"),
    };

    private void SpeakVoice(Entity<CelestialComponent> ent, float volume)
    {
        var comp = ent.Comp;
        if (comp.TalkSounds.Count == 0)
            return;

        var sound = _audio.ResolveSound(_random.Pick(comp.TalkSounds));
        var audioParams = new AudioParams { Volume = volume };

        if (comp.LocalizedEvents && Exists(ent) && !TerminatingOrDeleted(ent))
        {
            _audio.PlayGlobal(sound, Filter.BroadcastMap(Transform(ent).MapID), false, audioParams);
            return;
        }

        _globalSound.PlayGlobalOnStation(ent, sound, audioParams);
    }

    private void SpeakAttack(Entity<CelestialComponent> ent, CelestialComponent comp)
    {
        SpeakVoice(ent, comp.AttackVolume);
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
        var targets = GetSpacedTargets(ent, beamCount + comp.ExtraTargets);
        foreach (var target in targets)
        {
            FireBeamsAround(ent, comp, target, beamCount, telegraph);
        }
    }

    private void FinalVolley(Entity<CelestialComponent> ent, CelestialComponent comp)
    {
        var targets = GetSpacedTargets(ent, 1 + comp.ExtraTargets);
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

            RaiseCelestial(ent, new CelestialBeamVisualEvent(
                startPos, end, (int)targetPos.MapId, comp.BeamChargeTime, comp.DarkBeamDuration));

            var strikeMap = new MapCoordinates(aimPos, targetPos.MapId);

            Timer.Spawn(TimeSpan.FromSeconds(comp.BeamChargeTime), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                Sound(comp.BeamSound ?? comp.VariationSound, ent);

                // урон всем, кто стоит НА ЛИНИИ луча (радиус - BeamDamageRadius)
                var beamMid = (beamStart + beamEnd) / 2f;
                var beamHalfLen = (beamEnd - beamStart).Length() / 2f + comp.BeamDamageRadius;
                var beamMidMap = new MapCoordinates(beamMid, strikeMap.MapId);
                _nearbySpheres.Clear();
                foreach (var victim in _lookup.GetEntitiesInRange(beamMidMap, beamHalfLen, LookupFlags.Uncontained))
                {
                    if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                        continue;

                    var victimPos = _transform.GetMapCoordinates(victim).Position;
                    if (DistanceToSegment(victimPos, beamStart, beamEnd) > comp.BeamDamageRadius)
                        continue;

                    Hurt(victim, BeamDamage);
                }

                // ломаем пол под ударом
                if (!comp.BreakTiles)
                    return;
                foreach (var gridEnt in _mapManager.GetAllGrids(strikeMap.MapId))
                {
                    foreach (var tile in _map.GetTilesIntersecting(gridEnt.Owner, gridEnt.Comp, new Circle(strikeMap.Position, 0.6f)))
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
        SpeakVoice(ent, -8f);

        SpeakFor(ent, "ТЩЕТНО.", 4f);

        // строго по одному шару: следующий выходит из нового портала только
        // после того, как пропал предыдущий (2-й и дальше — из портала прямо
        // перед игроком)
        foreach (var target in targets)
            SpawnFutilityOrb(ent, comp, target, 0);

        var afterOrbs = comp.FutilityOrbPhaseTime;

        // Cutter по тем, кто НЕ был целью (с рандомным смещением), но не больше CutterTargets игроков
        var cutterPool = GetTargets(ent, 1 + comp.ExtraTargets + comp.FutilityTargets);
        var nonTargets = cutterPool.Where(t => !targets.Contains(t)).Take(comp.CutterTargets).ToList();
        for (var r = 0; r < comp.CutterRepeats; r++)
        {
            var delay = afterOrbs + r * comp.CutterRepeatDelay;
            var isFinalCutter = r == comp.CutterRepeats - 1;
            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;
                // во время каттера она ничего не говорит
                foreach (var target in nonTargets)
                {
                    if (TerminatingOrDeleted(target))
                        continue;
                    RunCutter(ent, comp, target, final: isFinalCutter);
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

    /// <summary>
    /// Один шар из свежего портала. Первый портал открывается рядом с целью,
    /// второй и следующие — прямо перед игроком. Следующий шар запускается
    /// только после исчезновения текущего.
    /// </summary>
    private void SpawnFutilityOrb(Entity<CelestialComponent> ent, CelestialComponent comp,
        EntityUid target, int index)
    {
        if (index >= comp.FutilityOrbCount)
            return;

        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target))
            return;

        var targetPos = _transform.GetMapCoordinates(target);
        var riftCenter = GetFutilityRiftCenter(target, comp, index);
        var riftMap = new MapCoordinates(riftCenter, targetPos.MapId);

        // рифт: текстура телепорта + звук появления
        Spawn("CelestialRift", riftMap);
        Sound(comp.FutilityCrackSound, ent, -4f);

        // прицеливание: пока портал заряжается, от него к цели идёт тонкая линия
        RaiseVisual(targetPos.MapId, new CelestialBeamVisualEvent(
            riftCenter, targetPos.Position, (int) targetPos.MapId,
            comp.FutilityAimTime, 0.05f, 0.35f), comp.LocalizedEvents);

        Timer.Spawn(TimeSpan.FromSeconds(comp.FutilityAimTime), () =>
        {
            if (TerminatingOrDeleted(target) || TerminatingOrDeleted(ent))
                return;

            if (comp.FutilityOrbSounds.Count > 0)
                Sound(_random.Pick(comp.FutilityOrbSounds), ent, -8f);

            var toTarget = _transform.GetMapCoordinates(target).Position - riftCenter;
            var dist = MathF.Max(toTarget.Length(), 0.01f);

            var orb = Spawn("CelestialOrb", riftMap);
            var orbComp = EnsureComp<CelestialOrbComponent>(orb);
            orbComp.Target = target;
            orbComp.Phase = _random.NextFloat(0f, MathF.Tau);
            orbComp.Lifetime = comp.OrbLifetime;
            orbComp.Speed = comp.OrbSpeed;
            orbComp.TurnRate = comp.OrbTurnRate; // неповоротливость: шар тяжело доворачивает
            orbComp.FireInterval = comp.OrbFireInterval;
            orbComp.BeamDamageMin = comp.SmallBeamMinDamage;
            orbComp.BeamDamageMax = comp.SmallBeamMaxDamage;
            orbComp.BeamRadius = comp.SmallBeamRadius;
            orbComp.LocalizedEvents = comp.LocalizedEvents;
            orbComp.VelocityDir = toTarget / dist;

            // глитч-полёт: ~16 рывков по 0.125 с — около двух секунд до игрока,
            // каждый рывок уводит шар вбок на случайное смещение
            orbComp.TeleportInterval = comp.FutilityJumpInterval;
            orbComp.TeleportDist = dist / 16f;
            orbComp.TeleportJitter = comp.FutilityJumpJitter;
            orbComp.JumpsLeft = 16;

            WaitFutilityOrbGone(ent, comp, target, orb, index + 1);
        });
    }

    /// <summary>Ждёт, пока шар пропадёт, и открывает портал для следующего.</summary>
    private void WaitFutilityOrbGone(Entity<CelestialComponent> ent, CelestialComponent comp,
        EntityUid target, EntityUid orb, int nextIndex)
    {
        Timer.Spawn(TimeSpan.FromSeconds(0.2f), () =>
        {
            if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target))
                return;

            if (Exists(orb) && !TerminatingOrDeleted(orb))
            {
                WaitFutilityOrbGone(ent, comp, target, orb, nextIndex);
                return;
            }

            SpawnFutilityOrb(ent, comp, target, nextIndex);
        });
    }

    /// <summary>Точка портала: первый — рядом с целью, остальные — перед игроком по взгляду.</summary>
    private Vector2 GetFutilityRiftCenter(EntityUid target, CelestialComponent comp, int index)
    {
        var targetPos = _transform.GetMapCoordinates(target).Position;

        if (index <= 0)
        {
            var riftAngle = _random.NextFloat(0f, MathF.Tau);
            var riftDist = _random.NextFloat(comp.RiftMinDistance, MathF.Max(comp.RiftMinDistance, comp.RiftMaxDistance));
            return targetPos + new Vector2(MathF.Cos(riftAngle), MathF.Sin(riftAngle)) * riftDist;
        }

        // портал открывается прямо перед игроком, по направлению взгляда
        var facing = _transform.GetWorldRotation(target).ToVec();
        if (facing.LengthSquared() < 0.01f)
        {
            var fallbackAngle = _random.NextFloat(0f, MathF.Tau);
            facing = new Vector2(MathF.Cos(fallbackAngle), MathF.Sin(fallbackAngle));
        }

        return targetPos + Vector2.Normalize(facing) * comp.FutilityPortalDistance;
    }

    private void OrbContact(EntityUid uid, CelestialOrbComponent orb, Vector2 at)
    {
        var mapId = _transform.GetMapCoordinates(uid).MapId;
        var damage = new DamageSpecifier();
        damage.DamageDict.TryAdd("Heat", orb.ContactDamage * 0.5f);
        damage.DamageDict.TryAdd("Blunt", orb.ContactDamage * 0.5f);

        RaiseVisual(mapId, new CelestialBeamVisualEvent(at - new Vector2(orb.ContactRadius, 0f),
            at + new Vector2(orb.ContactRadius, 0f), (int) mapId, 0.2f, 0.3f, 0.9f), orb.LocalizedEvents);

        foreach (var victim in _lookup.GetEntitiesInRange(new MapCoordinates(at, mapId), orb.ContactRadius, LookupFlags.Uncontained))
        {
            if (TerminatingOrDeleted(victim) || HasComp<CelestialComponent>(victim) || victim == uid)
                continue;
            Hurt(victim, damage);
        }
    }

    private void FireSmallBeam(Vector2 from, MapId mapId, CelestialOrbComponent orb)
    {
        var angle = _random.NextFloat(0f, MathF.Tau);
        var length = _random.NextFloat(4f, 7f);
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var start = from;
        var end = from + dir * length;

        // тонкий луч стоит: короткая розовая фаза, затем чёрный
        RaiseVisual(mapId, new CelestialBeamVisualEvent(start, end, (int)mapId, 0.35f, 0.45f, 0.4f), orb.LocalizedEvents);

        // мелкий урон всем на линии
        var damage = new DamageSpecifier();
        var amount = _random.NextFloat(orb.BeamDamageMin, orb.BeamDamageMax);
        damage.DamageDict.TryAdd("Heat", amount * 0.5f);
        damage.DamageDict.TryAdd("Blunt", amount * 0.5f);

        var mid = (start + end) / 2f;
        var halfLen = length / 2f + orb.BeamRadius;
        foreach (var victim in _lookup.GetEntitiesInRange(new MapCoordinates(mid, mapId), halfLen, LookupFlags.Uncontained))
        {
            if (TerminatingOrDeleted(victim) || HasComp<CelestialComponent>(victim))
                continue;
            if (DistanceToSegment(_transform.GetMapCoordinates(victim).Position, start, end) > orb.BeamRadius)
                continue;
            Hurt(victim, damage);
        }
    }

    // ------------------------------------------------------------------
    // Cutter: 8 лучей, вращаются вокруг центра и вспыхивают
    // ------------------------------------------------------------------

    private void RunCutter(Entity<CelestialComponent> ent, CelestialComponent comp, EntityUid target, bool final = false)
    {
        Sound(comp.CutterChargeSound, ent, -4f);

        var bossPos = _transform.GetMapCoordinates(ent);
        var targetPos = _transform.GetMapCoordinates(target);

        // Cutter разворачивается ПРЯМО У КУЧИ игроков
        var offsetAngle = _random.NextFloat(0f, MathF.Tau);
        var offsetDist = _random.NextFloat(3f, 6f);
        var center = targetPos.Position +
            new Vector2(MathF.Cos(offsetAngle) * offsetDist, MathF.Sin(offsetAngle) * offsetDist);
        var baseAngle = _random.NextFloat(0f, MathF.Tau);

        RaiseCelestial(ent, new CelestialCutterEvent(
            center, (int)bossPos.MapId, baseAngle, comp.CutterLength,
            comp.CutterRotateTime, comp.CutterFireTime));

        // урон в момент вспышки: все на линиях лучей
        Timer.Spawn(TimeSpan.FromSeconds(comp.CutterRotateTime), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;

            if (final && comp.CutterFinalSound != null)
                Sound(comp.CutterFinalSound, ent, -2f);
            else if (comp.CutterImpactSound != null)
                Sound(comp.CutterImpactSound, ent, -4f);

            var damage = new DamageSpecifier();
            damage.DamageDict.TryAdd("Heat", comp.CutterDamage * 0.6f);
            damage.DamageDict.TryAdd("Blunt", comp.CutterDamage * 0.4f);

            var halfLen = comp.CutterLength / 2f + comp.CutterDamageRadius;
            _nearbySpheres.Clear();
            foreach (var victim in _lookup.GetEntitiesInRange(new MapCoordinates(center, bossPos.MapId), halfLen, LookupFlags.Uncontained))
            {
                if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                    continue;

                var victimPos = _transform.GetMapCoordinates(victim).Position;
                var victimRel = victimPos - center;

                // у самого центра бьёт гарантированно
                if (victimRel.Length() <= comp.CutterDamageRadius * 2f)
                {
                    Hurt(victim, damage);
                    continue;
                }

                // 8 лучей-отрезков из центра (4 линии, противоположные пары)
                var hit = false;
                for (var k = 0; k < 8; k++)
                {
                    var ang = baseAngle + k * MathF.PI / 4f;
                    var rayEnd = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * (comp.CutterLength / 2f);
                    if (DistanceToSegment(victimPos, center, rayEnd) <= comp.CutterDamageRadius)
                    {
                        hit = true;
                        break;
                    }
                }

                if (hit)
                    Hurt(victim, damage);
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
                // догнал - взрывается на цели и всех рядом
                OrbContact(uid, orb, orbPos);
                QueueDel(uid);
                continue;
            }

            // шар не наводится в полёте: направление зафиксировано в портале,
            // дальше только глитч-рывки вдоль него с боковым разбросом
            orb.TeleportAccumulator += frameTime;
            if (orb.TeleportAccumulator < orb.TeleportInterval)
                continue;

            orb.TeleportAccumulator -= orb.TeleportInterval;

            if (!CanMove(uid))
                continue;

            var jumpDir = orb.VelocityDir.LengthSquared() < 0.001f
                ? toTarget / dist
                : orb.VelocityDir;

            var perpendicular = new Vector2(-jumpDir.Y, jumpDir.X);
            var jitter = perpendicular * _random.NextFloat(-orb.TeleportJitter, orb.TeleportJitter);
            _transform.SetWorldPosition(uid, orbPos + jumpDir * orb.TeleportDist + jitter);

            // рывки кончились — рвётся там, куда был нацелен
            orb.JumpsLeft--;
            if (orb.JumpsLeft <= 0)
            {
                OrbContact(uid, orb, _transform.GetMapCoordinates(uid).Position);
                QueueDel(uid);
                continue;
            }

            // стрельба мелкими лучами вдоль пути
            orb.FireAccumulator += frameTime;
            if (orb.FireAccumulator >= orb.FireInterval)
            {
                orb.FireAccumulator -= orb.FireInterval;
                FireSmallBeam(orbPos, _transform.GetMapCoordinates(uid).MapId, orb);
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
        SpeakVoice(ent, -8f);

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
                circleComp.Radius = _random.NextFloat(1.5f, 2.8f);
                circleComp.DamageRadiusScale = comp.CircleDamageRadiusScale;
                var dirAngle = _random.NextFloat(0f, MathF.Tau);
                circleComp.WanderDir = new Vector2(MathF.Cos(dirAngle), MathF.Sin(dirAngle));
                Dirty(circle, circleComp);
            }
        }

        // после дрейфа и чёрной фазы - сферы, затем Cutter по CutterTargets игрокам
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

                // Cutter только на нескольких игроков
                var cutterTargets = GetSpacedTargets(ent, comp.CutterTargets);
                for (var r = 0; r < comp.CutterRepeats; r++)
                {
                    var delay = r * comp.CutterRepeatDelay;
                    var isFinalCutter = r == comp.CutterRepeats - 1;
                    Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
                    {
                        if (TerminatingOrDeleted(ent))
                            return;
                        foreach (var target in cutterTargets)
                        {
                            if (TerminatingOrDeleted(target))
                                continue;
                            RunCutter(ent, comp, target, final: isFinalCutter);
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

                if (!CanMove(uid))
                    continue;

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

                var radius = circle.Radius * circle.DamageRadiusScale;
                var xform = Transform(uid);
                _nearbySpheres.Clear();
                _lookup.GetEntitiesInRange(xform.Coordinates, radius, _nearbySpheres, LookupFlags.Uncontained);
                var dmg = new DamageSpecifier();
                dmg.DamageDict.TryAdd("Heat", circle.DamagePerTick * 0.5f);
                dmg.DamageDict.TryAdd("Blunt", circle.DamagePerTick * 0.5f);
                foreach (var victim in _nearbySpheres)
                {
                    if (victim == uid || TerminatingOrDeleted(victim) || HasComp<CelestialComponent>(victim))
                        continue;
                    Hurt(victim, dmg);
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
        var targets = GetSpacedTargets(ent, comp.SphereTargets + comp.ExtraTargets);
        foreach (var target in targets)
        {
            var count = (int)MathF.Round(_random.Next(comp.SpheresPerTargetMin, comp.SpheresPerTargetMax + 1) * multiplier);
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
                    sphereComp.StartScale = _random.NextFloat(0.25f, 0.5f);
                    sphereComp.EndScale = _random.NextFloat(1.1f, 1.9f);
                    sphereComp.DamageRadiusScale = comp.SphereDamageRadiusScale;
                    sphereComp.Lifetime = _random.NextFloat(comp.SphereLifetimeMin, comp.SphereLifetimeMax);
                    Dirty(sphere, sphereComp);
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

        SpeakVoice(ent, -8f);

        SpeakFor(ent, comp.FlashLine, 3f);
        SummonSpheres(ent, comp, comp.FlashSphereMultiplier);

        // как только сферы отжили - СРАЗУ УВЯНЬ В ЦВЕТУ
        Timer.Spawn(TimeSpan.FromSeconds(comp.SphereLifetimeMax + 0.7f), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            StartBloom(ent, comp);
        });
    }

    // ------------------------------------------------------------------
    // Шестая атака: УВЯНЬ В ЦВЕТУ
    // ------------------------------------------------------------------

    private void StartBloom(Entity<CelestialComponent> ent, CelestialComponent? compOverride = null)
    {
        var comp = compOverride ?? ent.Comp;
        comp.Attacking = true;

        SpeakVoice(ent, -8f);

        SpeakFor(ent, comp.BloomLine, 4f);

        Sound(comp.BloomChargeSound, ent, -6f);

        // крест из 4 очень длинных лучей вокруг тела Селестиала
        var center = _transform.GetMapCoordinates(ent).Position;
        var mapId = Transform(ent).MapID;
        var baseAngle = _random.NextFloat(0f, MathF.Tau);

        RaiseCelestial(ent, new CelestialBloomEvent(
            center, (int) mapId, baseAngle, comp.BloomLength,
            comp.BloomRotateSpeed, comp.BloomSweepTime + comp.BloomStageGap,
            comp.BloomFireTime, comp.BloomStageGap));

        // звук активной стадии - отдельным таймером, чтобы не потерять
        Timer.Spawn(TimeSpan.FromSeconds(comp.BloomSweepTime + comp.BloomStageGap), () =>
        {
            if (TerminatingOrDeleted(ent))
                return;
            Sound(comp.BloomFireSound, ent, -4f);
        });

        // стадия 2: чёрные лучи наносят урон, продолжая вращаться
        var damageTicks = (int) (comp.BloomFireTime / comp.BloomDamageInterval);
        for (var tick = 1; tick <= damageTicks; tick++)
        {
            var delay = comp.BloomSweepTime + comp.BloomStageGap + (tick - 1) * comp.BloomDamageInterval;

            // угол считаем ДО таймера: переменная цикла захватывается лямбдой
            // по ссылке, и внутри таймера tick всегда равен damageTicks + 1 —
            // урон уходил в одну повёрнутую сторону, а не по видимым лучам
            var tickElapsed = (tick - 1) * comp.BloomDamageInterval;
            var tickAngle = baseAngle + comp.BloomRotateSpeed * (comp.BloomSweepTime + tickElapsed);

            Timer.Spawn(TimeSpan.FromSeconds(delay), () =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                var bloomDamage = new DamageSpecifier();
                bloomDamage.DamageDict.TryAdd("Heat", comp.BloomBeamDamage);

                // радиус запроса = вся длина луча (плюс запас)
                var queryRadius = comp.BloomLength + 4f;
                _nearbySpheres.Clear();
                foreach (var victim in _lookup.GetEntitiesInRange(
                    new MapCoordinates(center, mapId), queryRadius, LookupFlags.Uncontained))
                {
                    if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                        continue;

                    var victimPos = _transform.GetMapCoordinates(victim).Position;

                    // 4 луча по 90 градусов
                    for (var k = 0; k < 4; k++)
                    {
                        var ang = tickAngle + k * MathF.PI / 2f;
                        var rayEnd = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * comp.BloomLength;
                        if (DistanceToSegment(victimPos, center, rayEnd) <= comp.BloomDamageRadius)
                        {
                            Hurt(victim, bloomDamage);
                            break;
                        }
                    }
                }
            });
        }

        // атака заканчивается после обеих стадий
        Timer.Spawn(TimeSpan.FromSeconds(comp.BloomSweepTime + comp.BloomStageGap + comp.BloomFireTime + 0.5f), () =>
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

        SpeakVoice(ent, -8f);

        SpeakFor(ent, comp.FreezeLine, 3f);

        // одна атака на ГРУППУ игроков, стоящих рядом друг с другом
        var groupAnchor = GetTargets(ent, 1).FirstOrDefault();
        if (groupAnchor == default)
            return;

        var groupCenter = _transform.GetMapCoordinates(groupAnchor);
        var groupMembers = new List<EntityUid> { groupAnchor };
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || TerminatingOrDeleted(player))
                continue;
            if (player == groupAnchor)
                continue;
            if (Transform(player).MapID != groupCenter.MapId)
                continue;
            if (!HasComp<MobStateComponent>(player) || _mobState.IsDead(player))
                continue;
            if ((_transform.GetMapCoordinates(player).Position - groupCenter.Position).Length() <= 10f)
                groupMembers.Add(player);
        }

        var bossPos = groupCenter;
        var freezeBeams = new List<(Vector2 start, Vector2 end)>();

        void AddFreezeBeam(Vector2 center)
        {
            var beamAngle = _random.NextFloat(0f, MathF.Tau);
            var length = _random.NextFloat(comp.FreezeBeamMinLength, MathF.Max(comp.FreezeBeamMinLength, comp.FreezeBeamMaxLength));
            var dir = new Vector2(MathF.Cos(beamAngle), MathF.Sin(beamAngle));
            var start = center - dir * (length / 2f);
            var end = center + dir * (length / 2f);

            // 2 секунды розовые (замерли), затем чернеют
            RaiseCelestial(ent, new CelestialBeamVisualEvent(start, end, (int)bossPos.MapId, 2f, 1.5f));
            freezeBeams.Add((start, end));
        }

        // длинные лучи прямо через каждого игрока: от них надо уходить
        foreach (var target in GetTargets(ent, int.MaxValue))
        {
            var targetPos = _transform.GetMapCoordinates(target).Position;
            for (var i = 0; i < comp.FreezeBeamsPerTarget; i++)
                AddFreezeBeam(targetPos + _random.NextVector2(comp.FreezeBeamAimSpread));
        }

        // и ОЧЕНЬ много по всей округе
        for (var i = freezeBeams.Count; i < comp.FreezeBeamCount; i++)
        {
            var angle = _random.NextFloat(0f, MathF.Tau);
            var dist = _random.NextFloat(4f, 40f);
            AddFreezeBeam(bossPos.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist));
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
            var candidates = _lookup.GetEntitiesInRange(new MapCoordinates(bossPos.Position, bossPos.MapId), 60f + comp.FreezeBeamMaxLength, LookupFlags.Uncontained);
            foreach (var victim in candidates)
            {
                if (HasComp<CelestialComponent>(victim) || TerminatingOrDeleted(victim))
                    continue;

                var victimPos = _transform.GetMapCoordinates(victim).Position;
                foreach (var (bs, be) in freezeBeams)
                {
                    if (DistanceToSegment(victimPos, bs, be) <= comp.FreezeBeamRadius)
                    {
                        Hurt(victim, freezeDamage);
                        break;
                    }
                }
            }

            // cutter + кружки повторяются CutterRepeats раз, как и в остальных атаках
            for (var r = 0; r < comp.CutterRepeats; r++)
            {
                var final = r == comp.CutterRepeats - 1;
                Timer.Spawn(TimeSpan.FromSeconds(r * comp.CutterRepeatDelay), () =>
                {
                    if (TerminatingOrDeleted(ent))
                        return;

                    // cutter по группе игроков рядом (центр группы)
                    var cutterTargets = groupMembers;
                    foreach (var target in cutterTargets)
                    {
                        if (TerminatingOrDeleted(target))
                            continue;
                        RunCutter(ent, comp, target, final);
                    }

                    // кружки, чернеющие синхронно с этим Cutter
                    for (var i = 0; i < comp.FreezeCircleCount; i++)
                    {
                        var angle = _random.NextFloat(0f, MathF.Tau);
                        var dist = _random.NextFloat(5f, 38f);
                        var pos = bossPos.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
                        var circle = Spawn("CelestialCircle", new MapCoordinates(pos, bossPos.MapId));
                        if (TryComp<CelestialCircleComponent>(circle, out var circleComp))
                        {
                            circleComp.Radius = _random.NextFloat(1.5f, 2.8f);
                            circleComp.DamageRadiusScale = comp.CircleDamageRadiusScale;
                            var dirAngle = _random.NextFloat(0f, MathF.Tau);
                            circleComp.WanderDir = new Vector2(MathF.Cos(dirAngle), MathF.Sin(dirAngle));
                            circleComp.WanderTime = comp.CutterRotateTime; // чернеют одновременно со вспышкой Cutter
                            Dirty(circle, circleComp);
                        }
                    }
                });
            }
        });

        // атака заканчивается после последнего повтора
        var freezeEnd = 2f + MathF.Max(0, comp.CutterRepeats - 1) * comp.CutterRepeatDelay
            + comp.CutterRotateTime + 0.4f;
        Timer.Spawn(TimeSpan.FromSeconds(freezeEnd), () =>
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
        if (!CanMove(ent) || ent.Comp.Stationary)
            return;

        var targets = GetTargets(ent, 5);
        if (targets.Count == 0)
            return;

        var anchor = _random.Pick(targets);
        var anchorPos = _transform.GetMapCoordinates(anchor).Position;
        var oldPos = _transform.GetMapCoordinates(ent).Position;

        // всегда рядом с игроком и никогда не очень далеко
        var angle = _random.NextFloat(0f, MathF.Tau);
        var dist = _random.NextFloat(4f, 7f);
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

    /// <summary>
    /// SetWorldPosition бросает DebugTools.Assert, если у сущности нет валидного
    /// parent либо она не привязана к мапе (nullspace / root node). Такое бывает
    /// у сущностей, уже уходящих на удаление, и у сущностей, заспавненных вне мапы.
    /// </summary>
    private bool CanMove(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var xform = Transform(uid);
        return xform.ParentUid.IsValid() && xform.MapUid != null;
    }

    /// <summary>
    /// Цели, разнесённые по карте: каждая следующая - ДАЛЬШЕ предыдущих,
    /// чтобы атаки не складывались в одну толпу.
    /// </summary>
    private List<EntityUid> GetSpacedTargets(Entity<CelestialComponent> ent, int max, float minSeparation = 14f)
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
            if (!HasComp<MobStateComponent>(mob.Value) || _mobState.IsDead(mob.Value))
                continue;
            candidates.Add(mob.Value);
        }

        var picked = new List<EntityUid>();
        if (candidates.Count == 0)
            return picked;

        _random.Shuffle(candidates);
        picked.Add(candidates[0]);

        for (var i = 1; i < max; i++)
        {
            EntityUid? best = null;
            var bestDist = -1f;
            foreach (var c in candidates)
            {
                if (picked.Contains(c))
                    continue;
                var cPos = _transform.GetMapCoordinates(c).Position;
                var minToPicked = float.MaxValue;
                foreach (var p in picked)
                    minToPicked = MathF.Min(minToPicked, (cPos - _transform.GetMapCoordinates(p).Position).Length());

                if (minToPicked > bestDist)
                {
                    bestDist = minToPicked;
                    best = c;
                }
            }

            if (best == null)
                break;

            picked.Add(best.Value);
        }

        return picked;
    }

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
            // живые и лежащие в крите; трупы не атакуем
            if (_mobState.IsDead(mob.Value))
                continue;
            candidates.Add(mob.Value);
        }

        _random.Shuffle(candidates);
        return candidates.Take(max).ToList();
    }

    /// <summary>
    /// Звук босса/его снарядов. У локального варианта (<see cref="CelestialComponent.LocalizedEvents"/>)
    /// он уходит ТОЛЬКО игрокам карты босса, у глобального - как раньше, через станцию и ПВС.
    /// </summary>
    private void Sound(SoundSpecifier? sound, EntityUid source, float volume = -8f)
    {
        if (sound == null)
            return;

        var resolved = _audio.ResolveSound(sound);
        var audioParams = new AudioParams { Volume = volume };

        if (TryComp<CelestialComponent>(source, out var comp) && comp.LocalizedEvents)
        {
            _audio.PlayGlobal(resolved, Filter.BroadcastMap(Transform(source).MapID), false, audioParams);
            return;
        }

        _globalSound.PlayGlobalOnStation(source, resolved, audioParams);
    }

}
