using Content.Shared.Verbs;
using Robust.Shared.Player;
using Content.Server.Administration;
using Content.Server.Prayer;
using Content.Shared.Revenant.Components;
using Robust.Shared.Utility;
using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Server.DeadSpace.Languages;
using Content.Shared.Chat;

namespace Content.Server.Revenant;

public sealed partial class TelepathySystem : EntitySystem
{
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private PrayerSystem _prayerSystem = default!;
    [Dependency] private LanguageSystem _language = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(GetVerbs);
    }

    private void GetVerbs(GetVerbsEvent<Verb> ev)
    {
        AddRevenantVerbs(ev);
    }

    private void AddRevenantVerbs(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor) || args.User == args.Target)
            return;

        var player = actor.PlayerSession;

        if (HasComp<RevenantComponent>(args.User))
        {
            if (TryComp(args.Target, out ActorComponent? targetActor))
            {
                // Subtle Messages
                Verb telepathy = new();
                telepathy.Text = Loc.GetString("prayer-verbs-subtle-message-revenant");
                telepathy.Priority = -3;
                telepathy.DoContactInteraction = true;
                telepathy.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/vv.svg.192dpi.png"));
                telepathy.Act = () =>
                {
                    _quickDialog.OpenDialog(player, "Пробраться в мысли", "Сообщение", (string message) =>
                    {
                        var lexiconMessage = message;

                        if (TryComp<LanguageComponent>(args.User, out var language))
                            lexiconMessage = _language.TransformWord(message, language.SelectedLanguage);

                        var selectedLanguage = language != null ? language.SelectedLanguage : LanguageSystem.DefaultLanguageId.ToString();

                        _prayerSystem.SendSubtleMessage(targetActor.PlayerSession, player, message, Loc.GetString("prayer-popup-subtle-revenant"));
                        var ev = new EntitySpokeToEntityEvent(args.Target, message, lexiconMessage, selectedLanguage);
                        RaiseLocalEvent(args.User, ev, true);
                    });
                };
                args.Verbs.Add(telepathy);
            }
        }
    }
}

