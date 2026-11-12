using Content.Client.Popups;
using Content.Client.UserInterface.Controls;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.RCD;

[UsedImplicitly]
<<<<<<< ours
// DS14 - pre-v288 engine
public sealed class RCDMenuBoundUserInterface : BoundUserInterface
||||||| base
public sealed class RCDMenuBoundUserInterface : BoundUserInterface
=======
public sealed partial class RCDMenuBoundUserInterface : BoundUserInterface
>>>>>>> theirs
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RCDSystem _rcd = default!;

    private const string TopLevelActionCategory = "Main";

<<<<<<< ours
    // DS14-start
    private static readonly Dictionary<string, (string Tooltip, int Order, SpriteSpecifier Sprite)> PrototypesGroupingInfo
        = new Dictionary<string, (string Tooltip, int Order, SpriteSpecifier Sprite)>
||||||| base
    private static readonly Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)> PrototypesGroupingInfo
        = new Dictionary<string, (string Tooltip, SpriteSpecifier Sprite)>
=======
    private static readonly Dictionary<string, (string Tooltip, int Order, SpriteSpecifier Sprite)> PrototypesGroupingInfo
        = new()
>>>>>>> theirs
        {
<<<<<<< ours
            ["WallsAndFlooring"] = ("rcd-component-walls-and-flooring", 0, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
            ["WindowsAndGrilles"] = ("rcd-component-windows-and-grilles", 1, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/windows_and_grilles.png"))),
            ["Airlocks"] = ("rcd-component-airlocks", 2, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/airlocks.png"))),
            ["Electrical"] = ("rcd-component-electrical", 3, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/multicoil.png"))),
            ["Lighting"] = ("rcd-component-lighting", 4, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/lighting.png"))),
            ["NecromorphTriggers"] = ("rcd-component-necromorph-triggers", 5, new SpriteSpecifier.Rsi(new ResPath("/Textures/Tiles/Misc/floortrap.rsi"), "floortrapspawn")),
||||||| base
            ["WallsAndFlooring"] = ("rcd-component-walls-and-flooring", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
            ["WindowsAndGrilles"] = ("rcd-component-windows-and-grilles", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/windows_and_grilles.png"))),
            ["Airlocks"] = ("rcd-component-airlocks", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/airlocks.png"))),
            ["Electrical"] = ("rcd-component-electrical", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/multicoil.png"))),
            ["Lighting"] = ("rcd-component-lighting", new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/lighting.png"))),
=======
            ["WallsAndFlooring"] = ("rcd-component-walls-and-flooring", 0, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"))),
            ["WindowsAndGrilles"] = ("rcd-component-windows-and-grilles", 1, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/windows_and_grilles.png"))),
            ["Airlocks"] = ("rcd-component-airlocks", 2, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/airlocks.png"))),
            ["Electrical"] = ("rcd-component-electrical", 3, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/multicoil.png"))),
            ["Lighting"] = ("rcd-component-lighting", 4, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/lighting.png"))),
>>>>>>> theirs
        };
    // DS14-end

    private SimpleRadialMenu? _menu;

    public RCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        var models = ConvertToButtons(rcd.AvailablePrototypes);
        _menu.SetButtons(models);

        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(HashSet<ProtoId<RCDPrototype>> prototypes)
    {
        Dictionary<string, List<RadialMenuOptionBase>> buttonsByCategory = new();
        ValueList<RadialMenuOptionBase> topLevelActions = new();
        foreach (var protoId in prototypes)
        {
            var prototype = _prototypeManager.Index(protoId);
            if (prototype.Category == TopLevelActionCategory)
            {
                var topLevelActionOption = new RadialMenuActionOption<RCDPrototype>(HandleMenuOptionClick, prototype)
                {
                    IconSpecifier = RadialMenuIconSpecifier.With(prototype.Sprite),
<<<<<<< ours
                    ToolTip = GetTooltip(prototype),
                    Order = prototype.MenuOrder, // DS14
||||||| base
                    ToolTip = GetTooltip(prototype)
=======
                    ToolTip = GetTooltip(prototype),
                    Order = prototype.MenuOrder,
>>>>>>> theirs
                };
                topLevelActions.Add(topLevelActionOption);
                continue;
            }

            if (!PrototypesGroupingInfo.TryGetValue(prototype.Category, out var groupInfo))
                continue;

            if (!buttonsByCategory.TryGetValue(prototype.Category, out var list))
            {
                list = new List<RadialMenuOptionBase>();
                buttonsByCategory.Add(prototype.Category, list);
            }

            var actionOption = new RadialMenuActionOption<RCDPrototype>(HandleMenuOptionClick, prototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(prototype.Sprite),
<<<<<<< ours
                ToolTip = GetTooltip(prototype),
                Order = prototype.MenuOrder, // DS14
||||||| base
                ToolTip = GetTooltip(prototype)
=======
                ToolTip = GetTooltip(prototype),
                Order = prototype.MenuOrder,
>>>>>>> theirs
            };
            list.Add(actionOption);
        }

        var models = new RadialMenuOptionBase[buttonsByCategory.Count + topLevelActions.Count];
        var i = 0;
        foreach (var (key, list) in buttonsByCategory)
        {
            var groupInfo = PrototypesGroupingInfo[key];
            models[i] = new RadialMenuNestedLayerOption(list)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(groupInfo.Sprite),
<<<<<<< ours
                ToolTip = Loc.GetString(groupInfo.Tooltip),
                Order = groupInfo.Order, // DS14
||||||| base
                ToolTip = Loc.GetString(groupInfo.Tooltip)
=======
                ToolTip = Loc.GetString(groupInfo.Tooltip),
                Order = groupInfo.Order,
>>>>>>> theirs
            };
            i++;
        }

        foreach (var action in topLevelActions)
        {
            models[i] = action;
            i++;
        }

        return models;
    }

    private void HandleMenuOptionClick(RCDPrototype proto)
    {
        // A predicted message cannot be used here as the RCD UI is closed immediately
        // after this message is sent, which will stop the server from receiving it
        SendMessage(new RCDSystemMessage(proto.ID));


        if (_playerManager.LocalSession?.AttachedEntity == null)
            return;

<<<<<<< ours
        var rcdName = GetPrototypeName(proto); // DS14

        var msg = Loc.GetString("rcd-component-change-mode", ("mode", rcdName));
||||||| base
        var msg = Loc.GetString("rcd-component-change-mode", ("mode", Loc.GetString(proto.SetName)));

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject)
        {
            var name = Loc.GetString(proto.SetName);

            if (proto.Prototype != null &&
                _prototypeManager.TryIndex(proto.Prototype, out var entProto)) // don't use Resolve because this can be a tile
            {
                name = entProto.Name;
            }
=======
        var rcdName = _rcd.GetPrototypeName(proto);

        var msg = Loc.GetString("rcd-component-change-mode", ("mode", rcdName));
>>>>>>> theirs

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject)
            msg = Loc.GetString("rcd-component-change-build-mode", ("name", rcdName));

        // Popup message
        _popup.PopupEntity(msg, Owner);
    }

    private string GetTooltip(RCDPrototype proto)
    {
<<<<<<< ours
        var tooltip = GetPrototypeName(proto); // DS14
||||||| base
        string tooltip;

        if (proto.Mode is RcdMode.ConstructTile or RcdMode.ConstructObject
            && proto.Prototype != null
            && _prototypeManager.TryIndex(proto.Prototype, out var entProto)) // don't use Resolve because this can be a tile
        {
            tooltip = Loc.GetString(entProto.Name);
        }
        else
        {
            tooltip = Loc.GetString(proto.SetName);
        }

=======
        var tooltip = _rcd.GetPrototypeName(proto);
>>>>>>> theirs
        tooltip = OopsConcat(char.ToUpper(tooltip[0]).ToString(), tooltip.Remove(0, 1));

        return tooltip;
    }

    // DS14-start: RCDSystem is not registered client-side on the current engine baseline.
    private string GetPrototypeName(RCDPrototype prototype)
    {
        if (prototype.SetName != null)
            return Loc.GetString(prototype.SetName);

        if (prototype.Prototype != null)
            return _prototypeManager.Index(prototype.Prototype).Name;

        return Loc.GetString("generic-unknown-title");
    }
    // DS14-end

    private static string OopsConcat(string a, string b)
    {
        // This exists to prevent Roslyn being clever and compiling something that fails sandbox checks.
        return a + b;
    }
}
