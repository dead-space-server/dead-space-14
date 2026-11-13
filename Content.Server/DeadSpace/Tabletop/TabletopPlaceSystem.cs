using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Tabletop;
using Content.Server.Tabletop.Components;
using Content.Shared.CCVar;
using Content.Shared.DeadSpace.Tabletop.Components;
using Content.Shared.Interaction;
using Content.Shared.Tabletop.Components;
using Content.Shared.Verbs;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Tabletop;

public sealed partial class TabletopPlaceSystem : EntitySystem
{
    private const int MaxPiecesPerBoard = 10;

    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TabletopSystem _tabletop = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<ActivationVerb>>(AddPlaceFigurineVerb);
        SubscribeLocalEvent<InteractUsingEvent>(OnInteractUsing);
    }

    private void AddPlaceFigurineVerb(GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryComp(args.Target, out TabletopGameComponent? component))
            return;

        if (!_hands.TryGetActiveItem(args.User, out var held))
            return;

        if (!TryComp(held.Value, out TabletopPlaceableComponent? _))
            return;

        var verb = new ActivationVerb
        {
            Text = Loc.GetString("tabletop-place-figurine"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/die.svg.192dpi.png")),
            Act = () => PlaceFigurine(held.Value, args.Target, component, args.User)
        };

        args.Verbs.Add(verb);
    }

    private void OnInteractUsing(InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.Target, out TabletopGameComponent? component))
            return;

        if (!TryComp(args.Used, out TabletopPlaceableComponent? _))
            return;

        if (_cfg.GetCVar(CCVars.GameTabletopPlace))
            return;

        args.Handled = true;
        PlaceFigurine(args.Used, args.Target, component, args.User);
    }

    private void PlaceFigurine(EntityUid held, EntityUid tableUid, TabletopGameComponent component, EntityUid user)
    {
        if (_cfg.GetCVar(CCVars.GameTabletopPlace))
            return;

        if (component.Session is not { } session)
        {
            if (!TryComp(user, out ActorComponent? actor))
                return;

            session = _tabletop.EnsureSession(component);

            if (!session.Players.ContainsKey(actor.PlayerSession))
                _tabletop.OpenSessionFor(actor.PlayerSession, tableUid);
        }

        var meta = MetaData(held);
        var protoId = meta.EntityPrototype?.ID;

        if (protoId == null)
            return;

        if (CountPlacedFigurines(session) >= MaxPiecesPerBoard)
        {
            _popup.PopupEntity(Loc.GetString("tabletop-max-pieces", ("max", MaxPiecesPerBoard)), tableUid, user);
            return;
        }

        var hologram = Spawn(protoId, session.Position.Offset(-1, 0));

        EnsureComp<TabletopDraggableComponent>(hologram);
        EnsureComp<TabletopHologramComponent>(hologram);

        var placed = EnsureComp<TabletopPlacedFigurineComponent>(hologram);
        placed.Prototype = protoId;

        session.Entities.Add(hologram);

        QueueDel(held);

        _popup.PopupEntity(Loc.GetString("tabletop-added-piece"), tableUid, user);
    }

    private int CountPlacedFigurines(TabletopSession session)
    {
        var count = 0;

        foreach (var entity in session.Entities)
        {
            if (HasComp<TabletopPlacedFigurineComponent>(entity))
                count++;
        }

        return count;
    }
}
