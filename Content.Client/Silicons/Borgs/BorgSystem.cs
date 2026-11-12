<<<<<<< ours
using Content.Shared.Alert;
using Content.Shared.DeadSpace.SecurityBorg;
||||||| base
﻿using Content.Shared.Alert;
=======
using Content.Shared.Alert;
>>>>>>> theirs
using Content.Shared.Mobs;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Client.Silicons.Borgs;

/// <inheritdoc/>
public sealed partial class BorgSystem : SharedBorgSystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private EntityQuery<BorgChassisComponent> _chassisQuery = default!;
    [Dependency] private EntityQuery<PowerCellSlotComponent> _slotQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeBattery();

        SubscribeLocalEvent<BorgChassisComponent, AppearanceChangeEvent>(OnBorgAppearanceChanged);
        SubscribeLocalEvent<MMIComponent, AppearanceChangeEvent>(OnMMIAppearanceChanged);
    }

    public override void UpdateUI(Entity<BorgChassisComponent?> chassis)
    {
        if (_ui.TryGetOpenUi(chassis.Owner, BorgUiKey.Key, out var bui))
            bui.Update();
    }

    private void OnBorgAppearanceChanged(Entity<BorgChassisComponent> chassis, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateBorgAppearance((chassis.Owner, chassis.Comp, args.Component, args.Sprite));
    }

    protected override void OnInserted(Entity<BorgChassisComponent> chassis, ref EntInsertedIntoContainerMessage args)
    {
        if (!chassis.Comp.Initialized)
            return;

        base.OnInserted(chassis, ref args);
        UpdateUI(chassis.AsNullable());
        UpdateBorgAppearance((chassis, chassis.Comp));
        UpdateBatteryAlert((chassis.Owner, chassis.Comp, null));
    }

    protected override void OnRemoved(Entity<BorgChassisComponent> chassis, ref EntRemovedFromContainerMessage args)
    {
        if (!chassis.Comp.Initialized)
            return;

        base.OnRemoved(chassis, ref args);
        UpdateUI(chassis.AsNullable());
        UpdateBorgAppearance((chassis, chassis.Comp));
        UpdateBatteryAlert((chassis.Owner, chassis.Comp, null));
    }

    private void UpdateBorgAppearance(Entity<BorgChassisComponent?, AppearanceComponent?, SpriteComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, ref ent.Comp3))
            return;

        if (_appearance.TryGetData<MobState>(ent.Owner, MobStateVisuals.State, out var state, ent.Comp2))
        {
            if (state != MobState.Alive)
            {
                _sprite.LayerSetVisible((ent.Owner, ent.Comp3), BorgVisualLayers.Light, false);
                return;
            }
        }

        if (!_appearance.TryGetData<bool>(ent.Owner, BorgVisuals.HasPlayer, out var hasPlayer, ent.Comp2))
            hasPlayer = false;

        _sprite.LayerSetVisible((ent.Owner, ent.Comp3), BorgVisualLayers.Light, ent.Comp1.BrainEntity != null || hasPlayer);
        // DS14-start
        var mindState = hasPlayer ? ent.Comp1.HasMindState : ent.Comp1.NoMindState;
        if (_appearance.TryGetData<bool>(ent.Owner, SecurityBorgProneVisuals.Prone, out var securityBorgProne, ent.Comp2) && securityBorgProne)
            mindState = hasPlayer ? "sec_down_e" : "sec_down_e_r";
        _sprite.LayerSetRsiState((ent.Owner, ent.Comp3), BorgVisualLayers.Light, mindState);
        // DS14-end
    }

    private void OnMMIAppearanceChanged(Entity<MMIComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.TryGetData(MMIVisuals.BrainPresent, out bool brain))
            brain = false;
        if (!args.TryGetData(MMIVisuals.HasMind, out bool hasMind))
            hasMind = false;

<<<<<<< ours
        _sprite.LayerSetVisible((uid, sprite), MMIVisualLayers.Brain, brain);
        // DS14-start
        if (brain)
            _sprite.LayerSetRsiState((uid, sprite), MMIVisualLayers.Brain, SpeciesBrainState(component, sprite));
        // DS14-end
||||||| base
        _sprite.LayerSetVisible((uid, sprite), MMIVisualLayers.Brain, brain);
=======
        var lightColor = Color.White;
        var lightVisible = false;
        var spriteEnt = (ent.Owner, args.Sprite);

        _sprite.LayerSetVisible(spriteEnt, MMIVisualLayers.Brain, brain);
>>>>>>> theirs
        if (!brain)
        {
            _sprite.LayerSetRsiState(spriteEnt, MMIVisualLayers.Base, ent.Comp.NoBrainState);
        }
        else
        {
            var state = hasMind
                ? ent.Comp.HasMindState
                : ent.Comp.NoMindState;
            _sprite.LayerSetRsiState(spriteEnt, MMIVisualLayers.Base, state);

            lightColor = hasMind
                ? ent.Comp.HasMindLightColor
                : ent.Comp.NoMindLightColor;
            lightVisible = true;
        }

        // Update color if it exists.
        if (_sprite.LayerMapTryGet(spriteEnt, MMIVisualLayers.Unlit, out var layerIndex, logMissing: false))
        {
            _sprite.LayerSetVisible(spriteEnt, layerIndex, lightVisible);
            _sprite.LayerSetColor(spriteEnt, layerIndex, lightColor);
        }
    }

    // DS14-start
    /// <summary>A vox or diona brain in the jar looks like its own, scaled to the jar.</summary>
    private string SpeciesBrainState(MMIComponent mmi, SpriteComponent sprite)
    {
        const string fallback = "mmi_brain";
        if (mmi.BrainSlot.Item is not { } brain
            || !TryComp<SpriteComponent>(brain, out var brainSprite)
            || brainSprite.BaseRSI?.Path.ToString() is not { } path)
            return fallback;

        const string marker = "/Species/";
        var at = path.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
            return fallback;

        var species = path[(at + marker.Length)..].Split('/')[0].ToLowerInvariant();
        var state = fallback + "_" + species;
        return sprite.BaseRSI != null && sprite.BaseRSI.TryGetState(state, out _) ? state : fallback;
    }
    // DS14-end

    /// <summary>
    /// Sets the sprite states used for the borg "is there a mind or not" indication.
    /// </summary>
    /// <param name="borg">The entity and component to modify.</param>
    /// <param name="hasMindState">The state to use if the borg has a mind.</param>
    /// <param name="noMindState">The state to use if the borg has no mind.</param>
    /// <seealso cref="BorgChassisComponent.HasMindState"/>
    /// <seealso cref="BorgChassisComponent.NoMindState"/>
    public void SetMindStates(Entity<BorgChassisComponent> borg, string hasMindState, string noMindState)
    {
        borg.Comp.HasMindState = hasMindState;
        borg.Comp.NoMindState = noMindState;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateBattery(frameTime);
    }
}
