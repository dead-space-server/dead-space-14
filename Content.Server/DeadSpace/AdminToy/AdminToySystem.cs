// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Lightning;
using Content.Server.Mind;
using Content.Server.Speech;
using Content.Shared.Administration;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Corvax.TTS;
using Content.Shared.DeadSpace.AdminToy;
using Content.Shared.DeadSpace.Languages.Components;
using Content.Shared.DeadSpace.Languages.Prototypes;
using Content.Shared.GameTicking;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;
using LanguageSystem = Content.Server.DeadSpace.Languages.LanguageSystem;

namespace Content.Server.DeadSpace.AdminToy;

public sealed class AdminToySystem : EntitySystem
{
    private const string AdminToyPrototype = "MobAdminToy";
    private const int PrivateLayerFirstBit = 5;
    private const int PrivateLayerLastBit = 15;
    private const int MaxToyNameLength = 80;
    private const int MaxToyDescriptionLength = 512;
    private const string DefaultToyName = "Дух нудной работы";
    private const string DefaultToyDescription = "Дух рабочего, что некогда служил на этой станции и сгорел на работе... Или он просто съел пельмени от Джониты Райтмен... Кто знает?..";

    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedRgbLightControllerSystem _rgb = default!;
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;

    private readonly HashSet<ushort> _usedPrivateLayers = new();
    private readonly Dictionary<ICommonSession, HashSet<ushort>> _sessionLayers = new();
    private readonly Dictionary<NetUserId, EntityUid> _adminToys = new();
    private int _nextConstructionGhostId = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminToyComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AdminToyComponent, MindUnvisitedMessage>(OnMindUnvisited);
        SubscribeLocalEvent<AdminToyComponent, AdminToyBooActionEvent>(OnBooAction);
        SubscribeLocalEvent<AdminToyComponent, AdminToyLightningActionEvent>(OnLightningAction);
        SubscribeLocalEvent<AdminToyComponent, TransformSpeechEvent>(OnTransformSpeech, after: [typeof(AccentSystem)]);
        SubscribeLocalEvent<GetVisMaskEvent>(OnGetVisibility);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        SubscribeNetworkEvent<AdminToyPlaceConstructionGhostRequest>(OnPlaceConstructionGhost);
        SubscribeNetworkEvent<AdminToyClearConstructionGhostRequest>(OnClearConstructionGhost);
        SubscribeNetworkEvent<AdminToyClearAllConstructionGhostsRequest>(OnClearAllConstructionGhosts);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    public bool CanTarget(EntityUid target)
    {
        return TryGetTargetSession(target, out _);
    }

    public bool CanUse(ICommonSession admin)
    {
        return _admin.HasAdminFlag(admin, AdminFlags.Admin);
    }

    public void OpenSelection(ICommonSession admin, EntityUid target)
    {
        if (!CanUse(admin))
        {
            PopupTo(admin, Loc.GetString("admin-toy-no-access"));
            return;
        }

        if (!TryGetTargetSession(target, out _))
        {
            PopupTo(admin, Loc.GetString("admin-toy-invalid-target"));
            return;
        }

        _eui.OpenEui(new AdminToySelectionEui(GetNetEntity(target)), admin);
    }

