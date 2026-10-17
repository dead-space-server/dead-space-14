// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio;

namespace Content.Server.DeadSpace.Autopsy
{
    [RegisterComponent]
    public sealed partial class AutopsyScannerComponent : Component
    {
        [DataField]
        public float ScanDelay = 2f;

        [DataField]
        public SoundSpecifier ScanningSound;
    }
}
