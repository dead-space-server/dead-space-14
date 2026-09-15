using System.Diagnostics.CodeAnalysis;
using Content.Shared.DeadSpace.CustomGhosts;
using Content.Shared.Ghost;
using Content.Shared.GhostTypes;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.CustomGhosts;

public sealed class CustomGhostSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedGhostSystem _ghostSystem = default!;

    public void TryMakeCustomGhost(EntityUid uid)
    {
        if (!TryGetCustomGhost(uid, out var ghost))
            return;

        PrepareGhost(uid, ghost);
    }

    private void PrepareGhost(EntityUid uid, CustomGhostPrototype ghost)
    {
        _appearance.RemoveData(uid, GhostVisuals.Damage);
        RemCompDeferred<GhostSpriteStateComponent>(uid);

        var color = ghost.Alpha > 0
            ? ghost.Color.WithAlpha(ghost.Alpha)
            : ghost.Color;

        _ghostSystem.SetGhostColor(uid, color);
        _appearance.SetData(uid, CustomGhostAppearance.Sprite, ghost.SpritePath.ToString());
        _appearance.SetData(uid, CustomGhostAppearance.ColorOverride, color);

        if (!string.IsNullOrWhiteSpace(ghost.Name))
        {
            _metaDataSystem.SetEntityName(uid, ghost.Name);
        }

        if (!string.IsNullOrWhiteSpace(ghost.Description))
        {
            _metaDataSystem.SetEntityDescription(uid, ghost.Description);
        }
    }

    private bool TryGetCustomGhost(
        EntityUid uid,
        [NotNullWhen(true)] out CustomGhostPrototype? ghost)
    {
        ghost = null;

        if (!_player.TryGetSessionByEntity(uid, out var session))
            return false;

        var login = session.Name;
        foreach (var prototype in _prototypeManager.EnumeratePrototypes<CustomGhostPrototype>())
        {
            if (!string.Equals(
                    prototype.Login,
                    login,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            ghost = prototype;
            return true;
        }

        return false;
    }
}
