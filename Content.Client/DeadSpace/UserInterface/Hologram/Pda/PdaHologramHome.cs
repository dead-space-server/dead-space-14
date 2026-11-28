// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.GameTicking.Managers;
using Content.Client.Message;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Pda;

/// <summary>
/// Home screen of the PDA in the "Dead Space" style: the owner card with the inserted ID card, the shift clock, the
/// station alert level, the program tiles and the alert instructions. It shows only what the PDA state already
/// carries; every value can still be copied by clicking it, as on the stock home screen.
/// </summary>
public sealed partial class PdaHologramHome : BoxContainer
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly ClientGameTicker _gameTicker;
    private readonly ItemSlotsSystem _itemSlots;

    private readonly SpriteView _idView;
    private readonly Label _nameLabel;
    private readonly Label _idLabel;
    private readonly Label _clockLabel;
    private readonly Label _dateLabel;
    private readonly Label _alertLabel;
    private readonly Label _stationLabel;
    private readonly BoxContainer _programsSection;
    private readonly RichTextLabel _instructionsLabel;
    private readonly StyleBoxFlat _alertBox = new()
    {
        BorderThickness = new Thickness(1),
        ContentMarginLeftOverride = 8,
        ContentMarginRightOverride = 8,
        ContentMarginTopOverride = 2,
        ContentMarginBottomOverride = 2,
    };

    private string _name = string.Empty;
    private string _idCopy = string.Empty;
    private string _station = string.Empty;
    private string _alert = string.Empty;
    private string _instructions = string.Empty;
    private string _time = string.Empty;
    private long _shownSecond = long.MinValue;

    /// <summary>Program tiles of the home screen, with the install and uninstall buttons of the program list.</summary>
    public readonly PdaHologramProgramGrid Programs;

    /// <summary>"PDA · OWNER" for the window header.</summary>
    public string WindowTitle { get; private set; } = string.Empty;

    public PdaHologramHome()
    {
        IoCManager.InjectDependencies(this);
        _gameTicker = _entityManager.System<ClientGameTicker>();
        _itemSlots = _entityManager.System<ItemSlotsSystem>();

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        Margin = new Thickness(2, 6, 2, 8);
        SeparationOverride = 8;

        // Owner card: ID card, name and job, shift clock.
        _idView = new SpriteView
        {
            SetSize = new Vector2(64, 64),
            Stretch = SpriteView.StretchMode.Fill,
            OverrideDirection = Direction.South,
            VerticalAlignment = VAlignment.Center,
            Visible = false,
        };
        _nameLabel = new Label { ClipText = true, StyleClasses = { PdaHologramStyleClass.OwnerName } };
        _idLabel = new Label { ClipText = true, StyleClasses = { PdaHologramStyleClass.Meta } };
        _clockLabel = new Label { Align = Label.AlignMode.Right, StyleClasses = { PdaHologramStyleClass.Clock } };
        _dateLabel = new Label { Align = Label.AlignMode.Right, StyleClasses = { PdaHologramStyleClass.Meta } };

        var nameRow = CopyRow(_nameLabel, () => _name);
        nameRow.HorizontalExpand = true;
        var idRow = CopyRow(_idLabel, () => _idCopy);
        idRow.HorizontalExpand = true;
        var clockRow = CopyRow(_clockLabel, () => _time);
        clockRow.HorizontalAlignment = HAlignment.Right;

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 14,
            Children =
            {
                _idView,
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    SeparationOverride = 2,
                    Children = { nameRow, idRow },
                },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalAlignment = VAlignment.Center,
                    Children = { clockRow, _dateLabel },
                },
            },
        });

        AddChild(Divider());

        // Alert level and station.
        _alertLabel = new Label { StyleClasses = { PdaHologramStyleClass.AlertText } };
        var alertTag = new PanelContainer
        {
            PanelOverride = _alertBox,
            VerticalAlignment = VAlignment.Center,
            Children = { _alertLabel },
        };
        _stationLabel = new Label
        {
            ClipText = true,
            Align = Label.AlignMode.Right,
            HorizontalExpand = true,
            StyleClasses = { PdaHologramStyleClass.FieldLabel },
        };
        var stationRow = CopyRow(_stationLabel, () => _station);
        stationRow.HorizontalExpand = true;
        stationRow.VerticalAlignment = VAlignment.Center;
        var alertRow = CopyRow(alertTag, () => _alert);
        alertRow.VerticalAlignment = VAlignment.Center;

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("pda-hologram-alert-level").ToUpperInvariant(),
                    VerticalAlignment = VAlignment.Center,
                    StyleClasses = { PdaHologramStyleClass.FieldLabel },
                },
                alertRow,
                stationRow,
            },
        });

        // Program tiles.
        Programs = new PdaHologramProgramGrid();
        _programsSection = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Visible = false,
            Children = { Divider(), Programs },
        };
        AddChild(_programsSection);

        // Instructions of the alert level.
        _instructionsLabel = new RichTextLabel { HorizontalExpand = true };
        var instructionsRow = CopyRow(_instructionsLabel, () => _instructions);
        instructionsRow.HorizontalExpand = true;
        AddChild(Divider());
        AddChild(new Label
        {
            Text = Loc.GetString("pda-hologram-instructions").ToUpperInvariant(),
            StyleClasses = { PdaHologramStyleClass.FieldLabel },
        });
        AddChild(instructionsRow);
    }

    /// <summary>Fills the screen from the PDA state; <paramref name="pda"/> is used only to show the inserted ID card.</summary>
    public void UpdateState(PdaUpdateState state, EntityUid? pda)
    {
        var info = state.PdaOwnerInfo;
        var unknown = Loc.GetString("comp-pda-ui-unknown");

        _name = info.ActualOwnerName ?? info.IdOwner ?? unknown;
        _nameLabel.Text = _name;
        WindowTitle = Loc.GetString("pda-hologram-window-title", ("owner", _name)).ToUpperInvariant();

        if (info.IdOwner != null || info.JobTitle != null)
        {
            var owner = info.IdOwner ?? unknown;
            var job = info.JobTitle ?? Loc.GetString("comp-pda-ui-unassigned");
            _idCopy = owner + ", " + job;
            _idLabel.Text = Loc.GetString("pda-hologram-id-line", ("job", job), ("owner", owner)).ToUpperInvariant();
        }
        else
        {
            _idCopy = string.Empty;
            _idLabel.Text = Loc.GetString("pda-hologram-id-missing").ToUpperInvariant();
        }

        EntityUid? idCard = null;
        if (pda != null && _itemSlots.TryGetSlot(pda.Value, PdaComponent.PdaIdSlotId, out var slot))
            idCard = slot.Item;
        _idView.SetEntity(idCard);
        _idView.Visible = idCard != null;

        _station = state.StationName ?? unknown;
        _stationLabel.Text = Loc.GetString("pda-hologram-station", ("station", _station)).ToUpperInvariant();

        var alertKey = info.StationAlertLevel != null ? $"alert-level-{info.StationAlertLevel}" : "alert-level-unknown";
        _alert = Loc.GetString(alertKey);
        _instructions = Loc.GetString($"{alertKey}-instructions");

        // The level's own colour, lifted a little so dark ones (dark red, dark violet) stay readable on the glass.
        var alertColor = Color.InterpolateBetween(info.StationAlertColor, Color.White, 0.3f);
        _alertLabel.Text = _alert.ToUpperInvariant();
        _alertLabel.FontColorOverride = alertColor;
        _alertBox.BorderColor = alertColor;
        _alertBox.BackgroundColor = alertColor.WithAlpha(0.1f);

        _instructionsLabel.SetMarkupPermissive(_instructions);
    }

    public void SetPrograms(
        IReadOnlyList<(EntityUid Uid, CartridgeComponent Component)> programs,
        Action<EntityUid> onPressed,
        Action<EntityUid> onInstall,
        Action<EntityUid> onUninstall)
    {
        Programs.Populate(programs, onPressed, onInstall, onUninstall);
        _programsSection.Visible = programs.Count > 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var shiftTime = _timing.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
        var second = (long) Math.Floor(shiftTime.TotalSeconds);
        if (second == _shownSecond)
            return;

        _shownSecond = second;
        _time = shiftTime.ToString("hh\\:mm\\:ss");
        _clockLabel.Text = _time;
        _dateLabel.Text = Loc.GetString("pda-hologram-shift",
                ("date", DateTime.UtcNow.AddHours(3).ToString("dd.MM") + ".2710"))
            .ToUpperInvariant();
    }

    private ContainerButton CopyRow(Control content, Func<string> text)
    {
        var button = new ContainerButton { StyleClasses = { PdaHologramStyleClass.CopyRow } };
        button.AddChild(content);
        button.OnPressed += _ =>
        {
            var value = text();
            if (!string.IsNullOrEmpty(value))
                _clipboard.SetText(value);
        };
        return button;
    }

    private static PanelContainer Divider()
    {
        return new PanelContainer
        {
            MinHeight = 1,
            HorizontalExpand = true,
            StyleClasses = { PdaHologramStyleClass.Divider },
        };
    }
}
