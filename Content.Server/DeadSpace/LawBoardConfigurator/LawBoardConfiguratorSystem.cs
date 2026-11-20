using Content.Server.EUI;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Server.Silicons.Laws;
using Content.Shared.DeadSpace.LawBoardConfigurator.Components;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.DeadSpace.LawBoardConfigurator;

public sealed partial class LawBoardConfiguratorSystem : EntitySystem
{
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SiliconLawSystem _siliconLaw = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private readonly Dictionary<EntityUid, Dictionary<ICommonSession, LawBoardConfiguratorEui>> _openEuis = new();

    // session utilities ----------------------------------------------------
    private static bool TryGetAttachedEntity(ICommonSession session, out EntityUid entity)
    {
        if (session.AttachedEntity is { } attached)
        {
            entity = attached;
            return true;
        }

        entity = default;
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // close any open UIs if the controlling player walks out of range
        if (_openEuis.Count == 0)
            return;

        var consoles = _openEuis.Keys.ToArray();
        foreach (var console in consoles)
        {
            if (!_openEuis.TryGetValue(console, out var euis))
                continue;

            foreach (var (session, eui) in euis.ToArray())
            {
                if (!TryGetAttachedEntity(session, out var user))
                {
                    eui.Close();
                    euis.Remove(session);
                    continue;
                }

                if (!Exists(user) || !_interaction.InRangeUnobstructed(user, console))
                {
                    eui.Close();
                    euis.Remove(session);
                }
            }

            if (euis.Count == 0)
                _openEuis.Remove(console);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, EntInsertedIntoContainerMessage>(OnBoardSlotChanged);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, EntRemovedFromContainerMessage>(OnBoardSlotChanged);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, ContainerModifiedMessage>(OnBoardSlotChanged);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, GetVerbsEvent<ActivationVerb>>(OnGetActivationVerbs);
        SubscribeLocalEvent<LawBoardConfiguratorConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
    }

    private void OnActivateInWorld(EntityUid uid, LawBoardConfiguratorConsoleComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (TryOpenUi(uid, component, args.User))
            args.Handled = true;
    }

    private void OnInteractHand(EntityUid uid, LawBoardConfiguratorConsoleComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryOpenUi(uid, component, args.User);
    }

    private void OnGetActivationVerbs(EntityUid uid, LawBoardConfiguratorConsoleComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Act = () => TryOpenUi(uid, component, args.User),
            Text = Loc.GetString("ui-verb-toggle-open"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
        });
    }

    private void OnPowerChanged(EntityUid uid, LawBoardConfiguratorConsoleComponent component, ref PowerChangedEvent args)
    {
        if (args.Powered)
            return;

        CloseAllEuis(uid);
    }

    private bool TryOpenUi(EntityUid uid, LawBoardConfiguratorConsoleComponent component, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return false;

        if (_openEuis.TryGetValue(uid, out var openedBySession))
        {
            ICommonSession? keyToRemove = null;
            LawBoardConfiguratorEui? openedEui = null;

            foreach (var (session, openUi) in openedBySession)
            {
                if (session == actor.PlayerSession || session.UserId == actor.PlayerSession.UserId)
                {
                    keyToRemove = session;
                    openedEui = openUi;
                    break;
                }
            }

            if (keyToRemove != null && openedEui != null)
            {
                openedBySession.Remove(keyToRemove);
                openedEui.Close();

                if (openedBySession.Count == 0)
                    _openEuis.Remove(uid);

                return true;
            }
        }

        if (!_interaction.InRangeUnobstructed(user, uid))
            return false;

        if (!this.IsPowered(uid, EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("base-computer-ui-component-not-powered", ("machine", uid)), uid, user);
            return false;
        }

        if (TryComp<LockComponent>(uid, out var lockComp) && lockComp.Locked)
        {
            _popup.PopupEntity(Loc.GetString("law-board-configurator-locked"), uid, user);
            return false;
        }

        var eui = new LawBoardConfiguratorEui(
            _siliconLaw,
            EntityManager,
            _itemSlots,
            _popup,
            _metaData,
            uid,
            component.BoardSlot,
            closed => OnEuiClosed(uid, actor.PlayerSession, closed));
        _eui.OpenEui(eui, actor.PlayerSession);
        eui.RefreshFromConsole();

        if (!_openEuis.TryGetValue(uid, out var euis))
        {
            euis = new();
            _openEuis[uid] = euis;
        }

        euis[actor.PlayerSession] = eui;

        try
        {
            _audio.PlayPvs(component.OpenSound, uid);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to play open sound for {ToPrettyString(uid)}: {e}");
        }

        return true;
    }

    private void OnBoardSlotChanged(EntityUid uid, LawBoardConfiguratorConsoleComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.BoardSlot)
            return;

        RefreshAllEuis(uid);
    }

    private void OnConsoleShutdown(EntityUid uid, LawBoardConfiguratorConsoleComponent component, ComponentShutdown args)
    {
        CloseAllEuis(uid);
    }

    private void CloseAllEuis(EntityUid uid)
    {
        if (!_openEuis.TryGetValue(uid, out var euis))
            return;

        foreach (var eui in euis.Values.ToArray())
        {
            eui.Close();
        }

        _openEuis.Remove(uid);
    }

    private void RefreshAllEuis(EntityUid uid)
    {
        if (!_openEuis.TryGetValue(uid, out var euis))
            return;

        foreach (var eui in euis.Values.ToArray())
        {
            eui.RefreshFromConsole();
        }
    }

    private void OnEuiClosed(EntityUid console, ICommonSession session, LawBoardConfiguratorEui eui)
    {
        if (!_openEuis.TryGetValue(console, out var euis))
            return;

        if (!euis.TryGetValue(session, out var openEui) || openEui != eui)
            return;

        euis.Remove(session);
        if (euis.Count == 0)
            _openEuis.Remove(console);
    }
}
