// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Text;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Components;
using Content.Shared.GameTicking;

namespace Content.Server.DeadSpace.NewLife;

public sealed partial class NewLifeSystem
{
    private void InitializeSpawning()
    {
        SubscribeLocalEvent<PlayerSpawnAttemptEvent>(OnSpawnAttempt);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnSpawnAttempt(ref PlayerSpawnAttemptEvent args)
    {
        if (!_records.TryGetValue(args.Player.UserId, out var record) || record.Use != UseState.Reserved)
            return;

        string? reason = null;
        if (!HasAccess(args.Player.UserId))
            reason = "new-life-unavailable-access";
        else if (GetRoundBlocker(args.Player.UserId) is { } blocker)
            reason = blocker;
        else if (_ticker.UserHasJoinedGame(args.Player))
            reason = "new-life-unavailable-ghost";
        else if (TerminatingOrDeleted(args.Station) || !HasComp<StationSpawningComponent>(args.Station) ||
                 !HasComp<StationJobsComponent>(args.Station))
            reason = "new-life-unavailable-station";
        else if (record.Names.Contains(NormalizeName(args.Profile.Name)))
            reason = "new-life-unavailable-name";

        if (reason == null)
            return;
        args.Cancelled = true;
        args.Reason = Loc.GetString(reason);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var record = GetRecord(args.Player.UserId);
        record.Names.Add(NormalizeName(args.Profile.Name));
        if (!TerminatingOrDeleted(args.Mob))
            record.Names.Add(NormalizeName(Name(args.Mob)));
        if (record.Use != UseState.Reserved)
            return;

        record.Use = UseState.Used;
        SendState(args.Player, true);
        _chat.DispatchServerMessage(args.Player, Loc.GetString("new-life-spawned"));
    }

    private static string NormalizeName(string name)
    {
        return string.Join(' ', name.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormKC);
    }
}
