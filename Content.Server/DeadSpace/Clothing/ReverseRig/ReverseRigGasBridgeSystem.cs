// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Clothing.ReverseRig;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Clothing.ReverseRig;

/// <summary>
///     Server-side gas bridge for the Reverse RIG backpack. The backpack exposes its own GasTank buffer that
///     is used both for breathing (internals) and the jetpack. Every tick this system tops the buffer up from
///     the oxygen tank inserted in the backpack's item slot, making that tank the actual gas source.
/// </summary>
public sealed class ReverseRigGasBridgeSystem : EntitySystem
{
    public const string TankSlotId = "sor-tank";

    /// <summary>
    ///     Working gas reserve held in the backpack's buffer. The buffer is always topped up to this level
    ///     from the inserted tank, so the tank stays the actual reservoir and drains as gas is consumed.
    /// </summary>
    private const float TargetBufferMoles = 0.5f;

    private const float Epsilon = 0.0001f;

    /// <summary>
    ///     Tolerance for the per-gas composition comparison between the buffer and the inserted tank.
    /// </summary>
    private const float CompositionTolerance = 0.05f;

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReverseRigBackpackComponent, EntRemovedFromContainerMessage>(OnTankRemoved);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ReverseRigBackpackComponent, GasTankComponent>();
        while (query.MoveNext(out var uid, out var component, out var gasTankComp))
        {
            var buffer = gasTankComp.Air;
            if (buffer == null)
                continue;

            if (buffer.TotalMoles <= Epsilon)
                component.BufferSourceUid = null;

            if (!_itemSlots.TryGetSlot(uid, TankSlotId, out var slot) || slot.Item is not { } tankUid)
                continue;

            if (!TryComp<GasTankComponent>(tankUid, out var tankComp) || tankComp.Air == null)
                continue;

            var tankAir = tankComp.Air;

            if (buffer.TotalMoles > Epsilon && component.BufferSourceUid != tankUid)
            {
                if (component.BufferSourceUid is { } oldSource && TryReturnBuffer(oldSource, buffer))
                {
                    component.BufferSourceUid = null;
                }
                else if (tankAir.TotalMoles <= Epsilon || !CompositionMatches(buffer, tankAir))
                {
                    // The previous source no longer exists and the replacement contains a different gas.
                    // Preserve the old reserve until it is consumed instead of mixing or deleting it.
                    continue;
                }
            }

            // An empty current source is not a swap: its reserve remains available in the buffer.
            if (tankAir.TotalMoles <= Epsilon)
                continue;

            // Do not mix a source that changed composition while it was inserted. The old reserve is consumed first.
            if (!CompositionMatches(buffer, tankAir))
                continue;

            component.BufferSourceUid = tankUid;

            // The tank is the gas source: keep the buffer topped up to the working reserve and no more.
            var toAdd = TargetBufferMoles - buffer.TotalMoles;
            if (toAdd <= Epsilon)
                continue;

            var toTransfer = Math.Min(toAdd, tankAir.TotalMoles);
            if (toTransfer <= Epsilon)
                continue;

            _atmos.Merge(buffer, tankAir.Remove(toTransfer));
        }
    }

    private void OnTankRemoved(Entity<ReverseRigBackpackComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != TankSlotId || ent.Comp.BufferSourceUid != args.Entity)
            return;

        if (TryComp<GasTankComponent>(ent.Owner, out var bufferTank) &&
            bufferTank.Air != null &&
            !TerminatingOrDeleted(args.Entity))
        {
            TryReturnBuffer(args.Entity, bufferTank.Air);
        }

        // If the old source was destroyed or lost its GasTank component, keep the reserve in the backpack.
        // A compatible replacement may adopt it, otherwise it remains available until consumed.
        ent.Comp.BufferSourceUid = null;
    }

    private bool TryReturnBuffer(EntityUid source, GasMixture buffer)
    {
        if (TerminatingOrDeleted(source) ||
            !TryComp<GasTankComponent>(source, out var sourceTank) ||
            sourceTank.Air == null)
        {
            return false;
        }

        if (buffer.TotalMoles > Epsilon)
            _atmos.Merge(sourceTank.Air, buffer.Remove(buffer.TotalMoles));

        return true;
    }

    private static bool CompositionMatches(GasMixture buffer, GasMixture tank)
    {
        // An empty buffer can not mismatch whatever is inserted.
        if (buffer.TotalMoles <= Epsilon)
            return true;

        if (tank.TotalMoles <= Epsilon)
            return false;

        var bufferTotal = buffer.TotalMoles;
        var tankTotal = tank.TotalMoles;

        for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
        {
            var expected = bufferTotal * (tank[i] / tankTotal);
            if (MathF.Abs(buffer[i] - expected) > CompositionTolerance)
                return false;
        }

        return true;
    }
}
