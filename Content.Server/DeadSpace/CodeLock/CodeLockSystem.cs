using System.Linq;
using Content.Server.Audio;
using Content.Shared.Audio;
using Content.Shared.DeadSpace.CodeLock;
using Content.Shared.Lock;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.CodeLock;

public sealed partial class CodeLockSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CodeLockComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CodeLockComponent, BoundUIClosedEvent>(OnBoundUiClosed);

        SubscribeLocalEvent<CodeLockComponent, CodeLockKeypadMessage>(OnKeypadButtonPressed);
        SubscribeLocalEvent<CodeLockComponent, CodeLockKeypadClearMessage>(OnClearButtonPressed);
        SubscribeLocalEvent<CodeLockComponent, CodeLockKeypadEnterMessage>(OnEnterButtonPressed);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CodeLockComponent>();
        while (query.MoveNext(out var uid, out var codelock))
        {
            if (codelock.Status != CodeLockStatus.COOLDOWN)
                continue;

            TickCooldown(uid, frameTime, codelock);
        }
    }

    private void OnMapInit(EntityUid uid, CodeLockComponent codelock, MapInitEvent args)
    {
        if (codelock.RandomizeCode)
        {
            codelock.Code = GenerateRandomNumberString(codelock.CodeLength);
        }

        if (codelock.RandomizeCode == false && codelock.Code == "")
        {
            codelock.Code = new string('0', codelock.CodeLength);
        }

        UpdateUserInterface(uid, codelock);
    }

    #region UI Events

    private void OnEnterButtonPressed(EntityUid uid, CodeLockComponent component, CodeLockKeypadEnterMessage args)
    {
        _audio.PlayPvs(component.KeypadPressSound, uid);

        if (component.Status == CodeLockStatus.COOLDOWN)
            return;

        var curTime = _timing.CurTime;
        if (curTime < component.LastCodeEnteredAt + SharedCodeLockComponent.EnterCodeCooldown)
            return;

        component.LastCodeEnteredAt = curTime;

        if (component.Status == CodeLockStatus.UNLOCKED)
        {
            if (TryComp<LockComponent>(uid, out var lockComp))
            {
                _lock.Lock(uid, null, lockComp);
            }
            component.Status = CodeLockStatus.AWAIT_CODE;
            UpdateUserInterface(uid, component);
            return;
        }

        UpdateStatus(uid, component);
        UpdateUserInterface(uid, component);
    }

    private void OnKeypadButtonPressed(EntityUid uid, CodeLockComponent component, CodeLockKeypadMessage args)
    {
        if (args.Value is < 0 or > 9)
            return;

        PlayCodeLockKeypadSound(uid, args.Value, component);

        if (component.Status == CodeLockStatus.COOLDOWN || component.Status == CodeLockStatus.UNLOCKED)
            return;

        if (component.EnteredCode.Length >= component.CodeLength && component.Status != CodeLockStatus.CHANGE)
            return;

        if (component.EnteredCode.Length >= component.CodeMaxLength)
            return;

        component.EnteredCode += args.Value.ToString();
        UpdateUserInterface(uid, component);
    }

    private void OnClearButtonPressed(EntityUid uid, CodeLockComponent component, CodeLockKeypadClearMessage args)
    {
        _audio.PlayPvs(component.KeypadPressSound, uid);

        if (component.Status == CodeLockStatus.COOLDOWN)
            return;

        if (component.Status == CodeLockStatus.UNLOCKED)
        {
            component.Status = CodeLockStatus.CHANGE;
        }

        component.EnteredCode = "";

        UpdateUserInterface(uid, component);
    }

    private void OnBoundUiClosed(EntityUid uid, CodeLockComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey is not CodeLockUiKey.Key)
            return;

        if (_ui.GetActors(uid, CodeLockUiKey.Key).Any())
            return;

        component.EnteredCode = "";

        if (component.Status == CodeLockStatus.CHANGE)
            component.Status = CodeLockStatus.UNLOCKED;

        UpdateUserInterface(uid, component);
    }

    #endregion

    private void TickCooldown(EntityUid uid, float frameTime, CodeLockComponent? codelock = null)
    {
        if (!Resolve(uid, ref codelock))
            return;

        codelock.CooldownTime -= frameTime;
        if (codelock.CooldownTime <= 0)
        {
            codelock.CooldownTime = 0;
            codelock.Status = CodeLockStatus.AWAIT_CODE;
        }

        UpdateUserInterface(uid, codelock);
    }

    public void CodeLockStatusChanged(EntityUid uid, CodeLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var ev = new CodeLockStatusChangedEvent(component.Status);
        RaiseLocalEvent(uid, ref ev);
    }

    private void UpdateStatus(EntityUid uid, CodeLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        switch (component.Status)
        {
            case CodeLockStatus.AWAIT_CODE:
                if (component.EnteredCode == component.Code)
                {
                    if (TryComp<LockComponent>(uid, out var lockComp))
                    {
                        _lock.Unlock(uid, null, lockComp);
                    }
                    component.EnteredCode = "";
                    component.Status = CodeLockStatus.UNLOCKED;
                    component.Attempts = 0;
                }
                else
                {
                    component.Attempts += 1;
                    if (component.Attempts >= component.MaxAttempts)
                    {
                        component.Status = CodeLockStatus.COOLDOWN;
                        component.CooldownTime = component.Cooldown;
                        component.Attempts = 0;
                    }

                    component.EnteredCode = "";
                    _audio.PlayPvs(component.AccessDeniedSound, uid);
                }
                CodeLockStatusChanged(uid, component);
                break;
            case CodeLockStatus.UNLOCKED:
                CodeLockStatusChanged(uid, component);
                break;
            case CodeLockStatus.CHANGE:
                if (component.EnteredCode != "")
                {
                    component.Code = component.EnteredCode;
                    component.CodeLength = component.Code.Length;
                    component.EnteredCode = "";
                    _audio.PlayPvs(component.AccessGrantedSound, uid);
                    component.Status = CodeLockStatus.AWAIT_CODE;
                    if (TryComp<LockComponent>(uid, out var lockComp))
                    {
                        _lock.Lock(uid, null, lockComp);
                    }
                }
                CodeLockStatusChanged(uid, component);
                break;
        }
    }

    private void UpdateUserInterface(EntityUid uid, CodeLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!_ui.HasUi(uid, CodeLockUiKey.Key))
            return;

        var state = new CodeLockUiState
        {
            Status = component.Status,
            EnteredCodeLength = component.EnteredCode.Length,
            CodeLength = component.CodeLength,
            MaxCodeLength = component.CodeMaxLength,
            EnteredCode = component.EnteredCode,
            CooldownTime = (int) component.CooldownTime
        };

        _ui.SetUiState(uid, CodeLockUiKey.Key, state);
    }

    private void PlayCodeLockKeypadSound(EntityUid uid, int number, CodeLockComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var semitoneShift = number switch
        {
            1 => 0,
            2 => 2,
            3 => 3,
            4 => 4,
            5 => 5,
            6 => 6,
            7 => 7,
            8 => 9,
            9 => 10,
            0 => component.LastPlayedKeypadSemitones + 12,
            _ => 0,
        };

        component.LastPlayedKeypadSemitones = number == 0 ? component.LastPlayedKeypadSemitones : semitoneShift;

        var opts = component.KeypadPressSound.Params;
        opts = AudioHelpers.ShiftSemitone(opts, semitoneShift).AddVolume(-5f);
        _audio.PlayPvs(component.KeypadPressSound, uid, opts);
    }

    public string GenerateRandomNumberString(int length)
    {
        var ret = "";
        for (var i = 0; i < length; i++)
        {
            var c = (char) _random.Next('0', '9' + 1);
            ret += c;
        }

        return ret;
    }
}
