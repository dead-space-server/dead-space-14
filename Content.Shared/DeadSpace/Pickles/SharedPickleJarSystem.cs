// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Pickles.Components;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Pickles;

public abstract class SharedPickleJarSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly OpenableSystem _openable = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly ProtoId<ReagentPrototype> Nutriment = "Nutriment";
    private static readonly ProtoId<ReagentPrototype> Vitamin = "Vitamin";

    private static readonly SoundSpecifier OpenSound = new SoundCollectionSpecifier("pop");
    private static readonly SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Items/bottle_close1.ogg");
    private static readonly SoundSpecifier SlipSound = new SoundPathSpecifier("/Audio/Effects/slip.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickleJarComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PickleJarComponent, AfterAutoHandleStateEvent>(OnJarState);
        SubscribeLocalEvent<PickleJarComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<PickleJarComponent, InteractHandEvent>(OnInteractHand, before: [typeof(SharedItemSystem)]);
        SubscribeLocalEvent<PickleJarComponent, UseInHandEvent>(OnUseInHand, before: [typeof(OpenableSystem)]);
        // After Openable: it clears Handled when the jar is open (!Opened = false).
        SubscribeLocalEvent<PickleJarComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(SolutionTransferSystem), typeof(IngestionSystem)],
            after: [typeof(OpenableSystem)]);
        SubscribeLocalEvent<PickleJarComponent, IngestibleEvent>(OnIngestible);
        SubscribeLocalEvent<PickleJarComponent, OpenableOpenedEvent>(OnOpened);
        SubscribeLocalEvent<PickleJarComponent, OpenableClosedEvent>(OnClosed);
        SubscribeLocalEvent<PickleJarComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PickleJarComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PickledProduceComponent, ExaminedEvent>(OnPickledExamined);
    }

    private void OnStartup(Entity<PickleJarComponent> ent, ref ComponentStartup args)
    {
        UpdateJarVisuals(ent);
    }

    private void OnJarState(Entity<PickleJarComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateJarVisuals(ent);
    }

    private void OnEquippedHand(Entity<PickleJarComponent> ent, ref GotEquippedHandEvent args)
    {
        UpdateJarVisuals(ent);
    }

    public void UpdateJarVisuals(Entity<PickleJarComponent> ent)
    {
        var count = ent.Comp.IsWine ? 0 : Math.Clamp(ent.Comp.RemainingPieces, 0, 4);
        _appearance.SetData(ent, PickleJarVisuals.ProduceCount, count);
        _appearance.SetData(ent, PickleJarVisuals.ContentsStyle, ent.Comp.ContentsStyle);

        var prefix = _openable.IsClosed(ent) ? "closed" : "open";
        _item.SetHeldPrefix(ent.Owner, prefix, force: true);
    }

    public void ClearJarContentsMeta(Entity<PickleJarComponent> ent)
    {
        ent.Comp.RemainingPieces = 0;
        ent.Comp.PiecePrototype = null;
        ent.Comp.PieceTint = null;
        ent.Comp.IsWine = false;
        ent.Comp.ContentsStyle = "cucumber";
        ent.Comp.PieceName = "pickle-piece-pickled";
        Dirty(ent);
        UpdateJarVisuals(ent);
        _meta.SetEntityName(ent, Loc.GetString("ent-FoodPickleJar"));
        _meta.SetEntityDescription(ent, Loc.GetString("ent-FoodPickleJar.desc"));
    }

    public static string ContentsStyleFor(PickleRecipePrototype? recipe, string? produceProto = null)
    {
        if (recipe is { ContentsStyle.Length: > 0 })
            return recipe.ContentsStyle;

        return produceProto switch
        {
            "FoodCucumber" => "cucumber",
            "FoodCabbage" => "cabbage",
            "FoodOnion" => "onion",
            "FoodOnionRed" => "onionred",
            "FoodCarrot" => "carrot",
            "FoodGarlic" => "garlic",
            "FoodMushroom" => "mushroom",
            "FoodCactus" => "cactus",
            "FoodTomato" => "tomato",
            "FoodWatermelon" => "watermelon",
            "FoodPumpkin" => "pumpkin",
            "FoodChiliPepper" => "chili",
            "FoodSoybeans" => "soy",
            _ => "cucumber",
        };
    }

    private void OnPickledExamined(Entity<PickledProduceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Method == PickleMethod.Salt
            ? "pickle-produce-examine-salt"
            : "pickle-produce-examine-vinegar"));
    }

    private void OnExamined(Entity<PickleJarComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.IsWine)
            args.PushMarkup(Loc.GetString("pickle-jar-examine-wine"));
        else if (ent.Comp.RemainingPieces > 0)
            args.PushMarkup(Loc.GetString("pickle-jar-pieces", ("count", ent.Comp.RemainingPieces)));
    }

    private void OnGetVerbs(Entity<PickleJarComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.RemainingPieces <= 0 || _openable.IsClosed(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("pickle-jar-verb-take"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
            Act = () => TryTakePiece(ent, user),
            Priority = 2,
        });
    }

    private void OnInteractHand(Entity<PickleJarComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (_openable.IsClosed(ent) || ent.Comp.RemainingPieces <= 0)
            return;

        args.Handled = TryTakePiece(ent, args.User);
    }

    private void OnUseInHand(Entity<PickleJarComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (_openable.IsClosed(ent))
            return;

        if (ent.Comp.RemainingPieces > 0)
        {
            args.Handled = TryEatOnePiece(ent, args.User);
            return;
        }

        if (IsDrinkEmpty(ent))
            args.Handled = true;
    }

    private void OnAfterInteract(Entity<PickleJarComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is null)
            return;

        // Always claim barrel clicks and empty-jar clicks so SolutionTransfer/Ingestion stay silent.
        // Re-set Handled even if Openable just cleared it for an open jar.
        if (HasComp<FermentationBarrelComponent>(args.Target.Value) || IsDrinkEmpty(ent))
            args.Handled = true;
    }

    private void OnOpened(Entity<PickleJarComponent> ent, ref OpenableOpenedEvent args)
    {
        UpdateJarVisuals(ent);
        if (IsJarEffectivelyEmpty(ent))
            return;

        _audio.PlayPredicted(OpenSound, ent, args.User);
    }

    private void OnClosed(Entity<PickleJarComponent> ent, ref OpenableClosedEvent args)
    {
        UpdateJarVisuals(ent);
        if (IsJarEffectivelyEmpty(ent))
            return;

        _audio.PlayPredicted(CloseSound, ent, args.User);
    }

    private bool IsJarEffectivelyEmpty(Entity<PickleJarComponent> ent)
    {
        return ent.Comp.RemainingPieces <= 0 && IsDrinkEmpty(ent);
    }

    private void OnIngestible(Entity<PickleJarComponent> ent, ref IngestibleEvent args)
    {
        if (ent.Comp.RemainingPieces > 0 || IsDrinkEmpty(ent))
            args.Cancelled = true;
    }

    private bool IsDrinkEmpty(Entity<PickleJarComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, PickleJarComponent.SolutionName, out _, out var drink))
            return true;

        return drink.Volume <= 0;
    }

    protected virtual bool TryEatOnePiece(Entity<PickleJarComponent> ent, EntityUid user)
    {
        if (_openable.IsClosed(ent) || ent.Comp.RemainingPieces <= 0 || ent.Comp.PiecePrototype is null)
            return false;

        return _net.IsClient;
    }

    protected virtual bool TryTakePiece(Entity<PickleJarComponent> ent, EntityUid user)
    {
        if (_openable.IsClosed(ent) || ent.Comp.RemainingPieces <= 0 || ent.Comp.PiecePrototype is null)
            return false;

        if (_net.IsClient)
            return true;

        if (!TrySpawnPiece(ent, user, out var piece))
            return false;

        if (_random.Prob(ent.Comp.SlipChance))
        {
            _popup.PopupEntity(Loc.GetString("pickle-jar-slip"), user, user);
            _audio.PlayPredicted(SlipSound, user, user);
            OnJarSlip(ent, user, piece);
            return true;
        }

        _hands.PickupOrDrop(user, piece);
        return true;
    }

    protected virtual void OnJarSlip(Entity<PickleJarComponent> jar, EntityUid user, EntityUid piece)
    {
        _hands.PickupOrDrop(user, piece, dropNear: true, animate: false);
    }

    protected bool TrySpawnPiece(Entity<PickleJarComponent> ent, EntityUid user, out EntityUid piece)
    {
        piece = default;
        if (ent.Comp.PiecePrototype is not { } proto)
            return false;

        var method = ent.Comp.Method;
        var pieceName = ent.Comp.PieceName;
        var tint = ent.Comp.PieceTint ?? SharedFermentationSystem.TintFor(method);

        ent.Comp.RemainingPieces--;
        var emptied = ent.Comp.RemainingPieces <= 0;
        Dirty(ent);
        UpdateJarVisuals(ent);

        piece = Spawn(proto, Transform(user).Coordinates);
        var name = Identity.Name(piece, EntityManager);
        _meta.SetEntityName(piece, Loc.GetString(pieceName, ("name", name)));
        var descId = pieceName.Id.Replace("food-name-", "food-desc-", StringComparison.Ordinal);
        _meta.SetEntityDescription(piece, Loc.TryGetString(descId, out var desc)
            ? desc
            : Loc.GetString("pickle-produce-desc", ("name", Loc.GetString(pieceName, ("name", name)))));

        var pickled = EnsureComp<PickledProduceComponent>(piece);
        pickled.Method = method;
        pickled.Tint = tint;
        pickled.PieceName = pieceName;
        Dirty(piece, pickled);

        ApplyPickledFlavor(piece, method);
        AfterSpawnPiece(ent, piece);

        if (emptied)
            ClearJarContentsMeta(ent);

        return true;
    }

    public void ApplyPickledFlavor(EntityUid produce, PickleMethod method)
    {
        if (TryComp<FlavorProfileComponent>(produce, out var flavor))
        {
            flavor.Flavors.Clear();
            flavor.Flavors.Add(method == PickleMethod.Salt ? "salty" : "sour");
            flavor.IgnoreReagents.Add(Water.Id);
            flavor.IgnoreReagents.Add(Nutriment.Id);
            flavor.IgnoreReagents.Add(Vitamin.Id);
        }

        if (!_solutions.TryGetSolution(produce, "food", out var foodSoln, out var food))
            return;

        var water = food.GetTotalPrototypeQuantity(Water);
        if (water <= 0)
            return;

        food.RemoveReagent(new ReagentId(Water, null), water);
        _solutions.UpdateChemicals(foodSoln.Value);
    }

    protected virtual void AfterSpawnPiece(Entity<PickleJarComponent> jar, EntityUid piece)
    {
    }
}
