// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Vending;

/// <summary>
/// Notices a switch of the interface style, or of the projection colour or opacity. Style boxes and colours set from
/// code are not rebuilt with the stylesheet, so a window checks this once a frame and re-applies its own when it
/// changes. The first check always reports a change.
/// </summary>
public sealed class HologramStyleStamp
{
    private bool _initialized;
    private bool _hologram;
    private Color _accent;
    private Color _surface;

    public bool Hologram => _hologram;

    public bool Update()
    {
        var hologram = DeadSpaceStylePalette.Hologram;
        var accent = DeadSpaceStylePalette.BorderHeader;
        var surface = DeadSpaceStylePalette.Surface;

        if (_initialized && hologram == _hologram && accent == _accent && surface == _surface)
            return false;

        _initialized = true;
        _hologram = hologram;
        _accent = accent;
        _surface = surface;
        return true;
    }
}
