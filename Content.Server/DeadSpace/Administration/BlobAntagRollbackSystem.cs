// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Backmen.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Shared.Backmen.Blob.Components;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Administration;

/// <summary>
/// Preserves a blob carrier body so an administrator can restore it after transformation.
/// </summary>
public sealed partial class BlobAntagRollbackSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<NetUserId, BlobBodyBackup> _backups = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        foreach (var backup in _backups.Values)
        {
            if (!TerminatingOrDeleted(backup.Body))
                QueueDel(backup.Body);
        }

        _backups.Clear();
    }

    public void PreserveBody(NetUserId userId, EntityUid body, MapCoordinates coordinates)
    {
        if (_backups.TryGetValue(userId, out var previous) && Exists(previous.Body) && previous.Body != body)
            QueueDel(previous.Body);

        _backups[userId] = new BlobBodyBackup(body, coordinates);
    }

    public void MovePreservedBodyToNullspace(NetUserId userId)
    {
        if (_backups.TryGetValue(userId, out var backup) && Exists(backup.Body))
            _transform.DetachEntity(backup.Body, Transform(backup.Body));
    }

    public bool TryRestoreBody(ICommonSession session, EntityUid mindId, out EntityUid body)
    {
        body = default;
        if (!_backups.Remove(session.UserId, out var backup) || !Exists(backup.Body))
            return false;

        EntityUid? core = null;
        if (session.AttachedEntity is { } attached &&
            TryComp<BlobObserverComponent>(attached, out var observer))
        {
            core = observer.Core;
        }

        body = backup.Body;
        if (TryComp<BlobCarrierComponent>(body, out var carrier) &&
            carrier.TransformToBlob is { } transformAction &&
            Exists(transformAction))
        {
            QueueDel(transformAction);
        }

        RemComp<BlobCarrierComponent>(body);
        _transform.SetMapCoordinates(body, backup.Coordinates);
        _mind.TransferTo(mindId, body, ghostCheckOverride: true);
        _players.SetAttachedEntity(session, body, true);

        if (core is { } coreEntity && TryComp<BlobCoreComponent>(coreEntity, out var blobCore))
        {
            foreach (var tile in blobCore.BlobTiles)
            {
                if (Exists(tile) && tile != coreEntity)
                    QueueDel(tile);
            }

            QueueDel(coreEntity);
        }

        var rules = EntityQueryEnumerator<BlobRuleComponent>();
        while (rules.MoveNext(out _, out var rule))
            rule.Blobs.RemoveAll(entry => entry.Item1 == mindId);

        return true;
    }

    private readonly record struct BlobBodyBackup(EntityUid Body, MapCoordinates Coordinates);
}
