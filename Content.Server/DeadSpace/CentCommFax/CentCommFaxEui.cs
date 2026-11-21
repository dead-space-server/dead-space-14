// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.CentCommFax;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Eui;
using Content.Shared.Fax.Components;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.CentCommFax;

public sealed partial class CentCommFaxEui : BaseEui
{
    private const int MaxTitleLength = 100;
    private const int MaxContentLength = 10000;
    private const int MaxCommentLength = 4000;
    private const int MaxFromLength = 100;
    private const int MaxStamps = 10;
    private const int MaxPaperStamps = 30;
    private const int MaxStampText = 200;
    private const int MaxStampPath = 256;
    private const int MaxSignatures = 30;
    private const int MaxSignatureLength = 200;
    private const string DefaultPaper = "PaperPrintedCentcomm";

    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly CentCommFaxSystem _centComm;
    private readonly FollowerSystem _follower;
    private readonly GameTicker _ticker;
    private readonly SharedHandsSystem _hands;
    private readonly PaperSystem _paper;
    private readonly MetaDataSystem _meta;
    private readonly SharedTransformSystem _transform;

    public CentCommFaxEui()
    {
        IoCManager.InjectDependencies(this);
        _centComm = _entities.System<CentCommFaxSystem>();
        _follower = _entities.System<FollowerSystem>();
        _ticker = _entities.System<GameTicker>();
        _hands = _entities.System<SharedHandsSystem>();
        _paper = _entities.System<PaperSystem>();
        _meta = _entities.System<MetaDataSystem>();
        _transform = _entities.System<SharedTransformSystem>();
    }

    public override void Opened()
    {
        base.Opened();
        _centComm.InboxChanged += StateDirty;
        StateDirty();
    }

    public override void Closed()
    {
        base.Closed();
        _centComm.InboxChanged -= StateDirty;
    }

    public override CentCommFaxEuiState GetNewState()
    {
        var state = new CentCommFaxEuiState();
        state.Inbox.AddRange(_centComm.Inbox);

        var faxes = _entities.EntityQueryEnumerator<FaxMachineComponent>();
        while (faxes.MoveNext(out var uid, out var fax))
        {
            state.Faxes.Add(new CentCommFaxTarget(_entities.GetNetEntity(uid), fax.FaxName, _centComm.GetStationName(uid)));
        }

        return state;
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admins.HasAdminFlag(Player, AdminFlags.Fun))
            return;

