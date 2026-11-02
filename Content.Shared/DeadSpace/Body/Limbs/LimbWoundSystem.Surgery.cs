// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.DeadSpace.Surgery;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Body.Limbs;

public sealed partial class LimbWoundSystem
{
    private Vector2? _dropAt;

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
        var cap = LimbWoundRules.HealthFor(zone, limbs);
        state.Health = edit.SetHealth
            ? Math.Clamp(edit.Health, 0f, cap)
            : Math.Clamp(state.Health + edit.Health, 0f, cap);

        if (edit.ClearAtrophy && state.Health > 0f)
            state.Atrophied = false;
        if (edit.SetAtrophy)
            state.Atrophied = true;
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

    public bool Amputate(EntityUid body, LimbZone zone, Vector2? at = null)
    {
        if (_net.IsClient || !LimbWoundRules.CanSever(zone) || !TryComp<LimbBodyComponent>(body, out var limbs))
            return false;

        EnsureZones(limbs);
        var index = LimbWoundRules.Index(zone);
        if ((uint) index >= (uint) limbs.Zones.Count || limbs.Zones[index].Severed)
            return false;

        _dropAt = at;
        DropZone(body, limbs, zone);
        _dropAt = null;
        return true;
    }

    public bool TryRegrowZone(EntityUid body, LimbZone zone, float healthFraction = 0.08f)
    {
        if (_net.IsClient || !LimbWoundRules.CanSever(zone) || !TryComp<LimbBodyComponent>(body, out var limbs))
            return false;

        EnsureZones(limbs);
        var index = LimbWoundRules.Index(zone);
        var zones = CollectionsMarshal.AsSpan(limbs.Zones);
        if ((uint) index >= (uint) zones.Length || !zones[index].Severed)
            return false;

        if (!TryComp<BodyComponent>(body, out var bodyComp)
            || bodyComp.Prototype is not { } protoId
            || !_proto.TryIndex(protoId, out BodyPrototype? prototype))
            return false;

        var expectedType = PartType(zone);
        var expectedSymmetry = Symmetry(zone);
        if (expectedType == null)
            return false;

        if (!TryMatchSlot(prototype, expectedType.Value, expectedSymmetry, out var slotId, out var parentSlot))
            return false;

        if (!TryLivingPart(body, bodyComp, prototype, parentSlot, out var parent))
            return false;

        if (!prototype.Slots.TryGetValue(slotId, out var slot) || slot.Part is not { } partProto)
            return false;

        var part = Spawn(partProto, new EntityCoordinates(parent, Vector2.Zero));
        if (!TryComp<BodyPartComponent>(part, out var partComp))
        {
            QueueDel(part);
            return false;
        }

        var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
        var attached = _containers.TryGetContainer(parent, containerId, out _)
            ? _body.AttachPart(parent, slotId, part)
            : _body.TryCreatePartSlotAndAttach(parent, slotId, part, partComp.PartType);
        if (!attached)
        {
            QueueDel(part);
            return false;
        }

        ref var state = ref zones[index];
        state.Severed = false;
        state.Bandaged = false;
        state.Atrophied = false;
        state.Embedded = false;
        state.SkinBare = false;
        state.Flesh = 0f;
        state.Bone = 0f;
        state.Bleed = 0f;
        state.Scar = 0;
        state.Health = LimbWoundRules.HealthFor(zone, limbs) * Math.Clamp(healthFraction, 0.05f, 0.1f);

        if (LimbWoundRules.TryCarried(zone, out var child))
        {
            var childIndex = LimbWoundRules.Index(child);
            if ((uint) childIndex < (uint) zones.Length && zones[childIndex].Severed)
            {
                zones[childIndex].Severed = false;
                zones[childIndex].Health = state.Health;
                zones[childIndex].Flesh = 0f;
                zones[childIndex].Bone = 0f;
                zones[childIndex].Bleed = 0f;
                zones[childIndex].Atrophied = false;
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

        if (!TryMatchSlot(prototype, meta.EntityPrototype.ID, out var slotId, out var parentSlot)
            && !TryMatchSlot(prototype, expectedType.Value, expectedSymmetry, out slotId, out parentSlot))
            return false;

        if (!TryLivingPart(body, bodyComp, prototype, parentSlot, out var parent))
            return false;

        var attempt = new LimbGraftAttemptEvent(zone, part);
        RaiseLocalEvent(body, ref attempt);
        if (attempt.Cancelled)
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
            state.Health = LimbWoundRules.HealthFor(zone, limbs) * MathF.Max(0.35f, fraction);
            state.Flesh = LimbWoundRules.StrainFlesh;
            state.Bone = LimbWoundRules.BreakBone;
            state.Bleed = LimbWoundRules.StumpBleed * 0.5f;
        }
        else
        {
            state.Atrophied = false;
            state.Health = LimbWoundRules.HealthFor(zone, limbs) * fraction;
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
        ApplyGraftLook(body, zone, part);
        Dirty(body, limbs);
        var grafted = new LimbGraftedEvent(zone, part);
        RaiseLocalEvent(body, ref grafted);
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

    private bool TryMatchSlot(BodyPrototype prototype, BodyPartType type, BodyPartSymmetry symmetry, out string slotId, out string parentSlot)
    {
        slotId = string.Empty;
        parentSlot = string.Empty;
        foreach (var (id, slot) in prototype.Slots)
        {
            if (slot.Part is not { } partId
                || !_proto.TryIndex<EntityPrototype>(partId, out var partProto)
                || !partProto.TryGetComponent<BodyPartComponent>(out var bodyPart, EntityManager.ComponentFactory)
                || bodyPart.PartType != type
                || symmetry != BodyPartSymmetry.None && bodyPart.Symmetry != symmetry)
                continue;

            foreach (var (parentId, parent) in prototype.Slots)
            {
                if (!parent.Connections.Contains(id))
                    continue;

                slotId = id;
                parentSlot = parentId;
                return true;
            }
        }

        return false;
    }

    private void ApplyGraftLook(EntityUid body, LimbZone zone, EntityUid part)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var look))
            return;

        if (TryComp<LimbFleshComponent>(part, out var marked) && marked.Species.Length > 0)
            ApplyGraftMarks(body, look, zone, marked);

        if (!TryComp<LimbFleshComponent>(part, out var flesh)
            || flesh.Species.Length == 0
            || flesh.Species == look.Species.Id && flesh.Skin == look.SkinColor
            || !_proto.TryIndex<SpeciesPrototype>(flesh.Species, out var species)
            || !_proto.TryIndex<HumanoidSpeciesBaseSpritesPrototype>(species.SpriteSet, out var sprites))
        {
            ClearGraftLook(body, look, zone);
            return;
        }

        foreach (var layer in GraftLayers(zone))
        {
            if (sprites.Sprites.TryGetValue(layer, out var spriteId))
                _humanoid.SetBaseLayerId(body, layer, spriteId, false, look);
            _humanoid.SetBaseLayerColor(body, layer, flesh.Skin, false, look);
        }

        Dirty(body, look);
    }

