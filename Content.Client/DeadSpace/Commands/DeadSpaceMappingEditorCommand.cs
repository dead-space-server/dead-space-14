using System.Numerics;
using Content.Client.Movement.Systems;
using Content.Shared.Movement.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client.DeadSpace.Commands;

public sealed class DeadSpaceMappingEditorCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override string Command => "mappingeditor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        shell.ExecuteCommand("mappingclientsidesetup");

        var zoom = new Vector2(1.5f, 1.5f);
        var player = _playerManager.LocalSession?.AttachedEntity;

        if (player != null &&
            _entityManager.TryGetComponent<ContentEyeComponent>(player, out var contentEye))
        {
            _entityManager.System<ContentEyeSystem>().RequestZoom(player.Value, zoom, true, true, contentEye);
        }
        else
        {
            _eyeManager.CurrentEye.Zoom = zoom;
        }

        shell.ExecuteCommand("scene DeadSpaceMappingState");
    }
}
