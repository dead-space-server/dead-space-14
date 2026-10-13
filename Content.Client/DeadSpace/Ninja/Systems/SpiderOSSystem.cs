// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.Power.Components;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class SpiderOSSystem : SharedSpiderOSSystem
{
    private readonly List<SpiderOSBootSession> _sessions = new();

    private enum BootPhase : byte
    {
        Playing = 0,
        Rollback = 1,
        WaitingConfirm = 2,
        WaitingLock = 3,
        Finishing = 4,
    }

    private sealed class SpiderOSBootSession
    {
        public EntityUid Suit = default;
        public SpiderOSBootPrototype Script = default!;
        public int StepIndex;
        public float Remaining;
        public BootPhase Phase;
        public bool ExpectedActive;

        public bool LockRequested;

        public SpiderOSBootCheck PendingCheck;

        public Action<LocId, (string, object)[]?>? OnLog;
        public Action<float>? OnProgress;
        public Action<LocId>? OnCheckFailed;
        public Action<bool, SpiderOSBootCheck>? OnSecureRequest;
        public Action? OnLockRollback;
        public Action? OnFinished;
        public Action? OnConfirmed;
        public Action? OnRolledBack;
    }

    public sealed class SpiderOSBootCallbacks
    {
        public Action<LocId, (string, object)[]?>? OnLog;
        public Action<float>? OnProgress;
        public Action<LocId>? OnCheckFailed;
        public Action<bool, SpiderOSBootCheck>? OnSecureRequest;
        public Action? OnLockRollback;
        public Action? OnFinished;
        public Action? OnConfirmed;
        public Action? OnRolledBack;
    }

    public void StartBoot(EntityUid suit, SpiderOSBootPrototype boot, bool expectedActive, SpiderOSBootCallbacks callbacks)
    {
        CancelBoot(suit);

        var session = new SpiderOSBootSession
        {
            Suit = suit,
            Script = boot,
            ExpectedActive = expectedActive,
            Remaining = boot.Steps.Count > 0 ? boot.Steps[0].Delay : 0f,
            Phase = BootPhase.Playing,
            OnLog = callbacks.OnLog,
            OnProgress = callbacks.OnProgress,
            OnCheckFailed = callbacks.OnCheckFailed,
            OnSecureRequest = callbacks.OnSecureRequest,
            OnLockRollback = callbacks.OnLockRollback,
            OnFinished = callbacks.OnFinished,
            OnConfirmed = callbacks.OnConfirmed,
            OnRolledBack = callbacks.OnRolledBack,
        };

        _sessions.Add(session);
    }

    public void CancelBoot(EntityUid suit)
    {
        for (var i = _sessions.Count - 1; i >= 0; i--)
        {
            if (_sessions[i].Suit == suit)
            {
                _sessions.RemoveAt(i);
            }
        }
    }

    public void ConfirmSecure(EntityUid suit, bool success, LocId failReason)
    {

        foreach (var session in _sessions)
        {
            if (session.Suit != suit || session.Phase != BootPhase.WaitingLock)
                continue;

            if (!success)
            {
                session.OnLog?.Invoke(session.Script.Steps[session.StepIndex].Log, null);
                FailBoot(session, failReason);
                return;
            }

            FireStep(session, session.Script.Steps[session.StepIndex]);
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _sessions.Count - 1; i >= 0; i--)
        {
            var session = _sessions[i];

            if (!Exists(session.Suit))
            {
                _sessions.RemoveAt(i);
                continue;
            }

            switch (session.Phase)
            {
                case BootPhase.Playing:
                    UpdatePlaying(session, frameTime);
                    session.OnProgress?.Invoke(StepProgress(session));
                    break;
                case BootPhase.Rollback:
                    session.Remaining -= frameTime;
                    if (session.Remaining <= 0f)
                    {
                        _sessions.RemoveAt(i);
                        session.OnRolledBack?.Invoke();
                    }
                    break;
                case BootPhase.WaitingConfirm:
                    session.Remaining -= frameTime;
                    if (TryComp<SpiderOSComponent>(session.Suit, out var comp) && comp.SuitActivated == session.ExpectedActive)
                    {
                        _sessions.RemoveAt(i);
                        session.OnConfirmed?.Invoke();
                    }
                    else if (session.Remaining <= 0f)
                    {
                        BeginRollback(session);
                    }
                    break;
                case BootPhase.WaitingLock:
                    session.Remaining -= frameTime;
                    if (session.Remaining <= 0f)
                    {
                        FailBoot(session, "spider-os-boot-fail-timeout");
                    }
                    break;
                case BootPhase.Finishing:
                    session.Remaining -= frameTime;
                    if (session.Remaining <= 0f)
                    {
                        session.Phase = BootPhase.WaitingConfirm;
                        session.Remaining = ConfirmTimeoutSeconds;
                        session.OnFinished?.Invoke();
                    }
                    break;
            }
        }
    }

    private void UpdatePlaying(SpiderOSBootSession session, float frameTime)
    {
        if (session.StepIndex >= session.Script.Steps.Count)
        {
            session.Phase = BootPhase.Finishing;
            session.Remaining = FinishDelaySeconds;
            return;
        }

        session.Remaining -= frameTime;
        if (session.Remaining > 0f)
        {
            return;
        }

        var step = session.Script.Steps[session.StepIndex];

        if (step.Check is { } check)
        {
            var actor = SharedTransform.GetParentUid(session.Suit);
            if (!RunBootCheck(session.Suit, actor, check, out var failReason))
            {
                session.OnLog?.Invoke(step.Log, null);
                FailBoot(session, failReason);
                return;
            }

            if (check is SpiderOSBootCheck.BatteryReport)
            {
                LogBatteryLevel(session);
                FireStep(session, step, log: false);
                return;
            }

            if (check is SpiderOSBootCheck.SuitFasten || SuitHardwareSlots.ContainsKey(check))
            {
                session.LockRequested |= session.ExpectedActive;
                session.PendingCheck = check;
                session.OnSecureRequest?.Invoke(session.ExpectedActive, check);
                session.Phase = BootPhase.WaitingLock;
                session.Remaining = LockTimeoutSeconds;
                return;
            }
        }

        FireStep(session, step);
    }

    private void LogBatteryLevel(SpiderOSBootSession session)
    {
        if (!PowerCell.TryGetBatteryFromSlotOrEntity(session.Suit, out var battery) || !TryComp<BatteryComponent>(battery, out var batteryComponent))
        {
            return;
        }

        var charge = (int)Math.Round(batteryComponent.MaxCharge);
        session.OnLog?.Invoke("spider-os-boot-battery-capacity", new (string, object)[] { ("capacity", charge) });
    }

    private static void FireStep(SpiderOSBootSession session, SpiderOSBootStep step, bool log = true)
    {
        if (log)
        {
            session.OnLog?.Invoke(step.Log, null);
        }

        session.StepIndex++;

        if (session.StepIndex < session.Script.Steps.Count)
        {
            session.Phase = BootPhase.Playing;
            session.Remaining = session.Script.Steps[session.StepIndex].Delay;
        }
        else
        {
            session.Phase = BootPhase.Finishing;
            session.Remaining = FinishDelaySeconds;
        }
    }

    private void BeginRollback(SpiderOSBootSession session)
    {
        if (session.ExpectedActive && session.LockRequested)
        {
            session.OnLockRollback?.Invoke();
        }

        session.Phase = BootPhase.Rollback;
        session.Remaining = RollbackDelaySeconds;
    }

    private void FailBoot(SpiderOSBootSession session, LocId failReason)
    {
        BeginRollback(session);
        session.OnCheckFailed?.Invoke(failReason);
    }

    private static float StepProgress(SpiderOSBootSession session)
    {
        var total = session.Script.Steps.Count;
        if (total == 0)
        {
            return 1f;
        }

        var i = Math.Clamp(session.StepIndex, 0, total);
        if (i >= total)
        {
            return 1f;
        }

        var delay = session.Script.Steps[i].Delay;
        return delay > 0f
            ? Math.Clamp(1f - session.Remaining / delay, 0f, 1f)
            : 1f;
    }
}