    private void ApplyGraftMarks(EntityUid body, HumanoidAppearanceComponent look, LimbZone zone, LimbFleshComponent flesh)
    {
        var carried = LimbWoundRules.TryCarried(zone, out var child);
        var gone = new List<(MarkingCategories Category, string Id)>();
        foreach (var (category, list) in look.MarkingSet.Markings)
        {
            foreach (var marking in list)
            {
                if (!_proto.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto))
                    continue;

                if (LimbLookRules.Fits(zone, proto.BodyPart, true) || carried && LimbLookRules.Fits(child, proto.BodyPart, true))
                    gone.Add((category, marking.MarkingId));
            }
        }

        foreach (var (category, id) in gone)
            look.MarkingSet.Remove(category, id);

        foreach (var mark in flesh.Marks)
        {
            if (_proto.TryIndex<MarkingPrototype>(mark.MarkingId, out var proto))
                look.MarkingSet.AddBack(proto.MarkingCategory, new Marking(mark));
        }

        Dirty(body, look);
    }

    private void ClearGraftLook(EntityUid body, HumanoidAppearanceComponent look, LimbZone zone)
    {
        var changed = false;
        foreach (var layer in GraftLayers(zone))
            changed |= look.CustomBaseLayers.Remove(layer);

        if (changed)
            Dirty(body, look);
    }

    private static List<HumanoidVisualLayers> GraftLayers(LimbZone zone)
    {
        var layers = new List<HumanoidVisualLayers>();
        LimbLookRules.Collect(zone, layers);
        if (LimbWoundRules.TryCarried(zone, out var child))
            LimbLookRules.Collect(child, layers);
        return layers;
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

    private bool Machine(EntityUid body)
    {
        return TryComp<HumanoidAppearanceComponent>(body, out var look)
               && SurgerySpeciesPrototype.IsMachine(_proto, look.Species.Id);
    }
}
