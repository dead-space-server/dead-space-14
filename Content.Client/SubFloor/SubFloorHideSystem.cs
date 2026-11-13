using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared.Eye; //DS-14
using Content.Shared.SubFloor;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Player;

namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem : SharedSubFloorHideSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private VisibilityFlags _showLayers; //DS-14

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowAll
    {
        get => (_showLayers & SubfloorVisibility.All) == SubfloorVisibility.All; //DS-14
        set => SetShowLayers(value ? SubfloorVisibility.All : VisibilityFlags.None); //DS-14
    }

    //DS-14 start
    public VisibilityFlags ShowLayers => _showLayers;

    public void ToggleLayers(VisibilityFlags layers)
    {
        layers &= SubfloorVisibility.All;
        if (layers == VisibilityFlags.None)
            return;

        if ((_showLayers & layers) == layers)
            SetShowLayers(_showLayers & ~layers);
        else
            SetShowLayers(_showLayers | layers);
    }

    private void SetShowLayers(VisibilityFlags value)
    {
        value &= SubfloorVisibility.All;
        var categories = VisibilityFlags.SubfloorCable | VisibilityFlags.SubfloorAtmos | VisibilityFlags.SubfloorDisposal; //DS-14
        if ((value & categories) == 0) //DS-14
            value &= ~VisibilityFlags.Subfloor; //DS-14
        if (_showLayers == value)
            return;

        _showLayers = value;
        _ui.GetUIController<SandboxUIController>().SetSubfloorLayers(value);

        RaiseNetworkEvent(new ShowSubfloorRequestEvent
        {
            Mask = (int) value,
        });
    }

    private bool IsLayerShown(SubfloorViewLayer layer)
    {
        return (_showLayers & SubfloorViewLayers.ToFlag(layer)) != 0;
    }
    //DS-14 end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubFloorHideComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeNetworkEvent<ShowSubfloorRequestEvent>(OnRequestReceived);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        // Vismask resets so need to reset this.
        ShowAll = false;
    }

    private void OnRequestReceived(ShowSubfloorRequestEvent ev)
    {
        // When client receives request Queue an update on all vis.
        UpdateAll();
    }

    private void OnAppearanceChanged(EntityUid uid, SubFloorHideComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        _appearance.TryGetData<bool>(uid, SubFloorVisuals.Covered, out var covered, args.Component);
        _appearance.TryGetData<bool>(uid, SubFloorVisuals.ScannerRevealed, out var scannerRevealed, args.Component);

        var layerShown = IsLayerShown(component.ViewLayer); //DS-14
        scannerRevealed &= !layerShown; // no transparency for show-subfloor mode. //DS-14

        var revealed = !covered || layerShown || scannerRevealed; //DS-14

        // set visibility & color of each layer
        foreach (var layer in args.Sprite.AllLayers)
        {
            // pipe connection visuals are updated AFTER this, and may re-hide some layers
            layer.Visible = revealed;
        }

        // Is there some layer that is always visible?
        var hasVisibleLayer = false;
        foreach (var layerKey in component.VisibleLayers)
        {
            if (!_sprite.LayerMapTryGet((uid, args.Sprite), layerKey, out var layerIndex, false))
                continue;

            var layer = args.Sprite[layerIndex];
            layer.Visible = true;
            layer.Color = layer.Color.WithAlpha(1f);
            hasVisibleLayer = true;
        }

        _sprite.SetVisible((uid, args.Sprite), hasVisibleLayer || revealed);

        if (layerShown) //DS-14
        {
            // Allows sandbox mode to make wires visible over other stuff.
            component.OriginalDrawDepth ??= args.Sprite.DrawDepth;
            _sprite.SetDrawDepth((uid, args.Sprite), (int)Shared.DrawDepth.DrawDepth.Overdoors);
        }
        else if (scannerRevealed)
        {
            // Allows a t-ray to show wires/pipes above carpets/puddles.
            if (component.OriginalDrawDepth is not null)
                return;
            component.OriginalDrawDepth = args.Sprite.DrawDepth;
            var drawDepthDifference = Shared.DrawDepth.DrawDepth.ThickPipe - Shared.DrawDepth.DrawDepth.Puddles;
            _sprite.SetDrawDepth((uid, args.Sprite), args.Sprite.DrawDepth - (drawDepthDifference - 1));
        }
        else if (component.OriginalDrawDepth.HasValue)
        {
            _sprite.SetDrawDepth((uid, args.Sprite), component.OriginalDrawDepth.Value);
            component.OriginalDrawDepth = null;
        }
    }

    private void UpdateAll()
    {
        var query = AllEntityQuery<SubFloorHideComponent, AppearanceComponent>();
        while (query.MoveNext(out var uid, out _, out var appearance))
        {
            _appearance.QueueUpdate(uid, appearance);
        }
    }
}
