// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared.DeadSpace.Weapons.Parry;
using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Melee;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Weapons.Parry;

/// <summary>
/// The parry alert shows the weapon that parries (the one the player would hit with now), not a generic icon.
/// </summary>
public sealed class ParryAlertIconSystem : EntitySystem
{
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, UpdateAlertSpriteEvent>(OnUpdateAlertSprite);
    }

    private void OnUpdateAlertSprite(Entity<HandsComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (!_melee.TryGetWeapon(ent, out var weapon, out _) ||
            weapon == ent.Owner ||
            !TryComp<ParryComponent>(weapon, out var parry) ||
            args.Alert.ID != parry.CooldownAlert)
        {
            return;
        }

        var spriteView = args.SpriteViewEnt;
        if (TryComp<IconComponent>(weapon, out var icon))
        {
            _sprite.LayerSetTexture((spriteView.Owner, (SpriteComponent?) spriteView.Comp), AlertVisualLayers.Base, _sprite.GetIcon(icon));
            return;
        }

        if (MetaData(weapon).EntityPrototype is { } proto)
            _sprite.LayerSetTexture((spriteView.Owner, (SpriteComponent?) spriteView.Comp), AlertVisualLayers.Base, _sprite.GetPrototypeIcon(proto.ID).Default);
    }
}