    public void TrySpawnToy(ICommonSession admin, EntityUid target, string toyPrototype, string name, string description, string ttsVoice)
    {
        if (!CanUse(admin))
        {
            PopupTo(admin, Loc.GetString("admin-toy-no-access"));
            return;
        }

        if (!TryGetTargetSession(target, out var targetSession))
        {
            PopupTo(admin, Loc.GetString("admin-toy-invalid-target"));
            return;
        }

        if (!_prototype.TryIndex(toyPrototype, out EntityPrototype? toyProto) ||
            toyProto.Abstract ||
            !toyProto.HasComponent<AdminToySpawnableComponent>(EntityManager.ComponentFactory))
        {
            PopupTo(admin, Loc.GetString("admin-toy-invalid-prototype"));
            return;
        }

        if (!_mind.TryGetMind(admin, out var mindId, out var mind))
        {
            PopupTo(admin, Loc.GetString("admin-toy-no-mind"));
            return;
        }

        if (!TryResolveTtsVoice(ttsVoice, out var resolvedTtsVoice))
        {
            PopupTo(admin, Loc.GetString("admin-toy-invalid-tts"));
            return;
        }

        CleanupActiveToy(admin.UserId, mind);

        if (!TryAllocatePrivateLayer(out var layer))
        {
            PopupTo(admin, Loc.GetString("admin-toy-no-private-layer"));
            return;
        }

        if (!TrySpawnToyEntity(target, out var toy))
        {
            _usedPrivateLayers.Remove(layer);
            PopupTo(admin, Loc.GetString("admin-toy-spawn-failed"));
            return;
        }

        var component = Comp<AdminToyComponent>(toy);
        component.ToyPrototype = toyPrototype;
        component.AdminUserId = admin.UserId;
        component.TargetUserId = targetSession.UserId;
        component.TargetEntity = target;
        component.AdminMindId = mindId;
        component.PrivateVisibilityLayer = layer;

        ApplyToyMetadata(toy, name, description);
        ApplyToyLightComponents(toy, toyProto);
        ApplyToyLanguages(toy);
        ApplyToyTts(toy, resolvedTtsVoice);

        var visibility = EnsureComp<VisibilityComponent>(toy);
        _visibility.SetLayer((toy, visibility), layer);

        _actions.AddAction(toy, ref component.BooActionEntity, component.BooAction, toy);
        _actions.AddAction(toy, ref component.LightningActionEntity, component.LightningAction, toy);

        Dirty(toy, component);
        _adminToys[admin.UserId] = toy;

        AddSessionLayer(admin, layer);
        AddSessionLayer(targetSession, layer);

        if (mind.VisitingEntity != null)
            _mind.UnVisit(mindId, mind);

        _mind.Visit(mindId, toy, mind);
        RefreshSessionMask(admin);
        RefreshSessionMask(targetSession);

        PopupTo(admin, Loc.GetString("admin-toy-spawn-success", ("target", Name(target)), ("toy", toyProto.Name)));
    }

    private bool TrySpawnToyEntity(EntityUid target, out EntityUid toy)
    {
        toy = default;

        if (!_transform.TryGetMapOrGridCoordinates(target, out var targetCoords))
            return false;

        for (var i = 0; i < 24; i++)
        {
            var angle = _random.NextAngle();
            var distance = _random.NextFloat(2f, 3f);
            var coords = targetCoords.Value.Offset(angle.ToWorldVec() * distance);
            if (!coords.IsValid(EntityManager))
                continue;

            toy = Spawn(AdminToyPrototype, coords);
            return true;
        }

        return false;
    }

    private void ApplyToyMetadata(EntityUid toy, string name, string description)
    {
        var metadata = MetaData(toy);
        _metadata.SetEntityName(toy, NormalizeToyText(name, DefaultToyName, MaxToyNameLength), metadata);
        _metadata.SetEntityDescription(toy, NormalizeToyText(description, DefaultToyDescription, MaxToyDescriptionLength, true), metadata);
    }

    private void ApplyToyLightComponents(EntityUid toy, EntityPrototype toyPrototype)
    {
        var hasRgb = toyPrototype.TryGetComponent(out RgbLightControllerComponent? sourceRgb, EntityManager.ComponentFactory);
        if (toyPrototype.TryGetComponent(out PointLightComponent? sourceLight, EntityManager.ComponentFactory) || hasRgb)
        {
            var light = EnsureComp<PointLightComponent>(toy);

            if (sourceLight != null)
            {
                light.Offset = sourceLight.Offset;
                _pointLight.SetColor(toy, sourceLight.Color, light);
                _pointLight.SetEnergy(toy, sourceLight.Energy, light);
                _pointLight.SetSoftness(toy, sourceLight.Softness, light);
                _pointLight.SetFalloff(toy, sourceLight.Falloff, light);
                _pointLight.SetCurveFactor(toy, sourceLight.CurveFactor, light);
                _pointLight.SetCastShadows(toy, sourceLight.CastShadows, light);
                _pointLight.SetEnabled(toy, sourceLight.Enabled, light);
                _pointLight.SetRadius(toy, sourceLight.Radius, light);
            }

            Dirty(toy, light);
        }

        if (!hasRgb || sourceRgb == null)
            return;

        var rgb = EnsureComp<RgbLightControllerComponent>(toy);
        _rgb.SetCycleRate(toy, sourceRgb.CycleRate, rgb);
        _rgb.SetLayers(toy, sourceRgb.Layers == null ? null : new List<int>(sourceRgb.Layers), rgb);
    }

