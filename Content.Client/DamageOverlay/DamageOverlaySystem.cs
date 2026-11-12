using Content.Shared.DamageOverlay;
using Content.Shared.DeadSpace.Psychiatry; // DS14
using Content.Shared.DeadSpace.TheCircle.Dreadnought; // DS14
using Content.Shared.Mobs; // DS14
using Content.Shared.Mobs.Systems; // DS14
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client.DamageOverlay;

/// <inheritdoc />
public sealed partial class DamageOverlaySystem : SharedDamageOverlaySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!; // DS14

    private DamageOverlay _overlay = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        _overlay = new DamageOverlay();
    }

    [SubscribeLocalEvent]
    private void OnComponentShutdown(Entity<DamageOverlayComponent> entity, ref ComponentShutdown args)
    {
        if (_player.LocalEntity != entity)
            return;

        _overlayManager.RemoveOverlay(_overlay);
    }

    [SubscribeLocalEvent]
    private void OnPlayerAttached(Entity<DamageOverlayComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayManager.AddOverlay(_overlay);
    }

    [SubscribeLocalEvent]
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    // DS14-start
    /// <summary>
    /// The DS14 overlay keeps its own target values, so they are synced from the networked component every frame.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local)
            return;

        SyncOverlay(local);
    }

    /// <summary>
    /// Re-applies the local overlay values, e.g. after a client-side psychiatry phantom changed.
    /// </summary>
    public void RefreshLocal(EntityUid entity)
    {
        if (_player.LocalEntity != entity)
            return;

        SyncOverlay(entity);
    }

    private void SyncOverlay(EntityUid entity)
    {
        if (!TryComp<DamageOverlayComponent>(entity, out var comp) ||
            HasComp<DreadnoughtLastStandActiveComponent>(entity))
        {
            if (_overlay.State != MobState.Alive || _overlay.PainLevel != 0f || _overlay.OxygenLevel != 0f ||
                _overlay.PreCriticalLevel != 0f || _overlay.CritLevel != 0f)
            {
                _overlay.Reset();
            }

            return;
        }

        _overlay.State = comp.CurrentState;
        _overlay.PainLevel = comp.PainLevel;
        _overlay.OxygenLevel = comp.OxygenLevel;
        _overlay.PreCriticalLevel = comp.PreCriticalLevel;
        _overlay.CritLevel = comp.CritLevel;

        if (comp.CurrentState is not (MobState.Alive or MobState.PreCritical))
            return;

        // Psychiatry phantom pain/asphyxiation is only shown to the patient.
        if (!TryComp<SchizophreniaComponent>(entity, out var schizo) ||
            schizo.PhantomAmount <= 0f ||
            schizo.Stage < SchizophreniaStage.Simple ||
            !_mobThreshold.TryGetIncapThreshold(entity, out var critThreshold))
        {
            return;
        }

        var extra = Math.Clamp(schizo.PhantomAmount / critThreshold.Value.Float(), 0f, 0.4f);
        if (schizo.PhantomType == "Asphyxiation")
        {
            if (_overlay.OxygenLevel < 0.85f)
                _overlay.OxygenLevel = Math.Min(0.85f, _overlay.OxygenLevel + extra);
        }
        else if (_overlay.PainLevel < 0.85f)
        {
            _overlay.PainLevel = Math.Min(0.85f, _overlay.PainLevel + extra);
        }
    }
    // DS14-end

    protected override void EnsureOverlay(Entity<DamageOverlayComponent> entity)
    {
        base.EnsureOverlay(entity);

        if (_player.LocalEntity != entity)
            return;

        if (!_overlayManager.HasOverlay<DamageOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }
}
