using Content.Server.Administration.Managers;
using Content.Server.Preferences.Managers;
using Content.Shared.DeadSpace.AghostColor;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.AghostColor;

public sealed class AghostColorSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AghostColorComponent, PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(EntityUid uid, AghostColorComponent component, PlayerAttachedEvent args)
    {
        var session = args.Player;

        if (!_adminManager.IsAdmin(session))
            return;

        var prefs = _preferencesManager.GetPreferences(session.UserId);
        var colorOverride = prefs.AdminOOCColor;
        component.Color = colorOverride;

        Dirty(uid, component); // чтобы клиент получил обновление
    }
}
