// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.ExecutionChair;

/// <summary>
/// Электрический стул. Питается напрямую от высоковольтного кабеля под ним.
/// Не бьёт током сам по себе: каждый принятый сигнал - это ровно один разряд
/// по всем пристёгнутым. Трупы стул игнорирует.
/// </summary>
[RegisterComponent, Access(typeof(ExecutionChairSystem))]
public sealed partial class ExecutionChairComponent : Component
{

    [DataField]
    public int ShockDamage = 100;

    [DataField]
    public TimeSpan ShockDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public TimeSpan NextShock = TimeSpan.Zero;

    [DataField]
    public float RequiredPowerRatio = 0.999f;

    [DataField]
    public bool PlaySoundOnShock = true;

    [DataField]
    public SoundSpecifier ShockNoises = new SoundCollectionSpecifier("sparks");

    [DataField]
    public float ShockVolume = 20f;

    [DataField]
    public ProtoId<SinkPortPrototype> TriggerPort = "Trigger";
}
