using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared.DeadSpace.UniformAccessories;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Containers;

namespace Content.Client.DeadSpace.UniformAccessories;

[UsedImplicitly]
public sealed class UniformAccessoryBui : BoundUserInterface
{
    [Dependency] private readonly IClyde _display = default!;
    [Dependency] private readonly IEyeManager _eye = default!;

    private readonly SharedContainerSystem _container;
    private readonly TransformSystem _xform;

    private UniformAccessoryMenu? _menu;

    public UniformAccessoryBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _xform = EntMan.System<TransformSystem>();
        _container = EntMan.System<SharedContainerSystem>();
    }

    protected override void Open()
    {
        base.Open();

        if (_menu == null)
        {
            _menu = this.CreateWindow<UniformAccessoryMenu>();
            _menu.OnClose += CloseInterface;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_menu == null)
            return;

        if (!EntMan.TryGetComponent(Owner, out Shared.DeadSpace.UniformAccessories.UniformAccessoryHolderComponent? holder))
            return;

        if (!_container.TryGetContainer(Owner, holder.ContainerId, out var container))
            return;

        _menu.Accessories.Children.Clear();

        foreach (var accessory in container.ContainedEntities)
        {
            if (!EntMan.TryGetComponent(accessory, out MetaDataComponent? meta))
                continue;

            var button = new RadialMenuTextureButton
            {
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64, 64),
                ToolTip = meta.EntityName
            };

            var spriteView = new SpriteView
            {
                OverrideDirection = Direction.South,
                Scale = new Vector2(2f, 2f),
                MaxSize = new Vector2(112, 112),
                Stretch = SpriteView.StretchMode.Fill
            };
            spriteView.SetEntity(accessory);

            button.AddChild(spriteView);

            var netEnt = EntMan.GetNetEntity(accessory);

            button.OnButtonDown += _ =>
            {
                // Отправка сообщения только если меню открыто
                if (_menu != null && _menu.Visible)
                    SendPredictedMessage(new UniformAccessoriesBuiMsg(netEnt));
            };

            _menu.Accessories.AddChild(button);
        }

        if (!_menu.Visible)
        {
            var vp = _display.ScreenSize;
            var pos = _eye.WorldToScreen(_xform.GetMapCoordinates(Owner).Position) / vp;
            _menu.OpenCenteredAt(pos);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is UniformAccessoriesBuiState)
            Refresh();
    }

    public void CloseInterface()
    {
        if (_menu == null)
            return;

        _menu.OnClose -= CloseInterface;
        _menu.Dispose();
        _menu = null;
    }
}
