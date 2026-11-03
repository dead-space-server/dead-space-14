// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Surgery;

public static class SurgeryProcedure
{
    public const float CutDone = 0.95f;

    public static int Current(IReadOnlyList<SurgeryStep> steps, IReadOnlyList<SurgeryLayerState> layers)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var index = Index(layers, steps[i].Layer);
            if (index < 0 || Done(steps[i].Verb, layers[index]))
                continue;

            return i;
        }

        return -1;
    }

    public static int CatchUp(IReadOnlyList<SurgeryStep> steps, IReadOnlyList<SurgeryLayerState> layers, int step)
    {
        if (step < 0)
            step = 0;

        while (step < steps.Count)
        {
            var index = Index(layers, steps[step].Layer);
            if (index < 0 || !Done(steps[step].Verb, layers[index]))
                break;

            step++;
        }

        return step;
    }

    public static bool Advances(SurgeryVerbKind verb, in SurgeryLayerState layer, in SurgeryResult result)
    {
        if (verb == SurgeryVerbKind.Extract)
            return result.RemoveOrgan || result.RemoveImplant;

        if (verb == SurgeryVerbKind.Amputate)
            return result.Sever;

        if (verb == SurgeryVerbKind.SetBone)
            return result.Finished;

        if (verb == SurgeryVerbKind.PullEmbedded)
            return result.ClearEmbedded;

        if (verb == SurgeryVerbKind.Seat)
            return result.Attach;

        if (verb == SurgeryVerbKind.Insert)
            return result.PlaceOrgan || result.PlaceImplant;

        if (verb is SurgeryVerbKind.Graft or SurgeryVerbKind.MendBone or SurgeryVerbKind.Stanch or SurgeryVerbKind.Bandage or SurgeryVerbKind.Inject or SurgeryVerbKind.Append or SurgeryVerbKind.Shock or SurgeryVerbKind.SealSap)
            return result.Finished;

        return Done(verb, layer);
    }

    public static bool Done(SurgeryVerbKind verb, in SurgeryLayerState layer)
    {
        return verb switch
        {
            SurgeryVerbKind.Incise or SurgeryVerbKind.Chop => layer.Integrity <= 0.2f,
            SurgeryVerbKind.SawBone or SurgeryVerbKind.Drill => layer.Integrity <= CutDone,
            SurgeryVerbKind.Retract => layer.Retracted,
            SurgeryVerbKind.Extract => false,
            SurgeryVerbKind.Cauterize or SurgeryVerbKind.SealSap => layer.Integrity >= 0.85f && !layer.Retracted,
            _ => false,
        };
    }

    public static int Index(IReadOnlyList<SurgeryLayerState> layers, SurgeryLayerKind kind)
    {
        for (var i = 0; i < layers.Count; i++)
        {
            if (layers[i].Kind == kind)
                return i;
        }

        return -1;
    }

    public static void Conceal(List<SurgeryLayerState> layers, int index)
    {
        if (index < 0 || index + 1 >= layers.Count)
            return;

        var next = layers[index + 1];
        if (next.Kind is not (SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe))
            return;

        next.Integrity = 1f;
        next.Retracted = false;
        layers[index + 1] = next;
    }

    public static void Reveal(List<SurgeryLayerState> layers)
    {
        for (var i = 0; i < layers.Count - 1; i++)
        {
            if (!layers[i].Retracted)
                continue;

            var next = layers[i + 1];
            if (next.Kind is not (SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe))
                continue;

            next.Integrity = 0f;
            next.Retracted = true;
            layers[i + 1] = next;
        }
    }

    public static string Wound(string part, ReadOnlySpan<byte> kinds, ReadOnlySpan<float> integrity, ReadOnlySpan<byte> retracted, float amount, bool ribs = false)
    {
        if (Sealed(kinds, integrity, retracted, SurgeryLayerKind.Skin))
            return "none";

        if (Shows(kinds, integrity, retracted, SurgeryLayerKind.Cavity) && GateOpen(kinds, retracted, SurgeryLayerKind.Cavity)
            || Shows(kinds, integrity, retracted, SurgeryLayerKind.Globe) && GateOpen(kinds, retracted, SurgeryLayerKind.Globe))
            return ribs ? part + "_organs" : part + "_open";

        if (Shows(kinds, integrity, retracted, SurgeryLayerKind.Bone))
            return ribs ? part + "_ribs" : part + "_open";

        if (Held(kinds, retracted, SurgeryLayerKind.Skin))
            return part + "_open";

        return CutMark(part, amount);
    }

    public static SurgeryGameKind Game(SurgeryVerbKind verb)
    {
        return verb switch
        {
            SurgeryVerbKind.Retract => SurgeryGameKind.Spread,
            SurgeryVerbKind.SawBone => SurgeryGameKind.Saw,
            SurgeryVerbKind.Chop => SurgeryGameKind.Chop,
            SurgeryVerbKind.Drill => SurgeryGameKind.Drill,
            SurgeryVerbKind.Clamp => SurgeryGameKind.Clamp,
            SurgeryVerbKind.Extract => SurgeryGameKind.Sever,
            SurgeryVerbKind.Graft => SurgeryGameKind.Hold,
            SurgeryVerbKind.Cauterize => SurgeryGameKind.Sear,
            SurgeryVerbKind.SealSap => SurgeryGameKind.Hold,
            SurgeryVerbKind.MendBone => SurgeryGameKind.Glue,
            SurgeryVerbKind.Stanch => SurgeryGameKind.Stanch,
            SurgeryVerbKind.Bandage => SurgeryGameKind.Hold,
            SurgeryVerbKind.Inject => SurgeryGameKind.Inject,
            SurgeryVerbKind.Append => SurgeryGameKind.Hold,
            SurgeryVerbKind.Shock => SurgeryGameKind.Tap,
            _ => SurgeryGameKind.Cut,
        };
    }

    private static string CutMark(string part, float amount)
    {
        if (amount > 0.8f)
            return part + "_cut4";
        if (amount > 0.55f)
            return part + "_cut3";
        if (amount > 0.3f)
            return part + "_cut2";
        if (amount > 0.05f)
            return part + "_cut1";
        return "none";
    }

    private static bool Sealed(ReadOnlySpan<byte> kinds, ReadOnlySpan<float> integrity, ReadOnlySpan<byte> retracted, SurgeryLayerKind kind)
    {
        var count = Math.Min(kinds.Length, Math.Min(integrity.Length, retracted.Length));
        for (var i = 0; i < count; i++)
        {
            if ((SurgeryLayerKind) kinds[i] != kind)
                continue;

            return retracted[i] == 0 && integrity[i] > CutDone;
        }

        return false;
    }

    private static bool GateOpen(ReadOnlySpan<byte> kinds, ReadOnlySpan<byte> retracted, SurgeryLayerKind kind)
    {
        var count = Math.Min(kinds.Length, retracted.Length);
        for (var i = 0; i < count; i++)
        {
            if ((SurgeryLayerKind) kinds[i] != kind)
                continue;

            return i == 0 || retracted[i - 1] != 0;
        }

        return false;
    }

    private static bool Shows(ReadOnlySpan<byte> kinds, ReadOnlySpan<float> integrity, ReadOnlySpan<byte> retracted, SurgeryLayerKind kind)
    {
        return Held(kinds, retracted, kind) || CutThrough(kinds, integrity, kind);
    }

    private static bool Held(ReadOnlySpan<byte> kinds, ReadOnlySpan<byte> retracted, SurgeryLayerKind kind)
    {
        var count = Math.Min(kinds.Length, retracted.Length);
        for (var i = 0; i < count; i++)
        {
            if ((SurgeryLayerKind) kinds[i] == kind && retracted[i] != 0)
                return true;
        }

        return false;
    }

    private static bool CutThrough(ReadOnlySpan<byte> kinds, ReadOnlySpan<float> integrity, SurgeryLayerKind kind)
    {
        var count = Math.Min(kinds.Length, integrity.Length);
        for (var i = 0; i < count; i++)
        {
            if ((SurgeryLayerKind) kinds[i] == kind && integrity[i] < 0.2f)
                return true;
        }

        return false;
    }
}
