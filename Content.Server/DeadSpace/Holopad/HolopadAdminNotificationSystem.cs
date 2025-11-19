// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Audio.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Administration.Managers;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared.Telephone;
using Content.Shared.Holopad;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Server.DeadSpace.Holopad;

public sealed class HolopadAdminNotificationSystem: EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;

    private static readonly ProtoId<TagPrototype> HolopadAdminNotificationTag = "HolopadAdminNotification";
    public override void Initialize()
    {
            base.Initialize();
            SubscribeLocalEvent<HolopadComponent, TelephoneCallAttemptEvent>(onTelephoneCallAttempt);
    }

    private void onTelephoneCallAttempt(Entity<HolopadComponent> source, ref TelephoneCallAttemptEvent args)
    {
        if (_tagSystem.HasTag(args.Receiver, HolopadAdminNotificationTag))
        {
            string userName = Loc.GetString("holopad-unknown-player");

            if (args.User != null && TryComp<MetaDataComponent>(args.User, out var meta))
            {
                userName = meta.EntityName;
            }

            NotifyAdmins(Name(source), userName);

        }
    }
    private void NotifyAdmins(string holoName, string user)
    {
        _chat.SendAdminAnnouncement(Loc.GetString("holopad-chat-notify", ("holopad", holoName), ("user", user)));
        _audioSystem.PlayGlobal("/Audio/Machines/high_tech_confirm.ogg", Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(-8f));
    }
}
