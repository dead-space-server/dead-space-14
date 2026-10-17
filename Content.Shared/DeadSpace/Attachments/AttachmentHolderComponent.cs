// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Attachments;

/// <summary>
/// A weapon that takes attachments. The slots themselves are ordinary <c>ItemSlots</c> (whitelist, eject verb, sounds);
/// this lists which of them hold attachments.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class AttachmentHolderComponent : Component
{
    /// <summary>ItemSlots slot ids that hold attachments.</summary>
    [DataField(required: true)]
    public List<string> Slots = new();

    /// <summary>
    /// RSI for attachment layers that don't name their own sprite. Defaults to the weapon's own RSI;
    /// set it when the weapon art lives elsewhere (e.g. upstream crusher + Goob upgrade overlays).
    /// </summary>
    [DataField]
    public ResPath? OverlaySprite;

    /// <summary>
    /// Components the weapon gets while a slot holds anything at all, for slots that take ordinary items
    /// (a knife as a bayonet gives the gun a melee attack). Restored like attachment components.
    /// </summary>
    [DataField]
    public Dictionary<string, ComponentRegistry> SlotComponents = new();

    /// <summary>
    /// Slot for a bayonet: the weapon's melee attack takes the stats of the knife in it
    /// (damage, attack rate, reach, sounds).
    /// </summary>
    [DataField]
    public string? BayonetSlot;

    /// <summary>Stab animation of the bayonet instead of the knife's own. Null keeps the knife's.</summary>
    [DataField]
    public EntProtoId? BayonetAnimation;

    /// <summary>Slot for a flashlight; the weapon shines with it, toggled from the verb menu.</summary>
    [DataField]
    public string? LightSlot;

    [ViewVariables]
    [AutoNetworkedField]
    public bool LightOn;

    /// <summary>Action given to whoever holds the weapon when it has a flashlight slot.</summary>
    [DataField]
    public EntProtoId LightAction = "ActionAttachmentToggleLight";

    [DataField]
    public EntityUid? LightActionEntity;

    /// <summary>Kills made mostly with this weapon can drop crusher trophies.</summary>
    [DataField]
    public bool TrophyHunter;

    /// <summary>
    /// Sprite drawn over the weapon while something sits in a slot (tg style: knife under the barrel, flashlight).
    /// Keyed by slot id; offsets are per weapon sprite.
    /// </summary>
    [DataField]
    public Dictionary<string, AttachmentSlotOverlay> SlotOverlays = new();

    /// <summary>Server: components the attachments replaced or removed, restored on detach. Keyed by attachment.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, AppliedAttachment> Applied = new();

    /// <summary>Server: melee range before attachments.</summary>
    [ViewVariables]
    public float? BaseRange;

    /// <summary>Server: recharge time of self-recharging ammo before attachments.</summary>
    [ViewVariables]
    public float? BaseRechargeCooldown;
}

/// <summary>
/// RSI for attachment overlays of a weapon whose own art has no upgrade-* states
/// (upstream crusher + Goob overlays). A separate component so prototypes don't redefine the holder.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AttachmentOverlaySpriteComponent : Component
{
    [DataField(required: true)]
    public ResPath Sprite;
}

public sealed class AppliedAttachment
{
    /// <summary>Component names the attachment added that the weapon did not have.</summary>
    public readonly List<string> Added = new();

    /// <summary>Copies of the weapon's own components that were replaced or removed.</summary>
    public readonly List<IComponent> Saved = new();
}

[DataDefinition]
public sealed partial class AttachmentSlotOverlay
{
    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public string State = string.Empty;

    /// <summary>State while the weapon light is on (light slot only).</summary>
    [DataField]
    public string? StateOn;

    [DataField]
    public System.Numerics.Vector2 Offset;
}
