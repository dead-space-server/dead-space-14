// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>Mayhem in a bottle: everyone humanoid nearby gets a stuck weapon, a healing rush and a murderous frenzy.</summary>
[RegisterComponent]
public sealed partial class LavalandMayhemBottleComponent : Component
{
    [DataField]
    public float Range = 7f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(2);

    [DataField]
    public EntProtoId Weapon = "Chainsaw";

    [DataField]
    public ProtoId<ReagentPrototype>? Reagent = "Omnizine";

    [DataField]
    public FixedPoint2 ReagentAmount = 25;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>A creature in a mayhem frenzy.</summary>
[RegisterComponent]
public sealed partial class LavalandMayhemAffectedComponent : Component
{
    [ViewVariables]
    public EntityUid? Weapon;

    public TimeSpan EndsAt;
}

public enum LavalandCrystalEffect : byte
{
    /// <summary>Fires a random projectile away from whoever touched it.</summary>
    Emitter,
    /// <summary>Revives dead humanoids nearby, optionally changing their species and outfit.</summary>
    Revive,
    /// <summary>Reshapes the floor around it into a theme.</summary>
    ThemeWarp,
}

/// <summary>Anomalous crystal from the colossus. Touch it to trigger its effect.</summary>
[RegisterComponent]
public sealed partial class LavalandAnomalousCrystalComponent : Component
{
    [DataField(required: true)]
    public LavalandCrystalEffect Effect;

    /// <summary>Shown only to ghosts and admins.</summary>
    [DataField]
    public LocId? ObserverDescription;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    /// <summary>Charging time between the touch and the effect.</summary>
    [DataField]
    public TimeSpan ChargeTime = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public List<EntProtoId> Projectiles = new();

    [DataField]
    public float ProjectileSpeed = 15f;

    [DataField]
    public float Range = 5f;

    [DataField]
    public ProtoId<SpeciesPrototype>? ReviveSpecies;

    [DataField]
    public ProtoId<StartingGearPrototype>? ReviveGear;

    [DataField]
    public List<ProtoId<ContentTileDefinition>> ThemeTiles = new();

    public TimeSpan NextUse;
    public TimeSpan? FireAt;
    public EntityUid? Activator;
}

/// <summary>The colossus' vocal cords: eat them to gain the voice of god.</summary>
[RegisterComponent]
public sealed partial class LavalandVoiceOfGodItemComponent : Component
{
    [DataField]
    public List<LavalandVoiceCommand> Commands = new();

    [DataField]
    public float Range = 7f;

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>Speaks with the voice of god: keywords in speech command everyone nearby.</summary>
[RegisterComponent]
public sealed partial class LavalandVoiceOfGodComponent : Component
{
    [DataField]
    public List<LavalandVoiceCommand> Commands = new();

    [DataField]
    public float Range = 7f;

    [DataField]
    public SoundSpecifier? Sound;

    public TimeSpan NextCommand;
}

[DataDefinition]
public sealed partial class LavalandVoiceCommand
{
    /// <summary>Lower-case words that trigger the command.</summary>
    [DataField(required: true)]
    public List<string> Words = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Stun = TimeSpan.Zero;

    [DataField]
    public TimeSpan Knockdown = TimeSpan.Zero;

    [DataField]
    public DamageSpecifier? Damage;

    /// <summary>Pushes listeners away from the speaker with this speed.</summary>
    [DataField]
    public float Repel;
}

/// <summary>H.E.C.K. suit curse: every now and then whispers to its wearer.</summary>
[RegisterComponent]
public sealed partial class LavalandCursedWhispersComponent : Component
{
    [DataField]
    public List<LocId> Messages = new();

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromMinutes(4);

    public EntityUid? Wearer;
    public TimeSpan NextWhisper;
}
