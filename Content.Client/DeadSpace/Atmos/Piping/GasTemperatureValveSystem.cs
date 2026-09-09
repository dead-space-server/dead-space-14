// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Atmos.UI;
using Content.Shared.DeadSpace.Atmos.Piping;

namespace Content.Client.DeadSpace.Atmos.Piping;

public sealed class GasTemperatureValveSystem : SharedGasTemperatureValveSystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GasTemperatureValveComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<GasTemperatureValveComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent);
    }

    protected override void UpdateUi(Entity<GasTemperatureValveComponent> ent)
    {
        if (_ui.TryGetOpenUi(ent.Owner, GasTemperatureValveUiKey.Key, out var bui) && bui is GasTemperatureValveBoundUserInterface valveBui)
            valveBui.Update();
    }
}
