using System.Linq;
using Content.Shared.BarSign;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.BarSign.Ui;

[UsedImplicitly]
public sealed partial class BarSignBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private BarSignMenu? _menu;

    protected override void Open()
    {
        // DS14-start
        if (IsOpened)
            return;
        // DS14-end

        base.Open();

        var allSigns = BarSignSystem.GetAllBarSigns(_prototype)
            .OrderBy(p => Loc.GetString(p.Name))
            .ToList();

        _menu = this.CreateWindow<BarSignMenu>();
        _menu.LoadSigns(allSigns);

        _menu.OnSignSelected += id =>
        {
            SendPredictedMessage(new SetBarSignMessage(id));
        };

        _menu.OnClose += Close;
        _menu.OpenToLeft();
    }

    public override void Update()
    {
        if (!EntMan.TryGetComponent<BarSignComponent>(Owner, out var signComp)
            || !_prototype.Resolve(signComp.Current, out var signPrototype))
            return;

        _menu?.UpdateState(signPrototype);
    }

    // DS14-start
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _menu?.Dispose();
        _menu = null;
    }
    // DS14-end
}

