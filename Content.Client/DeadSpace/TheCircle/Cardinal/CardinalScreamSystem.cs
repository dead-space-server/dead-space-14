// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.TheCircle.Cardinal;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// The cardinal's screams on the client: the menu to pick one, the wave rolling out, and for hunting fighters bodies
/// through walls and arrows to marked outsiders off the screen.
/// </summary>
public sealed partial class CardinalScreamSystem : EntitySystem
{
    private static readonly SpriteSpecifier RallyIcon =
        new SpriteSpecifier.Rsi(new ResPath("Interface/Actions/changeling2.rsi"), "screech");
    private static readonly SpriteSpecifier HuntIcon =
        new SpriteSpecifier.Texture(new ResPath("_DeadSpace/Actions/thermal/thermal_toogle.png"));

    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private CardinalScreamWaveOverlay _waves = default!;
    private CardinalHuntOverlay _hunt = default!;
    private SimpleRadialMenu? _menu;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardinalScreamComponent, CardinalScreamActionEvent>(OnAction);

        _waves = new CardinalScreamWaveOverlay(EntityManager, _timing, _transform, _prototypes);
        _hunt = new CardinalHuntOverlay(EntityManager, _player, _timing, _lookup, _sprite, _transform);
        _overlays.AddOverlay(_waves);
        _overlays.AddOverlay(_hunt);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _menu?.Close();
        _overlays.RemoveOverlay(_waves);
        _overlays.RemoveOverlay(_hunt);
    }

    private void OnAction(Entity<CardinalScreamComponent> ent, ref CardinalScreamActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;
        _menu?.Close();
        var remaining = ent.Comp.NextScream - _timing.CurTime;
        var ready = remaining <= TimeSpan.Zero;
        var cooldown = ready
            ? string.Empty
            : "\n" + Loc.GetString("cardinal-scream-cooldown", ("seconds", Math.Ceiling(remaining.TotalSeconds)));

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(new List<RadialMenuOptionBase>
        {
            Option(ent.Owner, CardinalScreamKind.Rally, RallyIcon, "cardinal-scream-rally", Color.FromHex("#7a1414"), ready, cooldown),
            Option(ent.Owner, CardinalScreamKind.Hunt, HuntIcon, "cardinal-scream-hunt", Color.FromHex("#14602a"), ready, cooldown),
        });
        _menu.OpenCentered();
    }

    private RadialMenuActionOption<CardinalScreamKind> Option(EntityUid cardinal, CardinalScreamKind kind,
        SpriteSpecifier icon, string tooltip, Color color, bool ready, string cooldown)
    {
        return new RadialMenuActionOption<CardinalScreamKind>(picked =>
        {
            if (ready)
                RaiseNetworkEvent(new CardinalScreamChoiceEvent(GetNetEntity(cardinal), picked));
            _menu?.Close();
        }, kind)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(icon),
            ToolTip = Loc.GetString(tooltip) + cooldown,
            BackgroundColor = ready ? color : Color.DarkSlateGray,
            HoverBackgroundColor = ready ? Color.InterpolateBetween(color, Color.White, 0.25f) : Color.DarkSlateGray,
        };
    }
}
