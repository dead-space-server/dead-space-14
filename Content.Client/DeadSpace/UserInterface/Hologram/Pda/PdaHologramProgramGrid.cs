// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Pda;

/// <summary>
/// The PDA programs in the "Dead Space" style: four columns of <see cref="PdaHologramProgramTile"/>.
/// </summary>
public sealed class PdaHologramProgramGrid : GridContainer
{
    public PdaHologramProgramGrid()
    {
        Columns = 4;
        HorizontalExpand = true;
        HSeparationOverride = 8;
        VSeparationOverride = 8;
    }

    /// <summary>
    /// Rebuilds the tiles. Install and uninstall buttons appear only when their callbacks are given. Every action
    /// goes through the PDA's usual cartridge messages, which the server checks.
    /// </summary>
    public void Populate(
        IReadOnlyList<(EntityUid Uid, CartridgeComponent Component)> programs,
        Action<EntityUid> onPressed,
        Action<EntityUid>? onInstall = null,
        Action<EntityUid>? onUninstall = null)
    {
        RemoveAllChildren();

        foreach (var (uid, component) in programs)
        {
            var tile = new PdaHologramProgramTile();

            if (component.Icon is not null)
                tile.Icon.SetFromSpriteSpecifier(component.Icon);

            tile.SetName(Loc.GetString(component.ProgramName));
            tile.OnPressed += _ => onPressed(uid);

            switch (component.InstallationStatus)
            {
                case InstallationStatus.Cartridge when onInstall != null:
                    tile.InstallButton.Visible = true;
                    tile.InstallButton.Text = Loc.GetString("cartridge-bound-user-interface-install-button");
                    tile.InstallButton.OnPressed += _ => onInstall(uid);
                    break;
                case InstallationStatus.Installed when onUninstall != null:
                    tile.InstallButton.Visible = true;
                    tile.InstallButton.Text = Loc.GetString("cartridge-bound-user-interface-uninstall-button");
                    tile.InstallButton.OnPressed += _ => onUninstall(uid);
                    break;
            }

            AddChild(tile);
        }

        // Keep the four columns even when there are fewer programs than that.
        for (var i = programs.Count; i < Columns; i++)
        {
            AddChild(new Control { HorizontalExpand = true, MinWidth = 96 });
        }
    }
}
