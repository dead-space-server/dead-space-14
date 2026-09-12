// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Audio;
using Content.Shared.Alert;

namespace Content.Shared.DeadSpace.TheCircle.Legion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LegionComponent : Component
{
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(10);
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(30);
    [DataField] public float SpeedModifier = 1.35f;
    [DataField] public TimeSpan DoorSlowDuration = TimeSpan.FromSeconds(2);
    [DataField] public float DoorSlowModifier = 0.5f;
    [DataField] public TimeSpan RevealInterval = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan RevealDuration = TimeSpan.FromSeconds(1);
    [DataField] public SoundSpecifier HeartbeatSound = new SoundPathSpecifier("/Audio/_DeadSpace/Effects/Heartbeat/singlebeat.ogg");
    [DataField] public ProtoId<AlertPrototype> RageAlert = "LegionRage";
    [DataField, AutoNetworkedField] public bool Active;
    [ViewVariables] public TimeSpan EndsAt;
    [ViewVariables] public TimeSpan NextReveal;
    [ViewVariables] public Dictionary<EntityUid, int> Hits = new();
    [ViewVariables] public bool RevealStarted;
    [AutoNetworkedField] public bool RevealPulseActive;
    [ViewVariables] public EntityUid? HeartbeatStream;
    [ViewVariables] public TimeSpan CooldownEndsAt;
}

[RegisterComponent]
public sealed partial class LegionKnifeComponent : Component
{
    [DataField] public float Vampirism = 1.1f;
    [DataField] public float BloodRestore = 25f;
    [DataField] public float SecondPerkVampirism = 0.1f;
    [DataField] public TimeSpan SecondHitDamageDuration = TimeSpan.FromSeconds(5);
    [DataField] public float SecondHitDamage = 2f;
}

[RegisterComponent]
public sealed partial class LegionSurvivalPerkComponent : Component
{
    [DataField] public float TriggerDamage = 100f;
    [DataField] public float EndDamage = 150f;
    [DataField] public TimeSpan Window = TimeSpan.FromSeconds(10);
    [DataField] public int RequiredVictims = 5;
    [ViewVariables] public float DamageTaken;
    [ViewVariables] public TimeSpan? ActiveUntil;
    [ViewVariables] public HashSet<EntityUid> Victims = new();
}

[RegisterComponent]
public sealed partial class LegionPredatorPerkComponent : Component
{
    [DataField] public int RequiredVictims = 5;
    [DataField] public float SpeedBonus = 0.1f;
    [DataField] public float VampirismBonus = 0.1f;
    [ViewVariables] public HashSet<EntityUid> Victims = new();
    [ViewVariables] public bool Activated;
}

public sealed partial class LegionRageActionEvent : InstantActionEvent;

public sealed class LegionKnifeRageAttemptEvent(EntityUid user) : EntityEventArgs
{
    public EntityUid User = user;
}
