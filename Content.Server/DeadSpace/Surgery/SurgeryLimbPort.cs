// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Server.DeadSpace.Surgery;

public sealed class SurgeryLimbPort : EntitySystem
{
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    public void Apply(EntityUid patient, LimbZone zone, LimbOrgan? organ, in SurgeryResult result, EntityUid? held)
    {
        if (result.Rejected)
            return;

        if (LimbWoundRules.IsBody(zone))
        {
            _limbs.ApplyEdit(patient, zone, new LimbEdit
            {
                Flesh = result.Flesh,
                Bone = result.Bone,
                Skin = result.Skin,
                Bleed = result.Bleed,
                Scar = result.Scar,
                Health = result.Health,
                SetHealth = result.SetHealth,
                ClearAtrophy = result.ClearAtrophy,
                ClearEmbedded = result.ClearEmbedded,
                ClearSkinBare = result.ClearSkinBare,
            });
        }

        if (organ is { } picked)
        {
            if (result.RemoveOrgan && picked == LimbOrgan.Eyes
                && TryComp<LimbBodyComponent>(patient, out var eyes) && eyes.EyesGone < 1)
            {
                _limbs.ApplyOrganEdit(patient, picked, new LimbOrganEdit
                {
                    Damage = result.OrganDamage,
                    SetRuptured = result.Rupture,
                    Ruptured = result.Rupture,
                    EyesGone = 1,
                });
            }
            else if (result.RemoveOrgan)
                Take(patient, picked, result);

            if (result.PlaceOrgan && picked == LimbOrgan.Eyes
                && TryComp<LimbBodyComponent>(patient, out var seeing) && seeing.EyesGone > 0
                && (uint) LimbOrgan.Eyes < (uint) seeing.Organs.Count && !seeing.Organs[(int) LimbOrgan.Eyes].Missing
                && held is { } spare)
            {
                _limbs.ApplyOrganEdit(patient, picked, new LimbOrganEdit { EyesGone = -1 });
                Del(spare);
            }
            else if (result.PlaceOrgan && held is { } item)
                Place(patient, picked, item, result);

            if (!result.RemoveOrgan && !result.PlaceOrgan && (result.OrganDamage > 0f || result.Rupture || result.Eyes != 0))
            {
                _limbs.ApplyOrganEdit(patient, picked, new LimbOrganEdit
                {
                    Damage = result.OrganDamage,
                    SetRuptured = result.Rupture,
                    Ruptured = true,
                    EyesGone = result.Eyes,
                });
            }
        }

        if (result.Sever)
            _limbs.SeverPart(patient, zone);

        if (result.Attach && held is { } part)
            _limbs.TryReattach(patient, zone, part, result.Crude);
    }

    private void Take(EntityUid patient, LimbOrgan organ, in SurgeryResult result)
    {
        var edit = new LimbOrganEdit
        {
            Damage = result.OrganDamage,
            SetRuptured = result.Rupture,
            Ruptured = result.Rupture,
            SetMissing = true,
            Missing = true,
            EyesGone = result.Eyes,
        };

        if (!TryOrgan(patient, organ, out var entity) || !_body.RemoveOrgan(entity))
            return;

        Stamp(entity, patient, organ, result);
        _transform.SetCoordinates(entity, Transform(patient).Coordinates.Offset(new Vector2(0.45f, 0.15f)));
        _limbs.ApplyOrganEdit(patient, organ, edit);
        if (organ is LimbOrgan.Heart or LimbOrgan.Brain)
        {
            var zone = organ == LimbOrgan.Brain ? LimbZone.Head : LimbZone.Torso;
            _limbs.ApplyEdit(patient, zone, new LimbEdit { SetHealth = true, Health = 0f });
            _mobs.ChangeMobState(patient, MobState.Dead);
        }
    }

    private void Place(EntityUid patient, LimbOrgan organ, EntityUid item, in SurgeryResult result)
    {
        if (!TryComp<OrganComponent>(item, out _))
            return;

        if (!TryComp<MetaDataComponent>(item, out var meta)
            || meta.EntityPrototype == null
            || !meta.EntityPrototype.ID.Contains(organ.ToString()))
            return;

        if (!_prototype.TryIndex<SurgeryOrganPrototype>(organ.ToString(), out var slot))
            return;

        if (!TryPart(patient, slot.Slot, out var part))
            return;

        if (!_body.InsertOrgan(part, item, slot.Slot))
            return;

        _limbs.ApplyOrganEdit(patient, organ, new LimbOrganEdit
        {
            SetMissing = true,
            Missing = false,
            SetDamage = true,
            Damage = result.Rupture ? 40f : 0f,
            SetRuptured = true,
            Ruptured = result.Rupture,
            EyesGone = organ == LimbOrgan.Eyes ? -1 : 0,
        });
    }

    private void Stamp(EntityUid organ, EntityUid patient, LimbOrgan kind, in SurgeryResult result)
    {
        var finding = EnsureComp<OrganFindingComponent>(organ);
        finding.Owner = Name(patient);
        finding.Organ = kind.ToString();
        finding.Ruptured = result.Rupture;
        if (TryComp<LimbBodyComponent>(patient, out var limbs))
        {
            var index = (int) kind;
            if ((uint) index < (uint) limbs.Organs.Count)
                finding.Damage = limbs.Organs[index].Damage + result.OrganDamage;
        }
    }

    private bool TryOrgan(EntityUid patient, LimbOrgan organ, out EntityUid entity)
    {
        entity = default;
        if (!TryComp<BodyComponent>(patient, out var body))
            return false;

        foreach (var organEnt in _body.GetBodyOrgans(patient, body))
        {
            if (!TryComp<MetaDataComponent>(organEnt.Id, out var meta) || meta.EntityPrototype == null)
                continue;

            if (!meta.EntityPrototype.ID.Contains(organ.ToString(), StringComparison.OrdinalIgnoreCase))
                continue;

            entity = organEnt.Id;
            return true;
        }

        return false;
    }

    private bool TryPart(EntityUid patient, string slot, out EntityUid part)
    {
        part = default;
        foreach (var child in _body.GetBodyChildren(patient))
        {
            var containerId = SharedBodySystem.GetOrganContainerId(slot);
            if (!_containers.TryGetContainer(child.Id, containerId, out _))
                continue;

            part = child.Id;
            return true;
        }

        return false;
    }
}
