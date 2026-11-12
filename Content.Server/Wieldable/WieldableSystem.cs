using Content.Server.Movement.Components;
using Content.Server.Movement.Systems;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Movement.Components;
using Content.Shared.Wieldable;

namespace Content.Server.Wieldable;

public sealed partial class WieldableSystem : SharedWieldableSystem
{
    [Dependency] private ContentEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, GotEquippedHandEvent>(OnEyeOffsetHandEquipped);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, GotUnequippedHandEvent>(OnEyeOffsetHandUnequipped);
        SubscribeLocalEvent<CursorOffsetRequiresWieldComponent, HeldRelayedEvent<GetEyePvsScaleRelayedEvent>>(OnGetEyePvsScale);
    }

    private void OnEyeOffsetHandEquipped(Entity<CursorOffsetRequiresWieldComponent> entity, ref GotEquippedHandEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnEyeOffsetHandUnequipped(Entity<CursorOffsetRequiresWieldComponent> entity, ref GotUnequippedHandEvent args)
    {
        _eye.UpdatePvsScale(args.User);
    }

    private void OnGetEyePvsScale(Entity<CursorOffsetRequiresWieldComponent> entity,
        ref HeldRelayedEvent<GetEyePvsScaleRelayedEvent> args)
    {
        if (!TryComp(entity, out EyeCursorOffsetComponent? eyeCursorOffset))
            return;

        args.Args.Scale += eyeCursorOffset.PvsIncrease;
    }
}
