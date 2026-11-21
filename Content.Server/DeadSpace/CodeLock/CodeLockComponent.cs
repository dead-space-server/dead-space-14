using System.Threading;
using Content.Shared.DeadSpace.CodeLock;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.DeadSpace.CodeLock
{
    [RegisterComponent]
    [Access(typeof(CodeLockSystem))]
    public sealed partial class CodeLockComponent : SharedCodeLockComponent
    {
        public int LastPlayedKeypadSemitones = 0;

        [DataField] public int CodeMaxLength = 6;
        [DataField] public int CodeLength = 6;
        [DataField] public string Code = "";
        [DataField] public bool RandomizeCode = false;

        [DataField] public int Attempts = 0;
        [DataField] public int MaxAttempts = 3;

        [DataField]
        public int Cooldown = 30;
        [DataField]
        public float CooldownTime;

        [DataField]
        public string EnteredCode = "";

        [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan LastCodeEnteredAt = TimeSpan.Zero;

        [DataField]
        public CodeLockStatus Status = CodeLockStatus.AWAIT_CODE;

        [DataField("keypadPressSound")]
        public SoundSpecifier KeypadPressSound = new SoundPathSpecifier("/Audio/_Backmen/Machines/twobeep.ogg");

        [DataField("accessGrantedSound")]
        public SoundSpecifier AccessGrantedSound = new SoundPathSpecifier("/Audio/_Backmen/Machines/chime.ogg");

        [DataField("accessDeniedSound")]
        public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/_Backmen/Machines/buzz-sigh.ogg");
    }
}
