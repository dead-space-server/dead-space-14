using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Rounding;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;
using Content.Shared.DeadSpace.Dominator;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private TagSystem _tagSystem = default!;

    private static readonly ProtoId<TagPrototype> BatteryWeaponFireModesTag = "BatteryWeaponFireModesSprites";

    private void InitializeMagazineVisuals()
    {
        SubscribeLocalEvent<MagazineVisualsComponent, ComponentInit>(OnMagazineVisualsInit);
        SubscribeLocalEvent<MagazineVisualsComponent, AppearanceChangeEvent>(OnMagazineVisualsChange);
        SubscribeLocalEvent<MagazineVisualsComponent, GetInhandVisualsEvent>(OnGetHeldVisuals);
    }

    private void OnMagazineVisualsInit(Entity<MagazineVisualsComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)) return;

        // DS14: fire modes logic for battery weapons
        if (_tagSystem.HasTag(ent, BatteryWeaponFireModesTag))
        {
            if (TryComp<BatteryWeaponFireModesComponent>(ent, out var batteryFireModes))
            {
                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{ent.Comp.MagSteps - 1}-{batteryFireModes.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, false);
                }

                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{ent.Comp.MagSteps - 1}-{batteryFireModes.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, false);
                }
            }
            else if (TryComp<DominatorComponent>(ent, out var dominator))
            {
                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{ent.Comp.MagSteps - 1}-{dominator.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, false);
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Base, $"base-{dominator.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Base, false);
                }
                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{ent.Comp.MagSteps - 1}-{dominator.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, false);
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Base, $"base-{dominator.CurrentFireMode}");
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Base, false);
                }
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
            {
                _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{ent.Comp.MagSteps - 1}");
                _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, false);
            }

            if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
            {
                _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{ent.Comp.MagSteps - 1}");
                _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, false);
            }
        }
    }

    private void OnMagazineVisualsChange(Entity<MagazineVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        // tl;dr
        // 1.If no mag then hide it OR
        // 2. If step 0 isn't visible then hide it (mag or unshaded)
        // 3. Otherwise just do mag / unshaded as is
        var sprite = args.Sprite;

        if (sprite == null) return;

        if (!args.AppearanceData.TryGetValue(AmmoVisuals.MagLoaded, out var magloaded) ||
            magloaded is true)
        {
            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoMax, out var capacity))
            {
                capacity = ent.Comp.MagSteps;
            }

            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoCount, out var current))
            {
                current = 0;
            }

            // Treat invalid capacity as empty so RoundToLevels' (actual >= max) branch
            // doesn't accidentally render the FULL sprite for an unloaded mag.
            var step = (int)capacity <= 0
                ? 0
                : ContentHelpers.RoundToLevels((int)current, (int)capacity, ent.Comp.MagSteps);

            if (step == 0 && !ent.Comp.ZeroVisible)
            {
                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, false);
                }

                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, false);
                }

                return;
            }
            // DS14: fire modes logic for battery weapons
            if (_tagSystem.HasTag(ent, BatteryWeaponFireModesTag))
            {
                if (TryComp<BatteryWeaponFireModesComponent>(ent, out var batteryFireModes))
                {
                    if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                    {
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{step}-{batteryFireModes.CurrentFireMode}");
                    }

                    if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                    {
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{step}-{batteryFireModes.CurrentFireMode}");
                    }
                }
                else if (TryComp<DominatorComponent>(ent, out var dominator))
                {
                    if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                    {
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{step}-{dominator.CurrentFireMode}");
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Base, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Base, $"base-{dominator.CurrentFireMode}");
                    }

                    if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                    {
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{step}-{dominator.CurrentFireMode}");
                        _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Base, true);
                        _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Base, $"base-{dominator.CurrentFireMode}");
                    }
                }
            }
            else
            {
                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, true);
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.Mag, $"{ent.Comp.MagState}-{step}");
                }

                if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, true);
                    _sprite.LayerSetRsiState((ent, sprite), GunVisualLayers.MagUnshaded, $"{ent.Comp.MagState}-unshaded-{step}");
                }
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.Mag, out _, false))
            {
                _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.Mag, false);
            }

            if (_sprite.LayerMapTryGet((ent, sprite), GunVisualLayers.MagUnshaded, out _, false))
            {
                _sprite.LayerSetVisible((ent, sprite), GunVisualLayers.MagUnshaded, false);
            }
        }
    }

    private void OnGetHeldVisuals(EntityUid uid, MagazineVisualsComponent component, GetInhandVisualsEvent args)
    {
        if (!TryComp(uid, out AppearanceComponent? _))
            return;

        if (!TryComp(uid, out ItemComponent? _))
            return;

        if (!TryComp(uid, out DominatorComponent? dominator))
            return;

        // I'm not sure if this is the right implementation, so if you have any ideas then... FIX THIS PLEASE.

        var layer = new PrototypeLayerData();
        var key = $"inhand-{args.Location.ToString().ToLowerInvariant()}-{dominator.CurrentFireMode}";
        layer.State = key;
        args.Layers.Add((key, layer));
    }
}
