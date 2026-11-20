// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ERT.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.ERT;

public sealed partial class SharedErtResponseConsoleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ErtResponseConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
    }

    private void OnUiOpenAttempt(Entity<ErtResponseConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.IsAuthorized)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupPredicted(Loc.GetString("ert-console-auth-required"), ent, args.User);
    }
}
