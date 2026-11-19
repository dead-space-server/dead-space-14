// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared.Humanoid;

public abstract partial class SharedHumanoidAppearanceSystem
{
    public void AddMarking(EntityUid uid,
        Marking marking,
        bool sync = true,
        bool forced = false,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid)
            || !_markingManager.Markings.TryGetValue(marking.MarkingId, out var prototype))
        {
            return;
        }

        var markingObject = new Marking(marking.MarkingId, marking.MarkingColors)
        {
            Forced = forced,
            Dye = prototype.ForcedColoring ? null : marking.Dye?.Sanitized(),
        };

        humanoid.MarkingSet.AddBack(prototype.MarkingCategory, markingObject);

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetMarkingDye(EntityUid uid,
        MarkingCategories category,
        int index,
        MarkingDye? dye,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (index < 0
            || !Resolve(uid, ref humanoid)
            || !humanoid.MarkingSet.TryGetCategory(category, out var markings)
            || index >= markings.Count
            || !_markingManager.TryGetMarking(markings[index], out var prototype)
            || prototype.ForcedColoring)
        {
            return;
        }

        markings[index].Dye = dye?.Sanitized();
        Dirty(uid, humanoid);
    }

    public void SetLastMarkingDye(EntityUid uid, MarkingCategories category, MarkingDye? dye)
    {
        if (dye == null
            || !TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            || !humanoid.MarkingSet.TryGetCategory(category, out var markings)
            || markings.Count == 0)
        {
            return;
        }

        SetMarkingDye(uid, category, markings.Count - 1, dye, humanoid);
    }
}
