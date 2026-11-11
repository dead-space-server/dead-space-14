// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using Content.Server.DeadSpace.Husbandry.Feeding;
using Content.Server.DeadSpace.Husbandry.Pen;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Atmos;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Server.DeadSpace.Beekeeping;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DeadSpace.DebugAnalyzer;
using Content.Shared.DeadSpace.Husbandry.Breeding;
using Content.Shared.DeadSpace.Husbandry.Feeding;
using Content.Shared.DeadSpace.Husbandry.Needs;
using Content.Shared.DeadSpace.Husbandry.Pen;
using Content.Shared.DeadSpace.Husbandry.Sex;
using Content.Shared.DeadSpace.Husbandry.Walking;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.DeadSpace.Husbandry.Breeding;
using Content.Server.DeadSpace.Husbandry.Growth;
using Content.Server.DeadSpace.Husbandry.Walking;

namespace Content.Server.DeadSpace.DebugAnalyzer;

/// <summary>
/// Scans a living being with a <see cref="DebugAnalyzerComponent"/> and keeps the open window up to date. The report
/// has a section for everything the being has: health, the vanilla satiation, the needs, sex, growth, breeding and
/// pen of livestock, the state of a bee, the pollination of a plant.
/// </summary>
public sealed class DebugAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly AnimalPenSystem _pens = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SatiationSystem _satiation = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string ReportPrefix = "debug-analyzer";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DebugAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DebugAnalyzerComponent, UseInHandEvent>(OnUseInHand);
        Subs.BuiEvents<DebugAnalyzerComponent>(DebugAnalyzerUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
    }

    private void OnAfterInteract(Entity<DebugAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        // Mobs, and also plants and hives: what bees do is easier to look at there.
        if (args.Handled || args.Target is not { } target || !args.CanReach || !IsScannable(target))
            return;

        args.Handled = true;
        Scan(ent, target, args.User);
    }

    private bool IsScannable(EntityUid target)
    {
        return HasComp<MobStateComponent>(target) || HasComp<PollinationComponent>(target) || HasComp<BeeHiveComponent>(target);
    }

    private void OnUseInHand(Entity<DebugAnalyzerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !HasComp<MobStateComponent>(args.User))
            return;

        args.Handled = true;
        Scan(ent, args.User, args.User);
    }

    private void OnUiClosed(Entity<DebugAnalyzerComponent> ent, ref BoundUIClosedEvent args)
    {
        ent.Comp.ScannedEntity = null;
    }

    private void Scan(Entity<DebugAnalyzerComponent> ent, EntityUid target, EntityUid user)
    {
        ent.Comp.ScannedEntity = target;
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateInterval;

        _ui.OpenUi(ent.Owner, DebugAnalyzerUiKey.Key, user);
        SendReport(ent.Owner, target);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DebugAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.ScannedEntity is not { } target || _timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;

            if (Deleted(target)
                || comp.MaxScanRange is { } range
                && !_transform.InRange(Transform(target).Coordinates, xform.Coordinates, range))
            {
                comp.ScannedEntity = null;
                _ui.CloseUi(uid, DebugAnalyzerUiKey.Key);
                continue;
            }

            try
            {
                SendReport(uid, target);
            }
            catch (Exception e)
            {
                Log.Error($"Debug analyzer: the report of {ToPrettyString(target)} failed: {e}");
                comp.ScannedEntity = null;
            }
        }
    }

    private void SendReport(EntityUid analyzer, EntityUid target)
    {
        var sections = new List<DebugAnalyzerSection>
        {
            BuildGeneral(target),
        };

        TryAdd(sections, () => BuildHealth(target));
        TryAdd(sections, () => BuildSatiation(target));
        TryAdd(sections, () => BuildNeeds(target));
        TryAdd(sections, () => BuildGrowth(target));
        TryAdd(sections, () => BuildBreeding(target));
        TryAdd(sections, () => BuildPen(target));
        TryAdd(sections, () => BuildBee(target));
        TryAdd(sections, () => BuildHive(target));
        TryAdd(sections, () => BuildPollination(target));

        var message = new DebugAnalyzerScannedMessage(GetNetEntity(target), Name(target), sections);
        _ui.ServerSendUiMessage(analyzer, DebugAnalyzerUiKey.Key, message);
    }

    // A section that throws (some being has something odd) is left out and logged, it must not take the whole report
    // down, and still less the update of every analyzer: one exception there would stop them all until the restart.
    private void TryAdd(List<DebugAnalyzerSection> sections, Func<DebugAnalyzerSection?> build)
    {
        try
        {
            if (build() is { } section)
                sections.Add(section);
        }
        catch (Exception e)
        {
            Log.Error($"Debug analyzer: a section of the report failed: {e}");
        }
    }

    private DebugAnalyzerSection BuildGeneral(EntityUid target)
    {
        var rows = new List<DebugAnalyzerRow>
        {
            new(L("prototype"), MetaData(target).EntityPrototype?.ID ?? "-"),
            new(L("entity"), GetNetEntity(target).ToString()),
        };

        if (HasComp<MobStateComponent>(target))
        {
            var state = _mobState.IsDead(target) ? MobState.Dead
                : _mobState.IsCritical(target) ? MobState.Critical
                : MobState.Alive;
            var severity = state switch
            {
                MobState.Critical => DebugAnalyzerSeverity.Warning,
                MobState.Dead => DebugAnalyzerSeverity.Critical,
                _ => DebugAnalyzerSeverity.Normal,
            };
            rows.Add(new DebugAnalyzerRow(L("mob-state"), state.ToString(), severity: severity));
        }

        if (TryComp<AnimalSexComponent>(target, out var sex))
            rows.Add(new DebugAnalyzerRow(L("sex"), Text(sex.Sex)));

        return new DebugAnalyzerSection(L("section-general"), rows);
    }

    private DebugAnalyzerSection? BuildHealth(EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) || !HasComp<MobStateComponent>(target))
            return null;

        var rows = new List<DebugAnalyzerRow>();
        var total = damageable.TotalDamage;

        if (_thresholds.TryGetDeadThreshold(target, out var dead))
        {
            var fraction = dead.Value > FixedPoint2.Zero ? (float) (total / dead.Value) : 0f;
            var severity = fraction switch
            {
                >= 0.75f => DebugAnalyzerSeverity.Critical,
                >= 0.4f => DebugAnalyzerSeverity.Warning,
                _ => DebugAnalyzerSeverity.Normal,
            };
            rows.Add(new DebugAnalyzerRow(L("damage-total"), $"{Fmt((float) total)} / {Fmt((float) dead.Value)}",
                Math.Clamp(fraction, 0f, 1f), severity));
        }
        else
        {
            rows.Add(new DebugAnalyzerRow(L("damage-total"), Fmt((float) total)));
        }

        if (_thresholds.TryGetThresholdForState(target, MobState.Critical, out var crit))
            rows.Add(new DebugAnalyzerRow(L("threshold-critical"), Fmt((float) crit.Value)));

        if (dead != null)
            rows.Add(new DebugAnalyzerRow(L("threshold-dead"), Fmt((float) dead.Value)));

        foreach (var (type, amount) in damageable.Damage.DamageDict)
        {
            if (amount <= FixedPoint2.Zero)
                continue;

            var name = _prototypes.TryIndex<DamageTypePrototype>(type, out var proto) ? proto.LocalizedName : type;
            rows.Add(new DebugAnalyzerRow(name, Fmt((float) amount)));
        }

        if (TryComp<TemperatureComponent>(target, out var temperature))
        {
            var kelvin = temperature.CurrentTemperature;
            rows.Add(new DebugAnalyzerRow(L("temperature"), $"{Fmt(kelvin)} K ({Fmt(kelvin - Atmospherics.T0C)} °C)"));
        }

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            // The share of the normal amount (the reference solution of the bloodstream), not of the volume of the
            // container, which is twice as big: 100% is a healthy being.
            var level = _bloodstream.GetBloodLevel((target, bloodstream));
            var severity = level switch
            {
                < 0.6f => DebugAnalyzerSeverity.Critical,
                < 0.9f => DebugAnalyzerSeverity.Warning,
                _ => DebugAnalyzerSeverity.Normal,
            };
            rows.Add(new DebugAnalyzerRow(L("blood"), $"{Fmt(level * 100f)}%", Math.Clamp(level, 0f, 1f), severity));

            var bleeding = bloodstream.BleedAmount > 0f;
            rows.Add(new DebugAnalyzerRow(L("bleeding"), bleeding ? Fmt(bloodstream.BleedAmount) : "-",
                severity: bleeding ? DebugAnalyzerSeverity.Warning : DebugAnalyzerSeverity.Normal));
        }

        return new DebugAnalyzerSection(L("section-health"), rows);
    }

    /// <summary>
    /// The vanilla hunger and thirst, every satiation that the being has, by its name.
    /// </summary>
    private DebugAnalyzerSection? BuildSatiation(EntityUid target)
    {
        if (!TryComp<SatiationComponent>(target, out var satiation))
            return null;

        var rows = new List<DebugAnalyzerRow>();
        foreach (var type in satiation.Satiations.Keys)
        {
            var value = _satiation.GetValueOrNull((target, satiation), type);
            rows.Add(new DebugAnalyzerRow(type.Id, value == null ? "-" : value.Value.ToString()));
        }

        return new DebugAnalyzerSection(L("section-satiation"), rows);
    }

    private DebugAnalyzerSection? BuildNeeds(EntityUid target)
    {
        if (!TryComp<AnimalNeedsComponent>(target, out var needs))
            return null;

        var rows = new List<DebugAnalyzerRow>();
        AddNeed(rows, "satiety", needs.Satiety);
        AddNeed(rows, "hydration", needs.Hydration);

        return new DebugAnalyzerSection(L("section-needs"), rows);
    }

    private static void AddNeed(List<DebugAnalyzerRow> rows, string name, NeedState need)
    {
        var level = need.GetLevel();
        var severity = level switch
        {
            NeedLevel.Low => DebugAnalyzerSeverity.Warning,
            NeedLevel.Empty => DebugAnalyzerSeverity.Critical,
            _ => DebugAnalyzerSeverity.Normal,
        };

        rows.Add(new DebugAnalyzerRow(L($"{name}-value"), $"{Fmt(need.Value)} / {Fmt(need.Max)}",
            Fraction(need.Value, need.Max), severity));
        rows.Add(new DebugAnalyzerRow(L($"{name}-level"), level.ToString(), severity: severity));
        rows.Add(new DebugAnalyzerRow(L($"{name}-decay"), Fmt(need.DecayPerMinute)));
        rows.Add(new DebugAnalyzerRow(L($"{name}-seek"), Fmt(need.SeekBelow)));
    }

    private DebugAnalyzerSection? BuildGrowth(EntityUid target)
    {
        if (!TryComp<GrowthComponent>(target, out var growth)
            || growth.CurrentStage < 0
            || growth.CurrentStage >= growth.Stages.Count)
        {
            return null;
        }

        var stage = growth.Stages[growth.CurrentStage];
        var rows = new List<DebugAnalyzerRow>
        {
            new(L("growth-stage"), $"{stage.Id} ({growth.CurrentStage + 1}/{growth.Stages.Count})"),
        };

        if (growth.StageEndTime is { } end)
            rows.Add(new DebugAnalyzerRow(L("growth-next"), FormatTime(end - _timing.CurTime)));
        else
            rows.Add(new DebugAnalyzerRow(L("growth-next"), "-"));

        rows.Add(new DebugAnalyzerRow(L("growth-scale"), $"{Fmt(stage.Scale.X)} x {Fmt(stage.Scale.Y)}"));
        rows.Add(new DebugAnalyzerRow(L("growth-meat"), stage.MeatCount.ToString(CultureInfo.InvariantCulture)));
        rows.Add(new DebugAnalyzerRow(L("growth-price"), Fmt((float) stage.Price)));

        return new DebugAnalyzerSection(L("section-growth"), rows);
    }

    private DebugAnalyzerSection? BuildBreeding(EntityUid target)
    {
        if (!TryComp<AnimalBreedingComponent>(target, out var breeding))
            return null;

        var now = _timing.CurTime;
        var rows = new List<DebugAnalyzerRow>
        {
            new(L("breeding-species"), breeding.Species),
            new(L("breeding-pregnant"), breeding.Gestating ? "yes" : "no",
                severity: breeding.Gestating ? DebugAnalyzerSeverity.Warning : DebugAnalyzerSeverity.Normal),
        };

        if (breeding.GestationEndTime is { } end)
            rows.Add(new DebugAnalyzerRow(L("breeding-birth"), FormatTime(end - now)));

        rows.Add(new DebugAnalyzerRow(L("breeding-cooldown"), FormatTime(breeding.NextAllowed - now)));
        rows.Add(new DebugAnalyzerRow(L("breeding-attempt"), FormatTime(breeding.NextAttempt - now)));
        rows.Add(new DebugAnalyzerRow(L("breeding-chance"), $"{Fmt(breeding.BreedChance * 100f)}%"));

        return new DebugAnalyzerSection(L("section-breeding"), rows);
    }

    private DebugAnalyzerSection? BuildPen(EntityUid target)
    {
        if (!TryComp<AnimalPenComponent>(target, out var penComp))
            return null;

        var pen = _pens.GetPen((target, penComp));
        var rows = new List<DebugAnalyzerRow>();

        if (pen == null)
        {
            rows.Add(new(L("pen-kind"), "-"));
            return new DebugAnalyzerSection(L("section-pen"), rows);
        }

        rows.Add(new(L("pen-kind"), pen.Kind.ToString(), null, pen.Kind == PenKind.Open ? DebugAnalyzerSeverity.Warning : DebugAnalyzerSeverity.Normal));
        rows.Add(new(L("pen-tiles"), pen.Tiles.Count.ToString(CultureInfo.InvariantCulture)));
        rows.Add(new(L("pen-anchor"), $"{pen.Anchor.X}, {pen.Anchor.Y}"));
        return new DebugAnalyzerSection(L("section-pen"), rows);
    }

    /// <summary>
    /// A worker bee: what it is doing and why it may be standing still (no hive, no queen, no player near).
    /// </summary>
    private DebugAnalyzerSection? BuildBee(EntityUid target)
    {
        if (!TryComp<BeeComponent>(target, out var bee))
            return null;

        var hive = bee.HiveOwner;
        var rows = new List<DebugAnalyzerRow>
        {
            new(L("bee-state"), Text(bee.State)),
            new(L("bee-hive"), hive == null || Deleted(hive.Value) ? "-" : GetNetEntity(hive.Value).ToString(),
                severity: hive == null || Deleted(hive.Value) ? DebugAnalyzerSeverity.Critical : DebugAnalyzerSeverity.Normal),
            new(L("bee-pollen"), $"{Fmt(bee.PollenCarried)} / {Fmt(bee.MaxPollenCarry)}", Fraction(bee.PollenCarried, bee.MaxPollenCarry)),
            new(L("bee-target"), bee.TargetFlower is { } flower && !Deleted(flower) ? GetNetEntity(flower).ToString() : "-"),
            new(L("bee-walker"), TryComp<AnimalWalkerComponent>(target, out var walker) ? Text(walker.Status) : "-"),
            new(L("bee-htn"), HasComp<HTNComponent>(target) ? "yes" : "no",
                severity: HasComp<HTNComponent>(target) ? DebugAnalyzerSeverity.Warning : DebugAnalyzerSeverity.Normal),
            new(L("bee-busy"), bee.IsBusy ? "yes" : "no"),
        };

        return new DebugAnalyzerSection(L("section-bee"), rows);
    }

    private DebugAnalyzerSection? BuildHive(EntityUid target)
    {
        if (!TryComp<BeeHiveComponent>(target, out var hive))
            return null;

        var rows = new List<DebugAnalyzerRow>
        {
            new(L("hive-bees"), $"{hive.Bees.Count} / {hive.MaxBees}"),
            new(L("hive-pollen"), $"{Fmt(hive.PollenStored)} / {Fmt(hive.MaxPollen)}", Fraction(hive.PollenStored, hive.MaxPollen)),
            new(L("hive-spawn"), $"{Fmt(hive.SpawnTimer)} / {Fmt(hive.SpawnInterval)}"),
            new(L("hive-production"), $"{Fmt(hive.ProductionTimer)} / {Fmt(hive.ProductionInterval)}"),
        };

        return new DebugAnalyzerSection(L("section-hive"), rows);
    }

    private DebugAnalyzerSection? BuildPollination(EntityUid target)
    {
        if (!TryComp<PollinationComponent>(target, out var pollination))
            return null;

        var now = _timing.CurTime;
        var rows = new List<DebugAnalyzerRow>
        {
            new(L("pollination-flowering"), pollination.IsFlowering ? "yes" : "no"),
            new(L("pollination-cooldown"), FormatTime(pollination.NextAvailable - now)),
            new(L("pollination-boost"), pollination.BoostEnd == TimeSpan.Zero ? "-" : FormatTime(pollination.BoostEnd - now)),
            new(L("pollination-age"), Text(pollination.AgeBonus)),
            new(L("pollination-potency"), $"{Fmt(pollination.PotencyBonus * 100f)}%"),
        };

        return new DebugAnalyzerSection(L("section-pollination"), rows);
    }

    // The value is passed as an argument: calling a method on a field of a component would count as an access of it.
    private static string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-";

    private static string L(string id) => $"{ReportPrefix}-{id}";

    private static string Fmt(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static float Fraction(float value, float max) => max > 0f ? Math.Clamp(value / max, 0f, 1f) : 0f;

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes}:{time.Seconds:00}";
    }
}
