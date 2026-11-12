using Content.Server.Power.Components; // DS14
using Content.Shared.Doors.Components; // DS14
using Content.Shared.Doors.Systems;

namespace Content.Server.Doors.Systems;

public sealed partial class AirlockSystem : SharedAirlockSystem
{
    // DS14-start
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AirlockComponent, ComponentInit>(OnAirlockInit);
    }

    private void OnAirlockInit(EntityUid uid, AirlockComponent component, ComponentInit args)
    {
        if (TryComp<ApcPowerReceiverComponent>(uid, out var receiverComponent))
        {
            Appearance.SetData(uid, DoorVisuals.Powered, receiverComponent.Powered);
            Appearance.SetData(uid, DoorVisuals.ClosedLights, true); // Corvax-Resprite-Airlocks
        }
    }
    // DS14-end
}
