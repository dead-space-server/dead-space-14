using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.UserInterface;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Random.Helpers;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using static Content.Shared.Paper.PaperComponent;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
// DS14-start
using System.Text.RegularExpressions;
using Content.Shared.DeadSpace.SignatureOnPaper.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Timing;
using Robust.Shared.Network;
// DS14-end

namespace Content.Shared.Paper;

public sealed class PaperSystem : EntitySystem
{
    // DS14-start
    private const int MaxStampsPerPaper = 30;
    private const int RotatedStampChancePercent = 17;
    private const float MaxRotationDegrees = 360.0f;
    private const float DegreesToRadians = MathF.PI / 180.0f;
    // DS14-end

    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    // DS14-start
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    // DS14-end

    private static readonly ProtoId<TagPrototype> WriteIgnoreStampsTag = "WriteIgnoreStamps";
    private static readonly ProtoId<TagPrototype> WriteReWriteTag = "WriteReWrite";
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    // DS14-start
    private const string ClownJobId = "Clown";
    private const string ForcedPaperFont = "ComicSans";

    private static readonly Regex PaperFontTagRegex = new(@"\[/pfont\]|\[pfont=(?:""[^""]*""|[^\]]*)\]");
    // DS14-end

    private EntityQuery<PaperComponent> _paperQuery;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _writers = new(); // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PaperComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PaperComponent, ComponentShutdown>(OnShutdown); // DS14
        SubscribeLocalEvent<PaperComponent, BeforeActivatableUIOpenEvent>(BeforeUIOpen);
        SubscribeLocalEvent<PaperComponent, BoundUIOpenedEvent>(OnUIOpened); // DS14
        SubscribeLocalEvent<PaperComponent, BoundUIClosedEvent>(OnUIClosed); // DS14
        SubscribeLocalEvent<PaperComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PaperComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaperComponent, PaperInputTextMessage>(OnInputTextMessage);

        SubscribeLocalEvent<RandomPaperContentComponent, MapInitEvent>(OnRandomPaperContentMapInit);

        SubscribeLocalEvent<ActivateOnPaperOpenedComponent, PaperWriteEvent>(OnPaperWrite);

