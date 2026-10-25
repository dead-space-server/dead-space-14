// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Runtime.InteropServices;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbWoundSystem
{
    public bool ApplyEdit(EntityUid body, LimbZone zone, in LimbEdit edit)
    {
        if (!LimbWoundRules.IsBody(zone) || !TryComp<LimbBodyComponent>(body, out var limbs))
            return false;

        EnsureZones(limbs);
        var index = LimbWoundRules.Index(zone);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        if ((uint) index >= (uint) zones.Length)
            return false;

        ref var state = ref zones[index];
        if (state.Severed && !edit.ClearSevered)
            return false;

        state.Flesh = edit.SetFlesh ? MathF.Max(0f, edit.Flesh) : MathF.Max(0f, state.Flesh + edit.Flesh);
        state.Bone = edit.SetBone ? MathF.Max(0f, edit.Bone) : MathF.Max(0f, state.Bone + edit.Bone);
        state.Skin = MathF.Max(0f, state.Skin + edit.Skin);
        state.Bleed = edit.SetBleed ? MathF.Max(0f, edit.Bleed) : MathF.Max(0f, state.Bleed + edit.Bleed);
        state.Scar = Math.Max(0, state.Scar + edit.Scar);
        var cap = LimbWoundRules.HealthFor(zone);
        state.Health = edit.SetHealth
            ? Math.Clamp(edit.Health, 0f, cap)
            : Math.Clamp(state.Health + edit.Health, 0f, cap);

        if (edit.ClearAtrophy && state.Health > 0f)
            state.Atrophied = false;
        if (edit.SetEmbedded)
            state.Embedded = edit.Embedded;
        if (edit.ClearEmbedded)
            state.Embedded = false;
        if (edit.ClearSkinBare)
            state.SkinBare = false;
        if (edit.ClearSevered)
            state.Severed = false;

        if (zone == LimbZone.Torso)
        {
            var whistle = LimbWoundRules.RibBreath(state.Bone, state.Flesh) > 0f;
            limbs.LungWhistle = whistle;
            if (whistle)
                EnsureComp<LimbWhistleComponent>(body);
            else
                RemComp<LimbWhistleComponent>(body);
        }

        Dirty(body, limbs);
        ApplyLife(body, limbs);
        _movement.RefreshMovementSpeedModifiers(body);
        RefreshActive(body, limbs);
        return true;
    }

    public bool ApplyOrganEdit(EntityUid body, LimbOrgan organ, in LimbOrganEdit edit)
    {
        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return false;

        var bit = (LimbOrganMask) (1 << (int) organ);
        if ((limbs.Present & bit) == 0)
            return false;

        EnsureOrgans(limbs);
        var index = (int) organ;
        if ((uint) index >= (uint) limbs.Organs.Count)
            return false;

        var state = limbs.Organs[index];
        state.Damage = edit.SetDamage
            ? MathF.Max(0f, edit.Damage)
            : MathF.Max(0f, state.Damage + edit.Damage);
        if (edit.SetRuptured)
            state.Ruptured = edit.Ruptured;
        if (edit.SetMissing)
            state.Missing = edit.Missing;
        limbs.Organs[index] = state;

        if (organ == LimbOrgan.Eyes && edit.EyesGone != 0)
        {
            limbs.EyesGone = (byte) Math.Clamp(limbs.EyesGone + edit.EyesGone, 0, 2);
            if (TryComp<BlindableComponent>(body, out var blind))
            {
                var target = LimbWoundRules.Vision(limbs.EyesGone, limbs.EyeBurn);
                var delta = target - blind.EyeDamage;
                if (delta != 0)
                    _blind.AdjustEyeDamage((body, blind), delta);
            }
        }

        Dirty(body, limbs);
        ApplyLife(body, limbs);
        if (edit.SetMissing && !edit.Missing)
            ReleaseOrganDeath(body, limbs);
        return true;
    }

    public bool TryReattach(EntityUid body, LimbZone zone, EntityUid part, bool crude)
    {
        if (_net.IsClient || !LimbWoundRules.CanSever(zone))
            return false;

        if (!TryComp<BodyComponent>(body, out var bodyComp)
            || bodyComp.Prototype is not { } protoId
            || !_proto.TryIndex(protoId, out BodyPrototype? prototype)
            || !TryComp<BodyPartComponent>(part, out var partComp)
            || !TryComp(part, out MetaDataComponent? meta)
            || meta.EntityPrototype == null)
            return false;

        var expectedType = PartType(zone);
        var expectedSymmetry = Symmetry(zone);
        if (expectedType == null || partComp.PartType != expectedType)
            return false;

        if (expectedSymmetry != BodyPartSymmetry.None && partComp.Symmetry != expectedSymmetry)
            return false;

        if (!TryMatchSlot(prototype, meta.EntityPrototype.ID, out var slotId, out var parentSlot))
            return false;

        if (!TryLivingPart(body, bodyComp, prototype, parentSlot, out var parent))
            return false;

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
        var attached = _containers.TryGetContainer(parent, containerId, out _)
            ? _body.AttachPart(parent, slotId, part)
            : _body.TryCreatePartSlotAndAttach(parent, slotId, part, partComp.PartType);
        if (!attached)
            return false;

        if (!TryComp<LimbBodyComponent>(body, out var limbs))
            return true;

        EnsureZones(limbs);
        var index = LimbWoundRules.Index(zone);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        if ((uint) index >= (uint) zones.Length)
            return true;

        ref var state = ref zones[index];
        var fresh = CompOrNull<LimbFreshComponent>(part);
        var rotten = fresh is { Spent: true } || fresh != null && fresh.Left <= 0f;
        var fraction = fresh == null || fresh.Total <= 0f ? 0.7f : Math.Clamp(fresh.Left / fresh.Total, 0.15f, 1f);
        state.Severed = false;
        state.Bandaged = false;
        if (rotten)
        {
            state.Atrophied = true;
            state.Health = 0f;
            state.Flesh = LimbWoundRules.BreakFlesh;
            state.Bone = LimbWoundRules.BreakBone;
            state.Bleed = LimbWoundRules.StumpBleed;
        }
        else if (crude)
        {
            state.Atrophied = false;
            state.Health = LimbWoundRules.HealthFor(zone) * MathF.Max(0.35f, fraction);
            state.Flesh = LimbWoundRules.StrainFlesh;
            state.Bone = LimbWoundRules.BreakBone;
            state.Bleed = LimbWoundRules.StumpBleed * 0.5f;
        }
        else
        {
            state.Atrophied = false;
            state.Health = LimbWoundRules.HealthFor(zone) * fraction;
            state.Flesh = LimbWoundRules.StrainFlesh;
            state.Bone = 0f;
            state.Bleed = 0f;
        }

        if (LimbWoundRules.TryCarried(zone, out var child))
        {
            var childIndex = LimbWoundRules.Index(child);
            if ((uint) childIndex < (uint) zones.Length && zones[childIndex].Severed)
            {
                zones[childIndex].Severed = false;
                zones[childIndex].Health = state.Health;
                zones[childIndex].Bone = state.Bone;
                zones[childIndex].Bleed = state.Bleed;
                zones[childIndex].Atrophied = state.Atrophied;
                ShowZone(body, child);
            }
        }

        ShowZone(body, zone);
        Dirty(body, limbs);
        ApplyLife(body, limbs);
        _movement.RefreshMovementSpeedModifiers(body);
        RefreshActive(body, limbs);
        return true;
    }

    private bool TryMatchSlot(BodyPrototype prototype, string partProto, out string slotId, out string parentSlot)
    {
        slotId = string.Empty;
        parentSlot = string.Empty;
        foreach (var (id, slot) in prototype.Slots)
        {
            if (slot.Part is not { } part || part.Id != partProto)
                continue;

            slotId = id;
            foreach (var (parentId, parent) in prototype.Slots)
            {
                if (!parent.Connections.Contains(id))
                    continue;

                parentSlot = parentId;
                return true;
            }
        }

        return false;
    }

    private bool TryLivingPart(
        EntityUid body,
        BodyComponent bodyComp,
        BodyPrototype prototype,
        string slotId,
        out EntityUid part)
    {
        part = default;
        if (bodyComp.RootContainer.ContainedEntity is not { } root)
            return false;

        var have = new Dictionary<string, EntityUid> { [prototype.Root] = root };
        var frontier = new Queue<string>();
        var seen = new HashSet<string> { prototype.Root };
        frontier.Enqueue(prototype.Root);
        while (frontier.TryDequeue(out var id))
        {
            if (!prototype.Slots.TryGetValue(id, out var slot))
                continue;

            foreach (var next in slot.Connections)
            {
                if (!seen.Add(next) || !have.TryGetValue(id, out var parent))
                    continue;

                frontier.Enqueue(next);
                var containerId = SharedBodySystem.GetPartSlotContainerId(next);
                if (_containers.TryGetContainer(parent, containerId, out var container) && container.ContainedEntities.Count > 0)
                    have[next] = container.ContainedEntities[0];
            }
        }

        return have.TryGetValue(slotId, out part);
    }

    private void ShowZone(EntityUid body, LimbZone zone)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var look))
            return;

        var layers = new List<HumanoidVisualLayers>();
        LimbLookRules.Collect(zone, layers);
        foreach (var layer in layers)
            _humanoid.SetLayerVisibility((body, look), layer, true);
    }
}
