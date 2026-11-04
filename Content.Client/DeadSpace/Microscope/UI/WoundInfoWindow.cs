// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.OrganDamage;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Client.DeadSpace.Microscope.UI;

public sealed class WoundInfoWindow : DefaultWindow
{
    public WoundInfoWindow(OrganWound wound)
    {
        var prototypes = IoCManager.Instance!.Resolve<IPrototypeManager>();
        var timing = IoCManager.Instance!.Resolve<IGameTiming>();

        Title = Loc.GetString("microscope-wound-window-title");
        SetSize = new Vector2(300, 200);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(10, 10, 10, 10),
            SeparationOverride = 6,
        };

        string damageTypeName;
        if (prototypes.TryIndex<DamageTypePrototype>(wound.DamageType, out var type))
            damageTypeName = type.LocalizedName;
        else
            damageTypeName = wound.DamageType;

        var elapsed = timing.CurTime - wound.Time;
        var source = wound.Source ?? Loc.GetString("microscope-wound-source-unknown");

        container.AddChild(MakeLabel(Loc.GetString("microscope-wound-damage-type", ("type", damageTypeName))));
        container.AddChild(MakeLabel(Loc.GetString("microscope-wound-amount", ("amount", wound.Amount.Float()))));
        container.AddChild(MakeLabel(Loc.GetString("microscope-wound-time", ("time", $"{elapsed:m\\:ss}"))));
        container.AddChild(MakeLabel(Loc.GetString("microscope-wound-source", ("source", source))));

        AddChild(container);
    }

    private static Label MakeLabel(string text)
    {
        return new Label { Text = text };
    }
}