        _paperQuery = GetEntityQuery<PaperComponent>();
    }

    private void OnMapInit(Entity<PaperComponent> entity, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(entity.Comp.Content))
        {
            SetContent(entity, Loc.GetString(entity.Comp.Content));
        }
    }

    private void OnInit(Entity<PaperComponent> entity, ref ComponentInit args)
    {
        UpdateUserInterface(entity);

        if (TryComp<AppearanceComponent>(entity, out var appearance))
        {
            if (entity.Comp.Content != "" || entity.Comp.Signatures.Count > 0) // DS14-signatures
                _appearance.SetData(entity, PaperVisuals.Status, PaperStatus.Written, appearance);

            if (entity.Comp.StampState != null)
                _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
        }
    }

    // DS14-start
    private void OnShutdown(Entity<PaperComponent> entity, ref ComponentShutdown args)
    {
        if (!_net.IsServer)
            return;

        _writers.Remove(entity.Owner);
    }
    // DS14-end

    private void BeforeUIOpen(Entity<PaperComponent> entity, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateReadUserInterface(entity); // DS14
    }

    // DS14-start
    private void OnUIOpened(Entity<PaperComponent> entity, ref BoundUIOpenedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!Equals(args.UiKey, PaperUiKey.Write) || IsWriter(entity.Owner, args.Actor))
            return;

        _uiSystem.CloseUi(entity.Owner, PaperUiKey.Write, args.Actor);
    }

    private void OnUIClosed(Entity<PaperComponent> entity, ref BoundUIClosedEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!Equals(args.UiKey, PaperUiKey.Write))
            return;

        RemoveWriter(entity.Owner, args.Actor);

        if (!HasWriters(entity.Owner))
            UpdateWriteUserInterface(entity, PaperAction.Read);
    }
    // DS14-end

    private void OnExamined(Entity<PaperComponent> entity, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(PaperComponent)))
        {
            if (entity.Comp.Content != "" || entity.Comp.Signatures.Count > 0) // DS14-signatures
            {
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-has-words",
                        ("paper", entity)
                    )
                );
            }

            if (entity.Comp.StampedBy.Count > 0)
            {
                var commaSeparated =
                    string.Join(", ", entity.Comp.StampedBy.Select(s => GetStampDisplayText(s.StampedName))); // DS14
                args.PushMarkup(
                    Loc.GetString(
                        "paper-component-examine-detail-stamped-by",
                        ("paper", entity),
                        ("stamps", commaSeparated))
                );
            }
        }
    }

    private void OnInteractUsing(Entity<PaperComponent> entity, ref InteractUsingEvent args)
    {
        // only allow editing if there are no stamps or when using a cyberpen
        var editable = (entity.Comp.Signatures.Count == 0 && entity.Comp.StampedBy.Count == 0) || _tagSystem.HasTag(args.Used, WriteIgnoreStampsTag); // DS14-signatures
        if (_tagSystem.HasTag(args.Used, WriteTag))
        {
            if (editable)
            {
                if (entity.Comp.EditingDisabled)
                {
                    var paperEditingDisabledMessage = Loc.GetString("paper-tamper-proof-modified-message");
                    _popupSystem.PopupClient(paperEditingDisabledMessage, entity, args.User);

                    args.Handled = true;
                    return;
                }

                var ev = new PaperWriteAttemptEvent(entity.Owner);
                RaiseLocalEvent(args.User, ref ev);
                if (ev.Cancelled)
                {
                    if (ev.FailReason is not null)
                    {
                        var fileWriteMessage = Loc.GetString(ev.FailReason);
                        _popupSystem.PopupClient(fileWriteMessage, entity.Owner, args.User);
                    }

                    args.Handled = true;
                    return;
                }

                var writeEvent = new PaperWriteEvent(args.User, entity);
                RaiseLocalEvent(args.Used, ref writeEvent);

                // DS14-start
                AddWriter(entity.Owner, args.User);
                _uiSystem.CloseUi(entity.Owner, PaperUiKey.Key, args.User);
                UpdateWriteUserInterface(entity, PaperAction.Write);
                _uiSystem.OpenUi(entity.Owner, PaperUiKey.Write, args.User);
                // DS14-end
            }
            args.Handled = true;
            return;
        }

        // If a stamp, attempt to stamp paper
        // DS14-start
        if (TryComp<StampComponent>(args.Used, out var stampComp))
        {
            if (!_timing.IsFirstTimePredicted)
            {
                args.Handled = true;
                return;
            }

            if (entity.Comp.StampedBy.Count >= MaxStampsPerPaper)
            {
                var stampPaperFullMessage = Loc.GetString("paper-component-action-stamp-paper-full");
                _popupSystem.PopupClient(stampPaperFullMessage, entity, args.User);

                args.Handled = true;
                return;
            }

            if (!TryStamp(entity, GetStampInfo(entity, args.Used, stampComp), stampComp.StampState))
            {
                args.Handled = true;
                return;
            }
            // DS14-end

            // successfully stamped, play popup
            var stampPaperOtherMessage = Loc.GetString("paper-component-action-stamp-paper-other",
                ("user", args.User),
                ("target", args.Target),
                ("stamp", args.Used));

            _popupSystem.PopupEntity(stampPaperOtherMessage, args.User, Filter.PvsExcept(args.User, entityManager: EntityManager), true);
            var stampPaperSelfMessage = Loc.GetString("paper-component-action-stamp-paper-self",
                ("target", args.Target),
                ("stamp", args.Used));
            _popupSystem.PopupClient(stampPaperSelfMessage, args.User, args.User);

            _audio.PlayPredicted(stampComp.Sound, entity, args.User);

            // DS14-start
            if (_net.IsServer)
                UpdateUserInterface(entity);
            // DS14-end
        }
    }

    private StampDisplayInfo GetStampInfo(EntityUid paper, EntityUid stampUid, StampComponent stamp) // DS14
    {
        return new StampDisplayInfo
        {
            StampedName = stamp.StampedName,
            StampedColor = stamp.StampedColor,
            StampTexture = stamp.StampTexture,
            StampPatternTexture = stamp.StampPatternTexture,
            StampHeaderText = stamp.StampHeaderText,
            StampBackgroundText = stamp.StampBackgroundText,
            StampRotation = stamp.StampCanRotate ? GetStampRotation(paper, stampUid) : 0.0f,
            // DS14-start
            StampScale = stamp.StampScale,
            StampMainText = stamp.StampMainText,
            StampTextMaxScale = stamp.StampTextMaxScale
            // DS14-end
        };
    }

    // DS14-start
    private float GetStampRotation(EntityUid paper, EntityUid stamp)
    {
        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(paper), GetNetEntity(stamp));
        if (!random.Prob(RotatedStampChancePercent / 100.0f))
            return 0.0f;

        var magnitude = random.NextFloat(0.0f, MaxRotationDegrees);
        var direction = random.Prob(0.5f) ? 1.0f : -1.0f;

        return magnitude * direction * DegreesToRadians;
    }

    private string GetStampDisplayText(string stampName)
    {
        return _loc.TryGetString(stampName, out var localized) ? localized : stampName;
    }
    // DS14-end

    private void OnInputTextMessage(Entity<PaperComponent> entity, ref PaperInputTextMessage args)
    {
        // DS14-start
        if (!_net.IsServer)
            return;

        if (!Equals(args.UiKey, PaperUiKey.Write) || !IsWriter(entity.Owner, args.Actor))
            return;
        // DS14-end

        var ev = new PaperWriteAttemptEvent(entity.Owner);
        RaiseLocalEvent(args.Actor, ref ev);
        if (ev.Cancelled)
            return;

        // DS14-start
        // Clowns always write in Comic Sans and cannot change it.
        var text = args.Text;
        if (IsForcedFontWriter(args.Actor))
            text = ApplyForcedPaperFont(text);
        // DS14-end

        if (text.Length <= entity.Comp.ContentSize)
        {
            SetContent(entity, text);

            var paperStatus = string.IsNullOrWhiteSpace(text) ? PaperStatus.Blank : PaperStatus.Written; // DS14

            if (entity.Comp.Signatures.Count > 0) // DS14-signatures
                paperStatus = PaperStatus.Written;

            if (TryComp<AppearanceComponent>(entity, out var appearance))
                _appearance.SetData(entity, PaperVisuals.Status, paperStatus, appearance);

            if (TryComp(entity, out MetaDataComponent? meta))
                _metaSystem.SetEntityDescription(entity, "", meta);
            // DS14-start
            if (_handsSystem.GetActiveItem(args.Actor) is { } item)
            {
                if ((entity.Comp.Signatures.Count > 0 || entity.Comp.StampState != null) && _tagSystem.HasTag(item, WriteReWriteTag) && TryComp<SignaturePaperComponent>(entity, out var comp) && !entity.Comp.Signatures.Contains("[color=red][bold]Переписано[/bold][/color]"))
                {
                    entity.Comp.Signatures.Add("[color=red][bold]Переписано[/bold][/color]");
                    comp.NumberSignatures += 1;
                }
            }
            //DS14-end

            _adminLogger.Add(LogType.Chat,
                LogImpact.Low,
                $"{ToPrettyString(args.Actor):player} has written on {ToPrettyString(entity):entity} the following text: {text}");

            _audio.PlayPvs(entity.Comp.Sound, entity);
        }

        // DS14-start
        RemoveWriter(entity.Owner, args.Actor);
        _uiSystem.CloseUi(entity.Owner, PaperUiKey.Write, args.Actor);
        UpdateUserInterface(entity);
        // DS14-end
    }

    private void OnRandomPaperContentMapInit(Entity<RandomPaperContentComponent> ent, ref MapInitEvent args)
    {
        if (!_paperQuery.TryComp(ent, out var paperComp))
        {
            Log.Warning($"{ToPrettyString(ent)} has a {nameof(RandomPaperContentComponent)} but no {nameof(PaperComponent)}!");
            RemCompDeferred(ent, ent.Comp);
            return;
        }
        var dataset = _protoMan.Index(ent.Comp.Dataset);
        // Intentionally not using the Pick overload that directly takes a LocalizedDataset,
        // because we want to get multiple attributes from the same pick.
        var pick = _random.Pick(dataset.Values);

        // Name
        _metaSystem.SetEntityName(ent, Loc.GetString(pick));
        // Description
        _metaSystem.SetEntityDescription(ent, Loc.GetString($"{pick}.desc"));
        // Content
        SetContent((ent, paperComp), Loc.GetString($"{pick}.content"));

        // Our work here is done
        RemCompDeferred(ent, ent.Comp);
    }

    private void OnPaperWrite(Entity<ActivateOnPaperOpenedComponent> entity, ref PaperWriteEvent args)
    {
        _interaction.UseInHandInteraction(args.User, entity);
    }

    /// <summary>
    /// Accepts the name and state to be stamped onto the paper, returns true if successful.
    /// </summary>
    public bool TryStamp(Entity<PaperComponent> entity, StampDisplayInfo stampInfo, string spriteStampState)
    {
        // DS14-start
        if (entity.Comp.StampedBy.Count >= MaxStampsPerPaper)
            return false;

        entity.Comp.StampedBy.Add(stampInfo);
        Dirty(entity);

        if (entity.Comp.StampState == null && TryComp<AppearanceComponent>(entity, out var appearance))
        {
            entity.Comp.StampState = spriteStampState;
            // Would be nice to be able to display multiple sprites on the paper
            // but most of the existing images overlap
            _appearance.SetData(entity, PaperVisuals.Stamp, entity.Comp.StampState, appearance);
        }
        // DS14-end

        return true;
    }

    /// <summary>
    /// Copy any stamp information from one piece of paper to another.
    /// </summary>
    public void CopyStamps(Entity<PaperComponent?> source, Entity<PaperComponent?> target)
    {
        if (!Resolve(source, ref source.Comp) || !Resolve(target, ref target.Comp))
            return;

        target.Comp.StampedBy = new List<StampDisplayInfo>(source.Comp.StampedBy);
        target.Comp.StampState = source.Comp.StampState;
        Dirty(target);

        if (TryComp<AppearanceComponent>(target, out var appearance))
        {
            // delete any stamps if the stamp state is null
            _appearance.SetData(target, PaperVisuals.Stamp, target.Comp.StampState ?? "", appearance);
        }
    }

    public void SetContent(EntityUid entity, string content)
    {
        if (!TryComp<PaperComponent>(entity, out var paper))
            return;
        SetContent((entity, paper), content);
    }

    public void SetContent(Entity<PaperComponent> entity, string content)
    {
        entity.Comp.Content = content;
        Dirty(entity);
        UpdateUserInterface(entity);

        if (!TryComp<AppearanceComponent>(entity, out var appearance))
            return;

        var status = string.IsNullOrWhiteSpace(content)
            ? PaperStatus.Blank
            : PaperStatus.Written;

        if (entity.Comp.Signatures.Count > 0) // DS14-signatures
            status = PaperStatus.Written;

        _appearance.SetData(entity, PaperVisuals.Status, status, appearance);
    }

    private void UpdateUserInterface(Entity<PaperComponent> entity)
    {
        // DS14-start
        UpdateReadUserInterface(entity);
        UpdateWriteUserInterface(entity, HasWriters(entity.Owner) ? PaperAction.Write : PaperAction.Read);
    }

    private void UpdateReadUserInterface(Entity<PaperComponent> entity)
    {
        _uiSystem.SetUiState(entity.Owner, PaperUiKey.Key, CreateUserInterfaceState(entity, PaperAction.Read));
    }

    private void UpdateWriteUserInterface(Entity<PaperComponent> entity, PaperAction mode)
    {
        _uiSystem.SetUiState(entity.Owner, PaperUiKey.Write, CreateUserInterfaceState(entity, mode));
    }

    private PaperBoundUserInterfaceState CreateUserInterfaceState(Entity<PaperComponent> entity, PaperAction mode)
    {
        return new PaperBoundUserInterfaceState(entity.Comp.Content, entity.Comp.StampedBy, entity.Comp.Signatures, mode);
    }

    private void AddWriter(EntityUid paper, EntityUid writer)
    {
        if (!_net.IsServer)
            return;

        if (!_writers.TryGetValue(paper, out var writers))
        {
            writers = new HashSet<EntityUid>();
            _writers.Add(paper, writers);
        }

        writers.Add(writer);
    }

    private void RemoveWriter(EntityUid paper, EntityUid writer)
    {
        if (!_net.IsServer)
            return;

        if (!_writers.TryGetValue(paper, out var writers))
            return;

        writers.Remove(writer);
        if (writers.Count == 0)
            _writers.Remove(paper);
    }

    private bool IsWriter(EntityUid paper, EntityUid writer)
    {
        return _writers.TryGetValue(paper, out var writers) && writers.Contains(writer);
    }

    private bool HasWriters(EntityUid paper)
    {
        return _writers.TryGetValue(paper, out var writers) && writers.Count > 0;
    }

    private bool IsForcedFontWriter(EntityUid actor)
    {
        // The shared systems are registered client/server-side as derived
        // classes, so resolve them through the entity manager instead of IoC.
        return EntityManager.System<SharedMindSystem>().TryGetMind(actor, out var mindId, out _) &&
               EntityManager.System<SharedJobSystem>().MindHasJobWithId(mindId, ClownJobId);
    }

    private string ApplyForcedPaperFont(string text)
    {
        text = PaperFontTagRegex.Replace(text, string.Empty);
        return $"[pfont=\"{ForcedPaperFont}\"]{text}[/pfont]";
    }
    // DS14-end
}

/// <summary>
/// Event fired when using a pen on paper, opening the UI.
/// </summary>
[ByRefEvent]
public record struct PaperWriteEvent(EntityUid User, EntityUid Paper);

/// <summary>
/// Cancellable event for attempting to write on a piece of paper.
/// </summary>
/// <param name="paper">The paper that the writing will take place on.</param>
[ByRefEvent]
public record struct PaperWriteAttemptEvent(EntityUid Paper, string? FailReason = null, bool Cancelled = false);
