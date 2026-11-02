// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.OrganPrinter;
using Content.Shared.Materials;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.OrganPrinter;

/// <summary>
/// Логика печати органов из биомассы.
/// </summary>
public sealed class OrganPrinterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _material = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPrinterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganPrinterComponent, AfterActivatableUIOpenEvent>(OnAfterUiOpen);
        SubscribeLocalEvent<OrganPrinterComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<OrganPrinterComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterPrintRequestMessage>(OnPrintRequest);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterGameFailedMessage>(OnGameFailed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OrganPrinterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PrintingProduct == null || _timing.CurTime < comp.PrintEndTime)
                continue;

            var product = comp.PrintingProduct.Value;
            comp.PrintingProduct = null;
            _appearance.SetData(uid, OrganPrinterVisuals.Working, false);

            var printed = Spawn(product, _transform.GetMapCoordinates(uid), null, _transform.GetWorldRotation(uid));
            _audio.PlayPvs(comp.PrintSound, uid);
            _popup.PopupEntity(
                Loc.GetString("organ-printer-printed",
                    ("name", Name(printed)), ("quality", comp.PrintQuality), ("waste", comp.PrintWaste)),
                uid);

            if (_ui.HasUi(uid, OrganPrinterUiKey.Key))
                _ui.SetUiState(uid, OrganPrinterUiKey.Key, BuildState(uid, comp));
        }
    }

    private void OnMapInit(Entity<OrganPrinterComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent.Owner, OrganPrinterVisuals.Working, false);
    }

    private void OnAfterUiOpen(EntityUid uid, OrganPrinterComponent comp, AfterActivatableUIOpenEvent args)
    {
        UpdateUi((uid, comp));
    }

    private void OnMaterialAmountChanged(Entity<OrganPrinterComponent> ent, ref MaterialAmountChangedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSolutionChanged(Entity<OrganPrinterComponent> ent, ref SolutionChangedEvent args)
    {
        if (args.Solution.Owner != ent.Owner)
            return;

        UpdateUi(ent);
    }

    private void OnPrintRequest(Entity<OrganPrinterComponent> ent, ref OrganPrinterPrintRequestMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (!this.IsPowered(ent.Owner, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("organ-printer-no-power"), ent.Owner, args.Actor);
            return;
        }

        if (ent.Comp.PrintingProduct != null)
        {
            _popup.PopupEntity(Loc.GetString("organ-printer-busy"), ent.Owner, args.Actor);
            return;
        }

        var productId = args.Product.Id;
        var entry = FindEntry(ent.Comp, productId, args.Group);
        if (entry == null)
            return;

        var quality = Math.Clamp(args.Quality, 0, 100);
        var waste = Math.Max(0, args.Waste);
        var effectiveCost = entry.Cost + (int) Math.Ceiling(waste * ent.Comp.WasteCost);
        var particles = entry.RequiresParticles ? ent.Comp.ParticlesPerPrint : 0f;

        if (particles > 0f && !HasParticles(ent.Owner, ent.Comp, entry.Group, particles))
        {
            _popup.PopupEntity(
                Loc.GetString("organ-printer-no-particles",
                    ("need", particles), ("reagent", ReagentName(ent.Comp, entry.Group))),
                ent.Owner,
                args.Actor);
            return;
        }

        if (_material.GetMaterialAmount(ent.Owner, ent.Comp.Material) < effectiveCost)
        {
            _popup.PopupEntity(
                Loc.GetString("organ-printer-no-biomass", ("need", effectiveCost)), ent.Owner, args.Actor);
            return;
        }

        if (!_material.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -effectiveCost))
            return;

        if (particles > 0f)
            ConsumeParticles(ent.Owner, ent.Comp, entry.Group, particles);

        ent.Comp.PrintingProduct = entry.Product;
        ent.Comp.PrintEndTime = _timing.CurTime + ent.Comp.PrintDuration;
        ent.Comp.PrintQuality = quality;
        ent.Comp.PrintWaste = waste;
        _appearance.SetData(ent.Owner, OrganPrinterVisuals.Working, true);

        _popup.PopupEntity(Loc.GetString("organ-printer-started"), ent.Owner, args.Actor);
        UpdateUi(ent);
    }

    private void OnGameFailed(Entity<OrganPrinterComponent> ent, ref OrganPrinterGameFailedMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (!this.IsPowered(ent.Owner, EntityManager))
            return;

        // Сорванная печать тоже тратит частицу вида: попытка уже была начата.
        var entry = FindEntry(ent.Comp, args.Product.Id, args.Group);
        if (entry == null)
            return;

        var particles = entry.RequiresParticles ? ent.Comp.ParticlesPerPrint : 0f;
        if (particles > 0f)
        {
            if (!HasParticles(ent.Owner, ent.Comp, args.Group, particles))
                return;

            ConsumeParticles(ent.Owner, ent.Comp, args.Group, particles);
        }

        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", args.FastMode ? 25 : 12);
        _damageable.TryChangeDamage(ent.Owner, damage, true);

        _popup.PopupEntity(Loc.GetString("organ-printer-game-failed"), ent.Owner, args.Actor);
        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
    }

    public void UpdateUi(Entity<OrganPrinterComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, OrganPrinterUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, OrganPrinterUiKey.Key, BuildState(ent.Owner, ent.Comp));
    }

    private OrganPrinterUpdateState BuildState(EntityUid uid, OrganPrinterComponent comp)
    {
        var products = new List<OrganPrinterProduct>();
        foreach (var entry in comp.Entries)
        {
            if (_proto.HasIndex(entry.Product))
                products.Add(new OrganPrinterProduct(entry.Product, entry.Cost, entry.Group, entry.RequiresParticles));
            else
                Log.Error($"Organ printer {ToPrettyString(uid)} has unknown product {entry.Product}");
        }

        var species = new List<OrganPrinterSpecies>();
        foreach (var (group, reagent) in comp.SpeciesReagents)
        {
            if (!_proto.HasIndex(reagent))
            {
                Log.Error($"Organ printer {ToPrettyString(uid)} has unknown species reagent {reagent}");
                continue;
            }

            species.Add(new OrganPrinterSpecies(group, reagent, ParticlesIn(uid, comp, group)));
        }

        return new OrganPrinterUpdateState(
            products, _material.GetMaterialAmount(uid, comp.Material), species, comp.ParticlesPerPrint);
    }

    private static OrganPrinterEntry? FindEntry(OrganPrinterComponent comp, string product, OrganSpeciesGroup group)
    {
        foreach (var entry in comp.Entries)
        {
            if (entry.Product.Id == product && entry.Group == group)
                return entry;
        }

        return null;
    }

    private float ParticlesIn(EntityUid uid, OrganPrinterComponent comp, OrganSpeciesGroup group)
    {
        if (!comp.SpeciesReagents.TryGetValue(group, out var reagent))
            return 0f;

        return _solutions.GetTotalPrototypeQuantity(uid, reagent.Id).Float();
    }

    private bool HasParticles(EntityUid uid, OrganPrinterComponent comp, OrganSpeciesGroup group, float amount)
    {
        return comp.SpeciesReagents.ContainsKey(group) && ParticlesIn(uid, comp, group) >= amount;
    }

    private void ConsumeParticles(EntityUid uid, OrganPrinterComponent comp, OrganSpeciesGroup group, float amount)
    {
        if (!comp.SpeciesReagents.TryGetValue(group, out var reagent))
            return;

        if (!_solutions.TryGetSolution(uid, comp.Solution, out var solutionEnt, out _))
            return;

        _solutions.RemoveReagent(solutionEnt.Value, reagent.Id, amount);
    }

    private string ReagentName(OrganPrinterComponent comp, OrganSpeciesGroup group)
    {
        if (!comp.SpeciesReagents.TryGetValue(group, out var reagent) || !_proto.TryIndex(reagent, out var proto))
            return string.Empty;

        return proto.LocalizedName;
    }
}
