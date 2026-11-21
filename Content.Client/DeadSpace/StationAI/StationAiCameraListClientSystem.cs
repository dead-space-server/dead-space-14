// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.StationAI.UI;
using Content.Shared.DeadSpace.StationAI.UI;
using Content.Shared.DeadSpace.StationAi;

namespace Content.Client.DeadSpace.StationAI;

public sealed partial class StationAiCameraListClientSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiEyeComponent, AfterAutoHandleStateEvent>(OnAiEyeStateHandled);
    }

    private void OnAiEyeStateHandled(Entity<AiEyeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!_ui.TryGetOpenUi<AICameraListBoundUserInterface>(ent.Owner, AICameraListUiKey.Key, out var bui))
            return;

        bui.UpdateCameras();
    }
}
