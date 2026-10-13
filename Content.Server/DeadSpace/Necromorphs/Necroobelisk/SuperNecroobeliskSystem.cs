// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Robust.Shared.Player;
using Content.Shared.Verbs;
using Content.Server.Administration.Managers;
using Robust.Shared.Utility;
using Content.Shared.Database;
using Robust.Shared.Timing;
using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Server.Beam;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Station.Systems;
using Content.Shared.Audio;
using Content.Server.RoundEnd;
using Content.Server.DeadSpace.Necromorphs.Unitology;
using Content.Shared.Damage.Components;
using Robust.Shared.Audio.Systems;
using Content.Server.Power.Components;
using Content.Server.Anomaly;
using Robust.Shared.Map;
using Robust.Shared.Random;
using System.Numerics;
using Content.Server.GameTicking;
using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Examine;
using Content.Server.DeadSpace.Necromorphs.NecroWall.Components;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Zombies;

namespace Content.Server.DeadSpace.Necromorphs.Necroobelisk;

public sealed class SuperNecroobeliskSystem : SharedSuperNecroobeliskSystem
{
    private const string ActivationAnnouncement = "unitology-centcomm-announcement-supermatter-obelisk-activated";
    private const string ActivationAnnouncementSender = "Автоматические Системы Станции";
    private const string ActivationAnnouncementVoice = "Glados";

    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly AnomalySystem _anomaly = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, GetVerbsEvent<Verb>>(DoSetObeliskVerbs);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, SanityLostEvent>(OnSanityLost);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, NecroobeliskPulseEvent>(OnSeverityChanged);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, NecroobeliskStartConvergenceEvent>(OnConvergence);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, NecroobeliskAbsorbEvent>(DoAbsorb);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, NecroMoonAppearanceEvent>(DoAppearanceMoon);
        SubscribeLocalEvent<SuperMatterialNecroObeliskComponent, ExaminedEvent>(OnExaminedEvent);
    }
    private void OnExaminedEvent(EntityUid uid, SuperMatterialNecroObeliskComponent component, ExaminedEvent args)
    {
        if (component.Percents > 0) args.PushMarkup("Процесс активации обелиска: " + component.Percents.ToString() + "%");
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SuperMatterialNecroObeliskComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.SequenceStarted) continue;

            if (component.NextCheckPercents == TimeSpan.Zero) component.NextCheckPercents = _gameTiming.CurTime + component.CheckTime;

            if (_gameTiming.CurTime < component.NextCheckPercents) continue;
            //   - type: PowerSupplier
            //     supplyRate: 10000 # 10 kWt
            component.Percents += 1;
            component.NextCheckPercents = _gameTiming.CurTime + component.CheckTime;

            if (component.Percents > 0 && component.StateEnum == SuperMatterialNecroObeliskState.Stop)
            {
                EnsureComp<PowerSupplierComponent>(uid).MaxSupply = 65000;
                component.StateEnum = SuperMatterialNecroObeliskState.Zero;
            }

            if (component.Percents >= 25 && component.StateEnum < SuperMatterialNecroObeliskState.TwentyFive)
            {
                EnsureComp<PowerSupplierComponent>(uid).MaxSupply = 245000;
                component.IsActive = true;
                SetRangeSanity(component, 8f);
                UpdateState(uid, component);
                component.StateEnum = SuperMatterialNecroObeliskState.TwentyFive;
            }

            if (component.Percents >= 50 && component.StateEnum < SuperMatterialNecroObeliskState.Fifty)
            {
                EnsureComp<PowerSupplierComponent>(uid).MaxSupply = 385000;
                SetRangeSanity(component, 12f);
                SpawnKudzu(uid, component);
                component.StateEnum = SuperMatterialNecroObeliskState.Fifty;
            }

            if (component.Percents > 50 && component.Percents < 100)
                EnsureKudzuAlive(uid, component);

            if (component.Percents >= 70 && component.StateEnum < SuperMatterialNecroObeliskState.Seventy)
            {
                SpawnAnomaly(uid, component);
                component.NextCheckNecroSpawn = _gameTiming.CurTime + TimeSpan.FromSeconds(60);
                component.StateEnum = SuperMatterialNecroObeliskState.Seventy;
            }

            if (component.Percents > 70 && component.Percents < 100)
            {
                if (component.NextCheckNecroSpawn == TimeSpan.Zero)
                {
                    component.NextCheckNecroSpawn = _gameTiming.CurTime + TimeSpan.FromSeconds(60);
                    continue;
                }
                if (_gameTiming.CurTime > component.NextCheckNecroSpawn)
                {
                    for (var i = 0; i < 4; i++)
                        Spawn(component.NecroPrototype, Transform(uid).Coordinates.Offset(Vector2.Create(_random.NextFloat(-3, 3), _random.NextFloat(-3, 3))));
                    component.NextCheckNecroSpawn = _gameTiming.CurTime + TimeSpan.FromSeconds(60);
                }
            }

            if (component.Percents < 100)
                continue;

            if (!_gameTicker.AllPreviousGameRules.Any(x => x.Item2 == "SiegeOfTheCircle" || x.Item2 == "Unitology"))
            {
                component.Percents = 99;
                continue;
            }
            var coord = Transform(uid).Coordinates;
            component.SequenceStarted = false;
            component.StateEnum = SuperMatterialNecroObeliskState.Hundred;
            component.NextCheckPercents = TimeSpan.Zero;
            component.NextCheckNecroSpawn = TimeSpan.Zero;
            component.NextCheckKudzuSpawn = TimeSpan.Zero;
            _explosion.QueueExplosion(uid,
                "Default",
                1000000,
                5,
                100);
            Spawn("ObeliskSplinter", coord);
        }
    }

    private void DoAbsorb(EntityUid uid, SuperMatterialNecroObeliskComponent component, NecroobeliskAbsorbEvent args)
    {
        _beam.TryCreateBeam(uid, args.Target, "NecroLightning");

        QueueDel(args.Target);
        component.MobsAbsorbed += 1;
        Dirty(uid, component);
    }

    private void DoAppearanceMoon(EntityUid uid, SuperMatterialNecroObeliskComponent component, NecroMoonAppearanceEvent args)
    {
        var ev = new SpawnNecroMoonEvent();
        var query = AllEntityQuery<UnitologyRuleComponent>();
        while (query.MoveNext(out var rule, out _))
        {
            RaiseLocalEvent(rule, ref ev);
        }

        var query2 = AllEntityQuery<CircleOpsRuleComponent>();
        while (query2.MoveNext(out var rule, out _))
        {
            RaiseLocalEvent(rule, ref ev);
        }

        Spawn("NecroMoon", Transform(uid).Coordinates);
    }

    private void OnConvergence(EntityUid uid, SuperMatterialNecroObeliskComponent component, NecroobeliskStartConvergenceEvent args)
    {
        if (!component.IsCanStartConvergence || !component.IsActive)
            return;

        var msg = new GameGlobalSoundEvent(component.SoundConvergence, AudioParams.Default);
        var stationFilter = _stationSystem.GetInOwningStation(uid);
        stationFilter.AddPlayersByPvs(uid, entityManager: EntityManager);
        RaiseNetworkEvent(msg, stationFilter);

        component.IsStageConvergence = true;
    }

    private void OnDestruction(EntityUid uid, SuperMatterialNecroObeliskComponent component, DestructionEventArgs args)
    {
        if (!component.EndAfterDestroy)
            return;

        _roundEnd.RequestRoundEnd(
            TimeSpan.FromMinutes(1),
            requester: null,
            checkCooldown: false,
            text: "uni-centcomm-announcement-obelisk-was-destroyed",
            name: "round-end-system-shuttle-sender-announcement",
            announcementSource: uid
        );
    }

    private void OnMapInit(EntityUid uid, SuperMatterialNecroObeliskComponent component, MapInitEvent args)
    {
        component.NextPulseTime = _gameTiming.CurTime + component.TimeUtilPulse;
        component.IsActive = false;
        UpdateState(uid, component);
    }

    private void AnnounceActivation(EntityUid uid, SuperMatterialNecroObeliskComponent component)
    {
        if (!component.IsGivesWarnings ||
            !TryComp(uid, out TransformComponent? xform) ||
            xform.MapID == MapId.Nullspace)
        {
            return;
        }

        var announcement = Loc.GetString(ActivationAnnouncement);
        _chatSystem.DispatchAdminFilteredAnnouncement(
            Filter.Empty().AddInMap(xform.MapID, EntityManager),
            announcement,
            sender: ActivationAnnouncementSender,
            colorOverride: Color.LightSeaGreen,
            originalMessage: announcement,
            voice: ActivationAnnouncementVoice);
    }

    private void OnSeverityChanged(EntityUid uid, SuperMatterialNecroObeliskComponent component, ref NecroobeliskPulseEvent args)
    {
        if (_mobState.IsDead(uid))
            return;

        if (_gameTiming.CurTime >= component.NextSoundTime)
        {
            _audio.PlayPvs(component.Sound, uid, AudioParams.Default.WithVariation(0.05f).WithVolume(15f));
            component.NextSoundTime = _gameTiming.CurTime + component.SoundCooldown;
        }
    }

    private void OnSanityLost(EntityUid uid, SuperMatterialNecroObeliskComponent component, ref SanityLostEvent args)
    {
        if (HasComp<NecromorfComponent>(args.VictinUID) || HasComp<ZombieComponent>(args.VictinUID))
            return;

        if (!HasComp<UnitologyComponent>(args.VictinUID)
            && !HasComp<UnitologyHeadComponent>(args.VictinUID)
            && !HasComp<UnitologyEnslavedComponent>(args.VictinUID))
        {
            AddComp<UnitologyEnslavedComponent>(args.VictinUID);
        }

        if (!HasComp<InfectionDeadComponent>(args.VictinUID))
            AddComp<InfectionDeadComponent>(args.VictinUID);

        DamageSpecifier dspec = new();
        dspec.DamageDict.Add("Cellular", 2f);
        _damage.TryChangeDamage(args.VictinUID, dspec, true, false);
    }

    private void SpawnKudzu(EntityUid uid, SuperMatterialNecroObeliskComponent component)
    {
        if (!component.SpawnCudzu)
            return;

        Spawn(component.KudzuPrototype, Transform(uid).Coordinates);
        component.NextCheckKudzuSpawn = _gameTiming.CurTime + component.KudzuRespawnTime;
    }

    private void SpawnAnomaly(EntityUid uid, SuperMatterialNecroObeliskComponent component)
    {
        var xform = Transform(uid);
        if (_stationSystem.GetOwningStation(uid, xform) is not { } station)
            return;

        if (_stationSystem.GetLargestGrid(station) is not { } grid)
            return;

        _anomaly.SpawnOnRandomGridLocation(grid, component.AnomalyPrototype);
    }

    private void EnsureKudzuAlive(EntityUid uid, SuperMatterialNecroObeliskComponent component)
    {
        if (!component.SpawnCudzu || _gameTiming.CurTime < component.NextCheckKudzuSpawn)
            return;

        var kudzu = _lookup.GetEntitiesInRange<NecroKudzuComponent>(Transform(uid).Coordinates, component.KudzuRespawnRange);
        if (kudzu.Count == 0)
        {
            SpawnKudzu(uid, component);
            return;
        }

        component.NextCheckKudzuSpawn = _gameTiming.CurTime + component.KudzuRespawnTime;
    }

    private void DoSetObeliskVerbs(EntityUid uid, SuperMatterialNecroObeliskComponent component, GetVerbsEvent<Verb> args)
    {

        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        var player = actor.PlayerSession;

        if (!_adminManager.IsAdmin(player))
            return;

        if (component.IsActive)
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Выключить обелиск"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => ToggleObeliskActive(uid, component),
                Impact = LogImpact.Medium
            });
        }
        else
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Включить обелиск"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => ToggleObeliskActive(uid, component),
                Impact = LogImpact.Medium
            });
        }

        if (HasComp<DamageableComponent>(uid))
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Включить неуязвимость"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => SetDamageable(uid, component),
                Impact = LogImpact.Medium
            });
        }
        else
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Выключить неуязвимость"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => SetDamageable(uid, component),
                Impact = LogImpact.Medium
            });
        }
        args.Verbs.Add(new Verb()
        {
            Text = Loc.GetString("Изменить радиус безумного фона на 10"),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => SetRangeSanity(component, 10f),
            Impact = LogImpact.Medium
        });
        args.Verbs.Add(new Verb()
        {
            Text = Loc.GetString("Изменить радиус безумного фона на 20"),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => SetRangeSanity(component, 20f),
            Impact = LogImpact.Medium
        });
        args.Verbs.Add(new Verb()
        {
            Text = Loc.GetString("Изменить радиус безумного фона на 30"),
            Category = VerbCategory.Debug,
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
            Act = () => SetRangeSanity(component, 30f),
            Impact = LogImpact.Medium
        });

        if (component.IsStoper)
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Выключить возможность остановить обелиск"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => SetStoper(component),
                Impact = LogImpact.Medium
            });
        }
        else
        {
            args.Verbs.Add(new Verb()
            {
                Text = Loc.GetString("Включить возможность остановить обелиск"),
                Category = VerbCategory.Debug,
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/dot.svg.192dpi.png")),
                Act = () => SetStoper(component),
                Impact = LogImpact.Medium
            });
        }


    }

    public void SetStoper(SuperMatterialNecroObeliskComponent component)
    {
        component.IsStoper = !component.IsStoper;
    }

    public void SetDamageable(EntityUid uid, SuperMatterialNecroObeliskComponent component)
    {
        if (HasComp<DamageableComponent>(uid))
        {
            RemComp<DamageableComponent>(uid);
        }
        else
        {
            AddComp<DamageableComponent>(uid);
        }
    }

    public void SetRangeSanity(SuperMatterialNecroObeliskComponent component, float radius)
    {
        component.RangeSanity = radius;
    }

    public void StartActivationSequence(EntityUid target, SuperMatterialNecroObeliskComponent component)
    {
        if (component.SequenceStarted || component.Percents == 99)
            return;

        component.SequenceStarted = true;
        AnnounceActivation(target, component);
    }

    public void ToggleObeliskActive(EntityUid target, SuperMatterialNecroObeliskComponent component)
    {
        component.IsActive = !component.IsActive;
        UpdateState(target, component);
    }

    public void SetActive(EntityUid target, bool isActive, SuperMatterialNecroObeliskComponent? component = null)
    {
        if (!Resolve(target, ref component))
            return;

        component.IsActive = isActive;
        UpdateState(target, component);
    }
}
