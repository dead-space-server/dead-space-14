using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mobs.Components;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client.DeadSpace;

public sealed partial class LightHandleSystem : EntitySystem
{
    [Dependency] private ILightManager _light = default!;
    [Dependency] private IClientConGroupController _conGroup = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_light is { Enabled: true, DrawShadows: true, DrawHardFov: true, DrawLighting: true })
        {
            return;
        }

        if (_conGroup.CanAdminPlace())
        {
            return;
        }

        var plr = _playerManager.LocalSession?.AttachedEntity;
        if (plr == null)
            return;
        if (!HasComp<MobStateComponent>(plr))
            return;
        if (TryComp<BlindableComponent>(plr, out var blindableComponent) && blindableComponent.LightSetup)
            return;

        _light.Enabled = true;
        _light.DrawShadows = true;
        _light.DrawHardFov = true;
        _light.DrawLighting = true;
    }
}