    private void ApplyToyLanguages(EntityUid toy)
    {
        var language = EnsureComp<LanguageComponent>(toy);
        language.KnownLanguages.Clear();
        language.CantSpeakLanguages.Clear();
        language.UnlockLanguagesAfterMakeSentient.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<LanguagePrototype>())
        {
            language.KnownLanguages.Add(proto.ID);
        }

        if (language.KnownLanguages.Contains(LanguageSystem.DefaultLanguageId))
            language.SelectedLanguage = LanguageSystem.DefaultLanguageId;
        else if (language.KnownLanguages.Count > 0)
            language.SelectedLanguage = language.KnownLanguages.First();

        Dirty(toy, language);
    }

    private void ApplyToyTts(EntityUid toy, string? voice)
    {
        if (voice == null)
            return;

        var tts = EnsureComp<TTSComponent>(toy);
        tts.VoicePrototypeId = voice;
        Dirty(toy, tts);
    }

    private bool TryResolveTtsVoice(string voice, out string? resolvedVoice)
    {
        resolvedVoice = null;
        var trimmed = voice.Trim();

        if (!string.IsNullOrEmpty(trimmed))
        {
            if (!_prototype.HasIndex<TTSVoicePrototype>(trimmed))
                return false;

            resolvedVoice = trimmed;
            return true;
        }

        var voices = _prototype.EnumeratePrototypes<TTSVoicePrototype>().ToList();
        if (voices.Count == 0)
            return true;

        resolvedVoice = _random.Pick(voices).ID;
        return true;
    }

    private static string NormalizeToyText(string text, string defaultValue, int maxLength, bool allowNewLines = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return defaultValue;

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = new string(normalized
            .Where(ch => !char.IsControl(ch) || allowNewLines && ch == '\n')
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))
            return defaultValue;

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private void CleanupActiveToy(NetUserId adminUserId, MindComponent mind)
    {
        if (_adminToys.TryGetValue(adminUserId, out var activeToy))
        {
            if (TryComp<AdminToyComponent>(activeToy, out var activeToyComponent) &&
                !activeToyComponent.CleaningUp)
            {
                CleanupToy(activeToy, activeToyComponent, unvisit: true, delete: true);
            }
            else
            {
                _adminToys.Remove(adminUserId);
            }
        }

        if (mind.VisitingEntity is not {Valid: true} visiting)
            return;

        if (!TryComp<AdminToyComponent>(visiting, out var visitingToy) ||
            visitingToy.AdminUserId != adminUserId)
        {
            return;
        }

        CleanupToy(visiting, visitingToy, unvisit: true, delete: true);
    }

    private void OnBooAction(EntityUid uid, AdminToyComponent component, AdminToyBooActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var entities = _lookup.GetEntitiesInRange(uid, component.BooRadius).ToList();
        _random.Shuffle(entities);

        var booCounter = 0;
        foreach (var ent in entities)
        {
            if (!HasComp<PoweredLightComponent>(ent))
                continue;

            if (!_ghost.DoGhostBooEvent(ent))
                continue;

            booCounter++;
            if (booCounter >= component.BooMaxTargets)
                break;
        }

        if (booCounter == 0)
            _popup.PopupEntity(Loc.GetString("admin-toy-boo-failed"), uid, uid);
    }

    private void OnLightningAction(EntityUid uid, AdminToyComponent component, AdminToyLightningActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (component.TargetEntity != args.Target)
        {
            _popup.PopupEntity(Loc.GetString("admin-toy-lightning-wrong-target"), uid, uid);
            return;
        }

        if (!HasComp<MobStateComponent>(args.Target) || !_mobState.IsAlive(args.Target))
        {
            _popup.PopupEntity(Loc.GetString("admin-toy-lightning-invalid-target"), uid, uid);
            return;
        }

        var filter = GetPrivateFilter(component);
        _lightning.ShootLightning(uid,
            args.Target,
            component.LightningPrototype,
            visibilityLayer: component.PrivateVisibilityLayer,
            filter: filter);
    }

    private void OnTransformSpeech(Entity<AdminToyComponent> ent, ref TransformSpeechEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        var message = args.Message;
        args.Message = string.Empty;
        args.Cancel();

        if (!TryGetAdminSession(ent.Comp, out var adminSession) ||
            !TryGetTargetSession(ent.Comp, out var targetSession) ||
            targetSession.AttachedEntity is not { Valid: true } target)
        {
            return;
        }

        var speech = _chat.GetSpeechVerb(ent.Owner, message);
        var nameEv = new TransformSpeakerNameEvent(ent.Owner, Name(ent.Owner));
        RaiseLocalEvent(ent.Owner, nameEv);
        if (nameEv.SpeechVerb != null && _prototype.Resolve(nameEv.SpeechVerb, out var proto))
            speech = proto;

        var name = FormattedMessage.EscapeText(nameEv.VoiceName);
        var verb = Loc.GetString(_random.Pick(speech.SpeechVerbStrings));

        var lexiconMessage = message;
        var langName = Loc.GetString("admin-toy-private-speech-language");
        var selectedLanguage = string.Empty;

        if (TryComp<LanguageComponent>(ent.Owner, out var language))
        {
            selectedLanguage = language.SelectedLanguage;
            langName = _language.GetLangName(ent.Owner, language);
            lexiconMessage = _language.TransformWord(message, selectedLanguage);
        }

        var wrappedMessage = Loc.GetString(speech.Bold ? "chat-manager-entity-say-bold-wrap-message-lang" : "chat-manager-entity-say-wrap-message-lang",
            ("entityName", name),
            ("verb", verb),
            ("language", langName),
            ("fontType", speech.FontId),
            ("fontSize", speech.FontSize),
            ("message", FormattedMessage.EscapeText(message)));

        var messageToSend = message;
        var wrappedMessageToSend = wrappedMessage;

        if (!string.IsNullOrEmpty(selectedLanguage) && !_language.KnowsLanguage(target, selectedLanguage))
        {
            var wrappedMessageUnk = Loc.GetString(speech.Bold ? "chat-manager-entity-say-bold-wrap-message" : "chat-manager-entity-say-wrap-message",
                ("entityName", name),
                ("verb", verb),
                ("fontType", speech.FontId),
                ("fontSize", speech.FontSize),
                ("message", FormattedMessage.EscapeText(message)));

            messageToSend = lexiconMessage;
            wrappedMessageToSend = wrappedMessageUnk.Replace(FormattedMessage.EscapeText(message), FormattedMessage.EscapeText(lexiconMessage));
        }

        _chatManager.ChatMessageToOne(ChatChannel.Local, message, wrappedMessage, ent.Owner, false, adminSession.Channel);

        if (targetSession != adminSession)
            _chatManager.ChatMessageToOne(ChatChannel.Local, messageToSend, wrappedMessageToSend, ent.Owner, false, targetSession.Channel);

        var spokeEv = new EntitySpokeToEntityEvent(target, message, lexiconMessage, selectedLanguage);
        RaiseLocalEvent(ent.Owner, spokeEv, true);

        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Local, message, wrappedMessage, GetNetEntity(ent.Owner), null, false));
        _adminLogger.Add(LogType.Chat, LogImpact.Low,
            $"Admin toy private speech by {adminSession.Name} ({adminSession.UserId}) as {Name(ent.Owner)} " +
            $"to {targetSession.Name} ({targetSession.UserId}): {message}");
    }

    private void OnPlaceConstructionGhost(AdminToyPlaceConstructionGhostRequest ev, EntitySessionEventArgs args)
    {
        if (!TryGetSessionToy(args, out var toy, out var component))
        {
            return;
        }

        if (component.AdminUserId != args.SenderSession.UserId)
            return;

        if (!_prototype.TryIndex<ConstructionPrototype>(ev.Prototype, out var construction) ||
            construction.Hide ||
            construction.Type != ConstructionType.Structure)
        {
            return;
        }

        var coords = GetCoordinates(ev.Coordinates);
        if (!coords.IsValid(EntityManager))
            return;

        if (!coords.TryDistance(EntityManager, Transform(toy).Coordinates, out var distance) ||
            distance > component.ConstructionRange)
        {
            _popup.PopupEntity(Loc.GetString("admin-toy-construction-too-far"), toy, toy);
            return;
        }

        var ghostId = _nextConstructionGhostId++;
        component.ConstructionGhosts.Add(ghostId);
        Dirty(toy, component);

        RaiseNetworkEvent(
            new AdminToyConstructionGhostCreateEvent(ghostId, ev.Coordinates, ev.Prototype, ev.Angle),
            GetPrivateFilter(component));
    }

    private void OnClearConstructionGhost(AdminToyClearConstructionGhostRequest ev, EntitySessionEventArgs args)
    {
        if (!TryGetSessionToy(args, out var toy, out var component) ||
            !component.ConstructionGhosts.Remove(ev.GhostId))
        {
            return;
        }

        Dirty(toy, component);
        RaiseNetworkEvent(new AdminToyClearConstructionGhostsEvent([ev.GhostId]), GetPrivateFilter(component));
    }

    private void OnClearAllConstructionGhosts(AdminToyClearAllConstructionGhostsRequest ev, EntitySessionEventArgs args)
    {
        if (!TryGetSessionToy(args, out var toy, out var component) ||
            component.ConstructionGhosts.Count == 0)
        {
            return;
        }

        var ghosts = component.ConstructionGhosts.ToArray();
        component.ConstructionGhosts.Clear();
        Dirty(toy, component);
        RaiseNetworkEvent(new AdminToyClearConstructionGhostsEvent(ghosts), GetPrivateFilter(component));
    }

    private bool TryGetSessionToy(EntitySessionEventArgs args,
        out EntityUid toy,
        [NotNullWhen(true)] out AdminToyComponent? component)
    {
        toy = default;
        component = null;

        if (args.SenderSession.AttachedEntity is not {Valid: true} attached ||
            !TryComp(attached, out component) ||
            component.AdminUserId != args.SenderSession.UserId)
        {
            return false;
        }

        toy = attached;
        return true;
    }

    private void OnGetVisibility(ref GetVisMaskEvent ev)
    {
        if (!TryComp(ev.Entity, out ActorComponent? actor))
            return;

        if (!_sessionLayers.TryGetValue(actor.PlayerSession, out var layers))
            return;

        foreach (var layer in layers)
        {
            ev.VisibilityMask |= layer;
        }
    }

    private void OnMindUnvisited(EntityUid uid, AdminToyComponent component, MindUnvisitedMessage args)
    {
        var adminUserId = component.AdminUserId;
        var targetUserId = component.TargetUserId;

        CleanupToy(uid, component, unvisit: false, delete: true);

        RefreshSessionMask(adminUserId);
        RefreshSessionMask(targetUserId);
    }

    private void OnShutdown(EntityUid uid, AdminToyComponent component, ComponentShutdown args)
    {
        CleanupToy(uid, component, unvisit: true, delete: false);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        var query = EntityQueryEnumerator<AdminToyComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            CleanupToy(uid, component, unvisit: true, delete: false);
        }

        _usedPrivateLayers.Clear();
        _sessionLayers.Clear();
        _adminToys.Clear();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Connected)
            return;

        var query = EntityQueryEnumerator<AdminToyComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.AdminUserId == e.Session.UserId || component.TargetUserId == e.Session.UserId)
                CleanupToy(uid, component, unvisit: true, delete: true);
        }
    }

    private void CleanupToy(EntityUid uid, AdminToyComponent component, bool unvisit, bool delete)
    {
        if (component.CleaningUp)
            return;

        component.CleaningUp = true;

        var admin = TryGetAdminSession(component, out var adminSession) ? adminSession : null;
        var target = TryGetTargetSession(component, out var targetSession) ? targetSession : null;

        if (component.ConstructionGhosts.Count > 0)
        {
            var ev = new AdminToyClearConstructionGhostsEvent(component.ConstructionGhosts.ToArray());
            if (admin != null)
                RaiseNetworkEvent(ev, admin.Channel);
            if (target != null && target != admin)
                RaiseNetworkEvent(ev, target.Channel);
            component.ConstructionGhosts.Clear();
        }

        if (component.PrivateVisibilityLayer != 0)
        {
            if (admin != null)
                RemoveSessionLayer(admin, component.PrivateVisibilityLayer);
            if (target != null)
                RemoveSessionLayer(target, component.PrivateVisibilityLayer);

            _usedPrivateLayers.Remove(component.PrivateVisibilityLayer);
            component.PrivateVisibilityLayer = 0;
        }

        if (component.AdminUserId is { } adminUserId)
            _adminToys.Remove(adminUserId);

        if (TryComp<FollowerComponent>(uid, out var follower))
            _follower.StopFollowingEntity(uid, follower.Following);

        if (unvisit && component.AdminMindId is {Valid: true} mindId && TryComp<MindComponent>(mindId, out var mind))
        {
            if (mind.VisitingEntity == uid)
                _mind.UnVisit(mindId, mind);

            if (admin != null)
                RefreshSessionMask(admin);
        }

        if (delete && !TerminatingOrDeleted(uid))
            QueueDel(uid);
    }

    private bool TryAllocatePrivateLayer(out ushort layer)
    {
        for (var bit = PrivateLayerFirstBit; bit <= PrivateLayerLastBit; bit++)
        {
            layer = (ushort) (1 << bit);
            if (_usedPrivateLayers.Add(layer))
                return true;
        }

        layer = 0;
        return false;
    }

    private void AddSessionLayer(ICommonSession session, ushort layer)
    {
        if (!_sessionLayers.TryGetValue(session, out var layers))
        {
            layers = new HashSet<ushort>();
            _sessionLayers[session] = layers;
        }

        layers.Add(layer);
        RefreshSessionMask(session);
    }

    private void RemoveSessionLayer(ICommonSession session, ushort layer)
    {
        if (!_sessionLayers.TryGetValue(session, out var layers))
            return;

        layers.Remove(layer);
        if (layers.Count == 0)
            _sessionLayers.Remove(session);

        RefreshSessionMask(session);
    }

    private void RefreshSessionMask(ICommonSession session)
    {
        if (session.AttachedEntity is not {Valid: true} attached)
            return;

        _eye.RefreshVisibilityMask(attached);
    }

    private void RefreshSessionMask(NetUserId? userId)
    {
        if (userId == null || !_player.TryGetSessionById(userId.Value, out var session))
            return;

        RefreshSessionMask(session);
    }

    private Filter GetPrivateFilter(AdminToyComponent component)
    {
        var filter = Filter.Empty();

        if (TryGetAdminSession(component, out var admin))
            filter.AddPlayer(admin);

        if (TryGetTargetSession(component, out var target))
            filter.AddPlayer(target);

        return filter;
    }

    private bool TryGetAdminSession(AdminToyComponent component, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        return component.AdminUserId != null &&
               _player.TryGetSessionById(component.AdminUserId.Value, out session);
    }

    private bool TryGetTargetSession(EntityUid target, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;

        if (TryComp<ActorComponent>(target, out var actor))
        {
            session = actor.PlayerSession;
            return true;
        }

        return _mind.TryGetMind(target, out _, out var mind) &&
               mind.UserId != null &&
               _player.TryGetSessionById(mind.UserId.Value, out session);
    }

    private bool TryGetTargetSession(AdminToyComponent component, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        return component.TargetUserId != null &&
               _player.TryGetSessionById(component.TargetUserId.Value, out session);
    }

    private void PopupTo(ICommonSession session, string message)
    {
        if (session.AttachedEntity is not {Valid: true} attached)
            return;

        _popup.PopupEntity(message, attached, attached);
    }
}
