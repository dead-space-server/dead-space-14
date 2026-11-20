// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid.Markings;

namespace Content.Shared.Humanoid;

public abstract partial class SharedHumanoidAppearanceSystem
{
    public void SetMarkingDye(EntityUid uid,
        MarkingCategories category,
        int index,
        MarkingDye? dye,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (!humanoid.MarkingSet.TryGetCategory(category, out var markings)
            || index < 0
            || index >= markings.Count)
        {
            return;
        }

        if (!_markingManager.TryGetMarking(markings[index], out var prototype) || prototype.ForcedColoring)
            return;

        markings[index].Dye = dye?.Cleaned();
        Dirty(uid, humanoid);
    }

    public void SetLastMarkingDye(EntityUid uid, MarkingCategories category, MarkingDye? dye)
    {
        if (dye == null || !TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
            return;

        if (!humanoid.MarkingSet.TryGetCategory(category, out var markings) || markings.Count == 0)
            return;

        SetMarkingDye(uid, category, markings.Count - 1, dye, humanoid);
    }
}
