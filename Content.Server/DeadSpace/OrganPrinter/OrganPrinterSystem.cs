// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.DeadSpace.Surgery;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
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
    public const int MaxWaste = 2048;

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SurgeryGraftSystem _graft = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MaterialStorageSystem _material = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPrinterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganPrinterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<OrganPrinterComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterPrintRequestMessage>(OnPrintRequest);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterEjectMessage>(OnEject);
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
            if (comp.GroupSpecies.TryGetValue(comp.PrintGroup, out var species))
                _graft.MarkPrinted(printed, species);
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

    private void OnBeforeUiOpen(Entity<OrganPrinterComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateUi(ent);
    }

    private void OnMaterialAmountChanged(Entity<OrganPrinterComponent> ent, ref MaterialAmountChangedEvent args)
    {
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
        var group = args.Group;
        var entry = ent.Comp.Entries.FirstOrDefault(e => e.Product.Id == productId && e.Group == group);
        if (entry == null)
            return;

        var quality = Math.Clamp(args.Quality, 0, 100);
        var waste = Math.Clamp(args.Waste, 0, MaxWaste);
        var effectiveCost = entry.Cost + (int) Math.Clamp(
            Math.Ceiling(waste * (double) ent.Comp.WasteCost), 0, int.MaxValue - entry.Cost);

        if (_material.GetMaterialAmount(ent.Owner, ent.Comp.Material) < effectiveCost)
        {
            _popup.PopupEntity(
                Loc.GetString("organ-printer-no-biomass", ("need", effectiveCost)), ent.Owner, args.Actor);
            return;
        }

        if (!_material.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -effectiveCost))
            return;

        ent.Comp.PrintingProduct = entry.Product;
        ent.Comp.PrintEndTime = _timing.CurTime + ent.Comp.PrintDuration;
        ent.Comp.PrintQuality = quality;
        ent.Comp.PrintWaste = waste;
        ent.Comp.PrintGroup = entry.Group;
        _appearance.SetData(ent.Owner, OrganPrinterVisuals.Working, true);

        _popup.PopupEntity(Loc.GetString("organ-printer-started"), ent.Owner, args.Actor);
        UpdateUi(ent);
    }

    private void OnEject(Entity<OrganPrinterComponent> ent, ref OrganPrinterEjectMessage args)
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

        var amount = _material.GetMaterialAmount(ent.Owner, ent.Comp.Material);
        if (amount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("organ-printer-eject-empty"), ent.Owner, args.Actor);
            return;
        }

        if (!_material.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -amount))
            return;

        _material.SpawnMultipleFromMaterial(amount, ent.Comp.Material, _transform.GetMoverCoordinates(ent.Owner));
        UpdateUi(ent);
    }

    private void OnGameFailed(Entity<OrganPrinterComponent> ent, ref OrganPrinterGameFailedMessage args)
    {
        if (!args.Actor.Valid)
            return;

        if (!this.IsPowered(ent.Owner, EntityManager))
            return;

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
                products.Add(new OrganPrinterProduct(entry.Product, entry.Cost, entry.Group));
            else
                Log.Error($"Organ printer {ToPrettyString(uid)} has unknown product {entry.Product}");
        }

        return new OrganPrinterUpdateState(products, _material.GetMaterialAmount(uid, comp.Material));
    }
}
