using System.Security.Cryptography;
using System.Text;
using Content.Shared.DeadSpace.Terminal;
using Content.Shared.GameTicking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Terminal;

public sealed partial class TerminalSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string TerminalInterfaceType = "TerminalBoundUserInterface";
    private const int MaxHistory = 50;
    private const float LanSpeedBPS = 100_000f;
    private const float BaseLatencySeconds = 0.05f;
    private const float SignalSpeedTilesPerSecond = 20f;

    private sealed class PendingTransfer
    {
        public float RemainingSeconds;
        public Action Deliver = default!;
    }

    private readonly List<PendingTransfer> _transfers = new();
    private readonly Dictionary<EntityUid, TerminalMachine> _machines = new();
    private readonly HashSet<string> _ips = new();

    private const string RootPasswordAlphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int RootPasswordLength = 8;

    public string RootPassword { get; private set; } = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        GenerateRootPassword();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<TerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerminalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerminalComponent, TerminalCommandMessage>(OnCommand);
        SubscribeLocalEvent<TerminalComponent, TerminalSaveFileMessage>(OnSaveFile);
        SubscribeLocalEvent<TerminalComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<TerminalComponent, PowerChangedEvent>(OnPowerChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _transfers.Count - 1; i >= 0; i--)
        {
            var transfer = _transfers[i];
            transfer.RemainingSeconds -= frameTime;

            if (transfer.RemainingSeconds > 0)
                continue;

            _transfers.RemoveAt(i);
            transfer.Deliver();
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        GenerateRootPassword();
    }

    private void GenerateRootPassword()
    {
        var builder = new StringBuilder(RootPasswordLength);
        for (var i = 0; i < RootPasswordLength; i++)
            builder.Append(RootPasswordAlphabet[RandomNumberGenerator.GetInt32(RootPasswordAlphabet.Length)]);

        RootPassword = builder.ToString();
    }

    private void OnMapInit(EntityUid uid, TerminalComponent comp, MapInitEvent args)
    {
        GenerateIp(comp);
        comp.UserIndex = Random.Shared.Next(1000, 10000);

        var machine = GetMachine(uid, comp);
        ResetSession(comp, machine);
        ApplySystemFiles(comp, machine);

        if (!_ui.HasUi(uid, TerminalUiKey.Key))
            _ui.SetUi(uid, TerminalUiKey.Key, new InterfaceData(TerminalInterfaceType));

        Dirty(uid, comp);
    }

    private void OnShutdown(EntityUid uid, TerminalComponent comp, ComponentShutdown args)
    {
        _ips.Remove(comp.IpAdress);
        _machines.Remove(uid);
    }

    private void OnPowerChanged(EntityUid uid, TerminalComponent comp, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            _ui.CloseUi(uid, TerminalUiKey.Key);
    }

    private void OnGetVerbs(EntityUid uid, TerminalComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (TryComp<ActivatableUIComponent>(uid, out var activatable) && Equals(activatable.Key, TerminalUiKey.Key))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("terminal-verb-open"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => OpenTerminal(uid, user),
        });
    }

    private void OpenTerminal(EntityUid uid, EntityUid user)
    {
        if (!_power.IsPowered(uid))
            return;

        if (!_ui.HasUi(uid, TerminalUiKey.Key))
            _ui.SetUi(uid, TerminalUiKey.Key, new InterfaceData(TerminalInterfaceType));

        _ui.OpenUi(uid, TerminalUiKey.Key, user);
    }

    private void ReturnToConsole(EntityUid uid, EntityUid? actor)
    {
        if (actor is not { } user)
            return;

        _ui.CloseUi(uid, TerminalUiKey.Key, user);

        if (TryComp<ActivatableUIComponent>(uid, out var activatable) && activatable.Key is { } key && !Equals(key, TerminalUiKey.Key))
            _ui.OpenUi(uid, key, user);
    }

    private TerminalContext CreateContext(EntityUid uid, TerminalComponent comp, EntityUid? actor)
    {
        return new TerminalContext
        {
            Uid = uid,
            Comp = comp,
            Machine = GetMachine(uid, comp),
            Actor = actor,
            User = comp.User,
        };
    }

    private void OnCommand(EntityUid uid, TerminalComponent comp, TerminalCommandMessage message)
    {
        var ctx = CreateContext(uid, comp, message.Actor);
        var machine = ctx.Machine;
        string output;

        if (machine.Pending is { } pending)
        {
            machine.Pending = null;
            output = pending.Handler(ctx, message.PromptText);
        }
        else
        {
            var line = message.PromptText.Trim();
            if (line.Length > 0)
            {
                machine.History.Add(line);
                if (machine.History.Count > MaxHistory)
                    machine.History.RemoveAt(0);
            }

            output = ExecuteLine(ctx, line);
        }

        ApplySystemFiles(comp, machine);
        Dirty(uid, comp);

        if (ctx.Close)
        {
            ReturnToConsole(uid, ctx.Actor);
            return;
        }

        if (ctx.Deferred && output.Length == 0)
            return;

        SendState(ctx, output);
    }

    private void SendState(TerminalContext ctx, string output)
    {
        var pending = ctx.Machine.Pending;
        var prompt = pending?.Prompt ?? ctx.Comp.Prompt;
        _ui.SetUiState(ctx.Uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(output, prompt, pending != null, ctx.Clear, ctx.EditorPath, ctx.EditorContent));
    }

    private void SendOutput(EntityUid uid, TerminalComponent comp, string output)
    {
        _ui.SetUiState(uid, TerminalUiKey.Key, new TerminalBoundUserInterfaceState(output, comp.Prompt));
    }

    private void OnSaveFile(EntityUid uid, TerminalComponent comp, TerminalSaveFileMessage message)
    {
        var machine = GetMachine(uid, comp);
        if (machine.EditorPath == null || machine.EditorPath != message.Path)
            return;

        var ctx = CreateContext(uid, comp, message.Actor);
        ctx.User = machine.EditorUser ?? comp.User;

        var error = WriteFile(ctx, machine.EditorPath, message.Content, false);
        ApplySystemFiles(comp, machine);
        Dirty(uid, comp);

        SendOutput(uid, comp, error ?? $"{Loc.GetString("terminal-nano-file-saved", ("filePath", machine.EditorPath))}\n");
    }

    private void GenerateIp(TerminalComponent comp)
    {
        do
        {
            comp.IpFirst = Random.Shared.Next(0, 256);
            comp.IpSecond = Random.Shared.Next(0, 256);
            comp.IpThird = Random.Shared.Next(0, 256);
        }
        while (!_ips.Add(comp.IpAdress));
    }

    private bool TryFindByIp(string address, out EntityUid terminal)
    {
        var query = EntityQueryEnumerator<TerminalComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IpAdress == address)
            {
                terminal = uid;
                return true;
            }
        }

        terminal = default;
        return false;
    }

    private float GetTransferDelay(EntityUid sender, EntityUid receiver, int sizeBytes)
    {
        var senderCoords = _transform.GetMapCoordinates(sender);
        var receiverCoords = _transform.GetMapCoordinates(receiver);

        if (senderCoords.MapId != receiverCoords.MapId)
            return float.PositiveInfinity;

        var distance = (senderCoords.Position - receiverCoords.Position).Length();
        return BaseLatencySeconds + distance / SignalSpeedTilesPerSecond + sizeBytes / LanSpeedBPS;
    }

    private void SendWithDelay(EntityUid sender, EntityUid receiver, int sizeBytes, Action deliver)
    {
        var delay = GetTransferDelay(sender, receiver, sizeBytes);

        if (float.IsPositiveInfinity(delay))
            return;

        _transfers.Add(new PendingTransfer
        {
            RemainingSeconds = delay,
            Deliver = deliver,
        });
    }

    private string CmdPing(TerminalContext ctx, List<string> args)
    {
        if (args.Count == 0)
            return Fail(ctx, Loc.GetString("terminal-ping-usage"));
        if (args.Count > 1)
            return Fail(ctx, Loc.GetString("terminal-many-args"));

        var sender = ctx.Uid;
        var address = args[0];
        EntityUid receiver;

        if (address == "localhost" || address == "127.0.0.1")
            receiver = sender;
        else if (!TryFindByIp(address, out receiver))
            return Fail(ctx, Loc.GetString("terminal-ping-host-not-found", ("ip", address)));

        if (!TryComp<TerminalComponent>(receiver, out var receiverComp))
            return Fail(ctx, Loc.GetString("terminal-ping-network-error"));

        if (!ctx.Comp.NetworkEnabled || !receiverComp.NetworkEnabled)
            return Fail(ctx, Loc.GetString("terminal-ping-host-unreachable"));

        var oneWayDelay = GetTransferDelay(sender, receiver, 64);
        if (float.IsPositiveInfinity(oneWayDelay))
            return Fail(ctx, Loc.GetString("terminal-ping-host-unreachable"));

        var milliseconds = Math.Max(1, (int) Math.Round(oneWayDelay * 2f * 1000f));
        ctx.Deferred = true;

        SendWithDelay(sender, receiver, 64, () =>
        {
            if (!TryComp<TerminalComponent>(receiver, out var currentReceiver) || !currentReceiver.NetworkEnabled)
                return;

            SendWithDelay(receiver, sender, 64, () =>
            {
                if (!TryComp<TerminalComponent>(sender, out var currentSender) || !currentSender.NetworkEnabled)
                    return;

                SendOutput(sender, currentSender, $"64 bytes from {address}: time={milliseconds} ms\n");
            });
        });

        return string.Empty;
    }

    private string CmdIfconfig(TerminalContext ctx, List<string> args)
    {
        var state = ctx.Comp.NetworkEnabled ? "UP" : "DOWN";
        return $"eth0: flags=<{state},BROADCAST,RUNNING>\n        inet {ctx.Comp.IpAdress}  netmask 255.0.0.0\nlo: flags=<UP,LOOPBACK,RUNNING>\n        inet 127.0.0.1  netmask 255.0.0.0\n";
    }
}
