using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Shared.Shuttles.Systems;

public abstract partial class SharedEmergencyShuttleSystem : EntitySystem
{
    [Dependency] protected AccessReaderSystem AccessReader = default!; // DS14
    [Dependency] protected IConfigurationManager ConfigManager = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    protected bool EmergencyEarlyLaunchAllowed; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmergencyShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(OnEmergencyOpenAttempt);

        Subs.CVar(ConfigManager, CCVars.EmergencyEarlyLaunchAllowed, value => EmergencyEarlyLaunchAllowed = value, true); // DS14
    }

    private void OnEmergencyOpenAttempt(Entity<EmergencyShuttleConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // DS14-start
        // DS14: Traitor Ultra hijack uses the same console even when early launch is disabled.
        // Server-side handlers still reject early launch authorization messages when the CVar is off.
        if (AccessReader.IsAllowed(args.User, ent))
            return;

        if (!args.Silent)
            Popup.PopupEntity(Loc.GetString("emergency-shuttle-console-denied"), ent, args.User);

        args.Cancel();
        // DS14-end
    }
}