        switch (msg)
        {
            case CentCommFaxEuiMsg.Reply reply:
                HandleReply(reply);
                break;
            case CentCommFaxEuiMsg.Print print:
                HandlePrint(print.Sheet);
                break;
            case CentCommFaxEuiMsg.Load:
                HandleLoad();
                break;
            case CentCommFaxEuiMsg.RequestTemplate request:
                HandleTemplate(request);
                break;
            case CentCommFaxEuiMsg.Follow follow:
                if (Player.AttachedEntity is { } ghost &&
                    _entities.HasComponent<GhostComponent>(ghost) &&
                    _entities.TryGetEntity(follow.Target, out var target))
                {
                    _follower.StartFollowingEntity(ghost, target.Value);
                }
                break;
        }
    }

    private void HandleReply(CentCommFaxEuiMsg.Reply reply)
    {
        if (!_entities.TryGetEntity(reply.Target, out var target) ||
            !_entities.HasComponent<FaxMachineComponent>(target) ||
            !TryBuildPrintout(reply, out var printout))
        {
            return;
        }

        _centComm.Reply(target.Value, printout, reply.ReplyTo, Player);
    }

    private void HandlePrint(CentCommFaxEuiMsg.Reply reply)
    {
        if (!TryBuildPrintout(reply, out var printout))
            return;

        if (Player.AttachedEntity is not { } admin || !_entities.HasComponent<HandsComponent>(admin))
            return;

        var prototype = _prototypes.HasIndex<EntityPrototype>(printout.PrototypeId)
            ? printout.PrototypeId
            : DefaultPaper;
        var printed = _entities.SpawnAtPosition(prototype, _entities.GetComponent<TransformComponent>(admin).Coordinates);
        if (!_entities.TryGetComponent<PaperComponent>(printed, out var paper))
        {
            _entities.DeleteEntity(printed);
            return;
        }

        paper.Signatures = printout.Signatures ?? new List<string>();
        paper.EditingDisabled = false;
        _paper.SetContent((printed, paper), printout.Content);
        if (printout.StampState != null)
        {
            foreach (var stamp in printout.StampedBy)
            {
                _paper.TryStamp((printed, paper), stamp, printout.StampState);
            }
        }
        else if (printout.StampedBy.Count > 0)
        {
            paper.StampedBy = new List<StampDisplayInfo>(printout.StampedBy);
            _paper.SetContent((printed, paper), printout.Content);
        }

        _meta.SetEntityName(printed, printout.Name);

        if (!_hands.TryPickupAnyHand(admin, printed, checkActionBlocker: false))
            _transform.PlaceNextTo(printed, admin);
    }

    private void HandleLoad()
    {
        if (Player.AttachedEntity is not { } admin ||
            !_hands.TryGetActiveItem(admin, out var held) ||
            !_entities.TryGetComponent<PaperComponent>(held, out var paper))
        {
            return;
        }

        var metadata = _entities.GetComponent<MetaDataComponent>(held.Value);
        SendMessage(new CentCommFaxEuiMsg.PaperLoaded
        {
            Title = Truncate(metadata.EntityName, MaxTitleLength),
            Content = Truncate(paper.Content, MaxContentLength),
            PaperPrototype = metadata.EntityPrototype?.ID ?? DefaultPaper,
            StampState = TruncateOrNull(paper.StampState, MaxStampPath),
            Stamps = CopyStamps(paper.StampedBy),
            Signatures = CopySignatures(paper.Signatures),
        });
    }

    private void HandleTemplate(CentCommFaxEuiMsg.RequestTemplate request)
    {
        if (!_prototypes.TryIndex<PaperworkFormPrototype>(request.Template, out var form) || !form.Listed)
            return;

        if (!_resources.ContentFileExists(form.Text))
            return;

        var text = _resources.ContentFileReadText(form.Text).ReadToEnd();

        EntityUid? target = null;
        if (request.Target is { } net && _entities.TryGetEntity(net, out var resolved))
            target = resolved;

        var station = _centComm.GetStationName(target);
        var title = Loc.GetString(form.Name);
        text = PaperworkTextSubstitutions.ApplyBase(text, title, _ticker.RoundDuration(),
            string.IsNullOrEmpty(station) ? null : station);

        SendMessage(new CentCommFaxEuiMsg.TemplateLoaded
        {
            Template = form.ID,
            Title = title,
            Content = text,
            PaperPrototype = form.PaperPrototype,
        });
    }

    private bool TryBuildPrintout(CentCommFaxEuiMsg.Reply reply, out FaxPrintout printout)
    {
        printout = default!;
        if (reply.Stamps.Count > MaxStamps)
            return false;

        CentCommFaxEntry? original = null;
        if (!reply.UseLoaded && reply.ReturnOriginal && !_centComm.TryGetEntry(reply.ReplyTo, out original))
            return false;

        if (original == null && !reply.UseLoaded &&
            (string.IsNullOrWhiteSpace(reply.Title) || string.IsNullOrWhiteSpace(reply.Content)))
        {
            return false;
        }

        string content;
        string title;
        string? label;
        string paper;
        string? stampState;
        List<StampDisplayInfo> stamps;
        List<string> signatures;

        if (reply.UseLoaded)
        {
            content = Truncate(reply.Content, MaxContentLength);
            title = Truncate(reply.Title, MaxTitleLength);
            label = TruncateOrNull(reply.LoadedLabel, MaxTitleLength);
            paper = reply.PaperPrototype != null && _prototypes.HasIndex<EntityPrototype>(reply.PaperPrototype)
                ? reply.PaperPrototype
                : DefaultPaper;
            stampState = TruncateOrNull(reply.LoadedStampState, MaxStampPath);
            stamps = CopyStamps(reply.LoadedStamps);
            signatures = CopySignatures(reply.LoadedSignatures);
        }
        else if (original != null)
        {
            content = original.Content;
            title = original.Title;
            label = original.Label;
            paper = _prototypes.HasIndex<EntityPrototype>(original.PaperPrototype)
                ? original.PaperPrototype
                : DefaultPaper;
            stampState = original.StampState;
            stamps = new List<StampDisplayInfo>(original.Stamps);
            signatures = new List<string>(original.Signatures);
        }
        else
        {
            content = Truncate(reply.Content, MaxContentLength);
            title = Truncate(reply.Title, MaxTitleLength);
            label = null;
            paper = reply.PaperPrototype != null && _prototypes.HasIndex<EntityPrototype>(reply.PaperPrototype)
                ? reply.PaperPrototype
                : DefaultPaper;
            stampState = null;
            stamps = new List<StampDisplayInfo>();
            signatures = new List<string>();
        }

        var comment = reply.Comment.Trim();
        if (comment.Length > 0 && (reply.ReturnOriginal || reply.UseLoaded))
        {
            comment = Truncate(comment, MaxCommentLength);
            var separator = content.Length == 0 ? string.Empty : "\n\n";
            var room = MaxContentLength - content.Length - separator.Length;
            if (room > 0)
            {
                if (comment.Length > room)
                    comment = comment[..room];
                content += separator + comment;
            }
        }

        var from = Truncate(reply.From, MaxFromLength);
        foreach (var choice in reply.Stamps)
        {
            if (stamps.Count >= MaxPaperStamps)
                break;

            if (!_prototypes.TryIndex<EntityPrototype>(choice.Prototype, out var stampProto) ||
                !stampProto.TryComp(out StampComponent? stamp, _factory))
            {
                return false;
            }

            stampState ??= stamp.StampState;
            stamps.Add(new StampDisplayInfo
            {
                StampedName = string.IsNullOrWhiteSpace(from) ? stamp.StampedName : from,
                StampedColor = choice.Color ?? stamp.StampedColor,
                StampTexture = stamp.StampTexture,
                StampPatternTexture = stamp.StampPatternTexture,
                StampHeaderText = stamp.StampHeaderText,
                StampBackgroundText = stamp.StampBackgroundText,
                StampScale = stamp.StampScale,
                StampMainText = stamp.StampMainText,
                StampTextMaxScale = stamp.StampTextMaxScale,
            });
        }

        if (string.IsNullOrWhiteSpace(title))
            title = Loc.GetString("centcomm-fax-window-title");

        printout = new FaxPrintout(content, title, label, paper, stampState, stamps, reply.Locked, signatures);
        return true;
    }

    private static List<StampDisplayInfo> CopyStamps(IEnumerable<StampDisplayInfo> stamps)
    {
        var list = new List<StampDisplayInfo>();
        foreach (var stamp in stamps)
        {
            if (list.Count >= MaxPaperStamps)
                break;

            list.Add(new StampDisplayInfo
            {
                StampedName = Truncate(stamp.StampedName ?? string.Empty, MaxStampText),
                StampedColor = stamp.StampedColor,
                StampTexture = TruncateOrNull(stamp.StampTexture, MaxStampPath),
                StampPatternTexture = TruncateOrNull(stamp.StampPatternTexture, MaxStampPath),
                StampHeaderText = TruncateOrNull(stamp.StampHeaderText, MaxStampText),
                StampBackgroundText = TruncateOrNull(stamp.StampBackgroundText, MaxStampText),
                StampMainText = TruncateOrNull(stamp.StampMainText, MaxStampText),
                StampScale = float.IsFinite(stamp.StampScale) ? Math.Clamp(stamp.StampScale, 0.1f, 5f) : 1f,
                StampTextMaxScale = float.IsFinite(stamp.StampTextMaxScale) ? Math.Clamp(stamp.StampTextMaxScale, 0f, 5f) : 0f,
                StampRotation = float.IsFinite(stamp.StampRotation) ? Math.Clamp(stamp.StampRotation, -360f, 360f) : 0f,
            });
        }

        return list;
    }

    private static List<string> CopySignatures(IEnumerable<string>? signatures)
    {
        var list = new List<string>();
        if (signatures == null)
            return list;

        foreach (var signature in signatures)
        {
            if (list.Count >= MaxSignatures || string.IsNullOrEmpty(signature))
                continue;

            list.Add(Truncate(signature, MaxSignatureLength));
        }

        return list;
    }

    private static string? TruncateOrNull(string? text, int max)
    {
        return text == null ? null : Truncate(text, max);
    }

    private static string Truncate(string text, int max)
    {
        return text.Length <= max ? text : text[..max];
    }
}
