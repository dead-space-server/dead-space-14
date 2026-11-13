// Dead Space 14, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Shared.DeadSpace.CameraArchives;

public sealed partial class CameraArchiveSheetLimitSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CameraArchivePrintComponent, ActivatableUIOpenAttemptEvent>(OnOpen);
    }

    private void OnOpen(Entity<CameraArchivePrintComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.Still.Length == 0)
            return;

        var open = EntityQueryEnumerator<CameraArchivePrintComponent>();
        while (open.MoveNext(out var uid, out var shot))
        {
            if (uid == ent.Owner || shot.Still.Length == 0)
                continue;

            if (!_ui.IsUiOpen(uid, PaperComponent.PaperUiKey.Key, args.User))
                continue;

            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("camera-archive-one-sheet"), ent, args.User);

            args.Cancel();
            return;
        }
    }
}
