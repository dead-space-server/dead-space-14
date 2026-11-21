// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Server.DeadSpace.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Map;

namespace Content.Server.Cloning;

public sealed partial class CloningSystem
{
    [Dependency] private SurgeryGraftSystem _graft = default!;

    private bool TryCloningHead(EntityUid head, MapCoordinates? coords, CloningSettingsPrototype settings, [NotNullWhen(true)] out EntityUid? clone)
    {
        clone = null;
        if (!TryComp<LimbFleshComponent>(head, out var flesh) || flesh.Species.Length == 0)
            return TryCloningBrain(head, coords, settings, out clone);

        if (!_prototype.TryIndex<SpeciesPrototype>(flesh.Species, out var species))
            return false;

        var attemptEv = new CloningAttemptEvent(settings);
        RaiseLocalEvent(head, ref attemptEv);
        if (attemptEv.Cancelled && !settings.ForceCloning)
            return false;

        clone = coords == null ? Spawn(species.Prototype) : Spawn(species.Prototype, coords.Value);
        if (TryComp<HumanoidAppearanceComponent>(clone, out var look))
        {
            _humanoidSystem.SetSkinColor(clone.Value, flesh.Skin, false, false, look);
            look.EyeColor = flesh.Eyes;
            foreach (var mark in flesh.Marks)
                _humanoidSystem.AddMarking(clone.Value, mark.MarkingId, mark.MarkingColors, false, true, look);
            Dirty(clone.Value, look);
        }

        if (flesh.Dna.Length > 0)
        {
            var dna = EnsureComp<DnaComponent>(clone.Value);
            dna.DNA = flesh.Dna;
            Dirty(clone.Value, dna);
        }

        if (flesh.Donor.Length > 0)
            _metaData.SetEntityName(clone.Value, flesh.Donor);

        _adminLogger.Add(LogType.Chat, LogImpact.Medium, $"The head of {flesh.Donor} was cloned as {clone.Value:player}");
        return true;
    }

    private bool TryCloningBrain(EntityUid brain, MapCoordinates? coords, CloningSettingsPrototype settings, [NotNullWhen(true)] out EntityUid? clone)
    {
        clone = null;
        if (!HasComp<BrainComponent>(brain)
            || !_graft.TryOrigin(brain, out var speciesId, out var dna, out var donor)
            || !_prototype.TryIndex<SpeciesPrototype>(speciesId, out var species))
            return false;

        var attemptEv = new CloningAttemptEvent(settings);
        RaiseLocalEvent(brain, ref attemptEv);
        if (attemptEv.Cancelled && !settings.ForceCloning)
            return false;

        clone = coords == null ? Spawn(species.Prototype) : Spawn(species.Prototype, coords.Value);
        if (dna.Length > 0)
        {
            var dnaComp = EnsureComp<DnaComponent>(clone.Value);
            dnaComp.DNA = dna;
            Dirty(clone.Value, dnaComp);
        }

        if (donor.Length > 0)
            _metaData.SetEntityName(clone.Value, donor);

        _adminLogger.Add(LogType.Chat, LogImpact.Medium, $"The brain of {donor} was cloned as {clone.Value:player}");
        return true;
    }
}
