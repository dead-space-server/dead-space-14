// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Surgery;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.DeadSpace.OrganPrinter;
using Content.Shared.Materials;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.OrganPrinter;

public sealed partial class OrganPrinterSystem : EntitySystem
{
    private const int MaxProductIdLength = 64;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SurgeryGraftSystem _graft = default!;
    [Dependency] private SurgeryOrganFreshSystem _fresh = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private MaterialStorageSystem _material = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPrinterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganPrinterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<OrganPrinterComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterPrintMessage>(OnPrint);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterEjectMessage>(OnEject);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OrganPrinterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PrintingProduct == null || _timing.CurTime < comp.PrintEndTime)
                continue;

            FinishPrint((uid, comp));
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

    private void OnPrint(Entity<OrganPrinterComponent> ent, ref OrganPrinterPrintMessage args)
    {
        if (!args.Actor.Valid || !this.IsPowered(ent.Owner, EntityManager))
        {
            if (args.Actor.Valid)
                _popup.PopupEntity(Loc.GetString("organ-printer-no-power"), ent.Owner, args.Actor);
            return;
        }

        if (ent.Comp.PrintingProduct != null)
        {
            _popup.PopupEntity(Loc.GetString("organ-printer-busy"), ent.Owner, args.Actor);
            return;
        }

        if (args.Product.Id.Length == 0 || args.Product.Id.Length > MaxProductIdLength)
            return;

        OrganPrinterEntry? entry = null;
        foreach (var candidate in ent.Comp.Entries)
        {
            if (candidate.Product == args.Product && candidate.Group == args.Group)
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null || !_proto.HasIndex(entry.Product))
            return;

        if (_material.GetMaterialAmount(ent.Owner, ent.Comp.Material) < entry.Cost)
        {
            _popup.PopupEntity(Loc.GetString("organ-printer-no-biomass", ("need", entry.Cost)), ent.Owner, args.Actor);
            return;
        }

        if (!_material.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -entry.Cost))
            return;

        ent.Comp.PrintingProduct = entry.Product;
        ent.Comp.PrintEndTime = _timing.CurTime + ent.Comp.PrintDuration;
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

    private void FinishPrint(Entity<OrganPrinterComponent> ent)
    {
        if (ent.Comp.PrintingProduct is not { } product)
            return;

        ent.Comp.PrintingProduct = null;
        _appearance.SetData(ent.Owner, OrganPrinterVisuals.Working, false);

        var printed = Spawn(product, _transform.GetMapCoordinates(ent.Owner), null, _transform.GetWorldRotation(ent.Owner));
        if (ent.Comp.GroupSpecies.TryGetValue(ent.Comp.PrintGroup, out var species))
            _graft.MarkPrinted(printed, species);
        _fresh.MakeFresh(printed);

        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("organ-printer-printed", ("name", Name(printed))), ent.Owner);

        if (_ui.HasUi(ent.Owner, OrganPrinterUiKey.Key))
            _ui.SetUiState(ent.Owner, OrganPrinterUiKey.Key, BuildState(ent.Owner, ent.Comp));
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

        return new OrganPrinterUpdateState(products, _material.GetMaterialAmount(uid, comp.Material), comp.PrintingProduct != null);
    }
}
