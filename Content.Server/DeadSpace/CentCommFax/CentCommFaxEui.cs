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
using Content.Shared.Paper;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.CentCommFax;

public sealed partial class CentCommFaxEui : BaseEui
{
    private const int MaxTitleLength = 100;
    private const int MaxContentLength = 10000;
    private const int MaxFromLength = 100;
    private const string DefaultPaper = "PaperPrintedCentcomm";

    [Dependency] private IAdminManager _admins = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly CentCommFaxSystem _centComm;
    private readonly FollowerSystem _follower;
    private readonly GameTicker _ticker;

    public CentCommFaxEui()
    {
        IoCManager.InjectDependencies(this);
        _centComm = _entities.System<CentCommFaxSystem>();
        _follower = _entities.System<FollowerSystem>();
        _ticker = _entities.System<GameTicker>();
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
            string.IsNullOrWhiteSpace(reply.Title) ||
            string.IsNullOrWhiteSpace(reply.Content))
        {
            return;
        }

        var paper = reply.PaperPrototype != null && _prototypes.HasIndex<EntityPrototype>(reply.PaperPrototype)
            ? reply.PaperPrototype
            : DefaultPaper;

        var stamps = new List<StampDisplayInfo>();
        string? stampState = null;
        if (reply.StampPrototype != null &&
            _prototypes.TryIndex<EntityPrototype>(reply.StampPrototype, out var stampProto) &&
            stampProto.TryGetComponent<StampComponent>(out var stamp, _factory))
        {
            stampState = stamp.StampState;
            stamps.Add(new StampDisplayInfo
            {
                StampedName = string.IsNullOrWhiteSpace(reply.From) ? stamp.StampedName : Truncate(reply.From, MaxFromLength),
                StampedColor = reply.StampColor ?? stamp.StampedColor,
                StampTexture = stamp.StampTexture,
                StampPatternTexture = stamp.StampPatternTexture,
                StampHeaderText = stamp.StampHeaderText,
                StampBackgroundText = stamp.StampBackgroundText,
                StampScale = stamp.StampScale,
                StampMainText = stamp.StampMainText,
                StampTextMaxScale = stamp.StampTextMaxScale,
            });
        }

        var printout = new FaxPrintout(
            Truncate(reply.Content, MaxContentLength),
            Truncate(reply.Title, MaxTitleLength),
            null,
            paper,
            stampState,
            stamps,
            reply.Locked);

        _centComm.Reply(target.Value, printout, reply.ReplyTo, Player);
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

    private static string Truncate(string text, int max)
    {
        return text.Length <= max ? text : text[..max];
    }
}
