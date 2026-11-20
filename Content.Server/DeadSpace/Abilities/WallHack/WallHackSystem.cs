using Content.Shared.DeadSpace.Abilities.WallHack.Components;

namespace Content.Server.DeadSpace.Abilities.WallHack;

public sealed partial class WallHackSystem : EntitySystem
{
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WallHackComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<WallHackComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnComponentInit(EntityUid uid, WallHackComponent component, ComponentInit args)
    {
        if (TryComp<EyeComponent>(uid, out var eyeComp))
        {
            _eye.SetDrawLight((uid, eyeComp), false);
        }
    }

    private void OnShutdown(EntityUid uid, WallHackComponent component, ComponentShutdown args)
    {
        if (TryComp<EyeComponent>(uid, out var eyeComp))
        {
            _eye.SetDrawLight((uid, eyeComp), true);
        }
    }
}
