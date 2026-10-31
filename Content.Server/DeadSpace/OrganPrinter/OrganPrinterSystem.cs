// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
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

namespace Content.Server.DeadSpace.OrganPrinter;

/// <summary>
/// Логика печати органов из биомассы.
/// </summary>
public sealed class OrganPrinterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _material = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganPrinterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<OrganPrinterComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterPrintRequestMessage>(OnPrintRequest);
        SubscribeLocalEvent<OrganPrinterComponent, OrganPrinterGameFailedMessage>(OnGameFailed);
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

        var productId = args.Product.Id;
        var entry = ent.Comp.Entries.FirstOrDefault(e => e.Product.Id == productId);
        if (entry == null)
            return;

        var quality = Math.Clamp(args.Quality, 0, 100);
        var waste = Math.Max(0, args.Waste);
        var effectiveCost = entry.Cost + (int) Math.Ceiling(waste * ent.Comp.WasteCost);

        if (_material.GetMaterialAmount(ent.Owner, ent.Comp.Material) < effectiveCost)
        {
            _popup.PopupEntity(
                Loc.GetString("organ-printer-no-biomass", ("need", effectiveCost)), ent.Owner, args.Actor);
            return;
        }

        if (!_material.TryChangeMaterialAmount(ent.Owner, ent.Comp.Material, -effectiveCost))
            return;

        var printed = Spawn(entry.Product, _transform.GetMapCoordinates(ent.Owner), null, _transform.GetWorldRotation(ent.Owner));

        _audio.PlayPvs(ent.Comp.PrintSound, ent.Owner);
        _popup.PopupEntity(
            Loc.GetString("organ-printer-printed", ("name", Name(printed)), ("quality", quality), ("waste", waste)),
            ent.Owner, args.Actor);
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

        var products = new List<OrganPrinterProduct>();
        foreach (var entry in ent.Comp.Entries)
        {
            if (_proto.HasIndex(entry.Product))
                products.Add(new OrganPrinterProduct(entry.Product, entry.Cost));
            else
                Log.Error($"Organ printer {ToPrettyString(ent.Owner)} has unknown product {entry.Product}");
        }

        _ui.SetUiState(ent.Owner, OrganPrinterUiKey.Key,
            new OrganPrinterUpdateState(products, _material.GetMaterialAmount(ent.Owner, ent.Comp.Material)));
    }
}
