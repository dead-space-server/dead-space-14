// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;

namespace Content.Shared.DeadSpace.Surgery;

public static class SurgeryCut
{
    public static SurgeryResult Resolve(in SurgeryRequest request)
    {
        var result = new SurgeryResult
        {
            Integrity = request.Integrity,
            Retracted = request.Retracted,
            Key = string.Empty,
        };

        var progress = Math.Clamp(request.Progress, 0f, 1f);
        var slips = Math.Clamp(request.Slips, 0, 12);
        var messy = 1f + request.SurfaceSlip + request.ToolSlip;

        switch (request.Verb)
        {
            case SurgeryVerbKind.Incise:
            case SurgeryVerbKind.Chop:
                Incise(request, progress, slips, messy, ref result);
                if (request.Verb == SurgeryVerbKind.Chop)
                {
                    result.Bleed = 0f;
                    if (slips > 0)
                        result.Health = StrayHealth(request, slips) + slips * 2f;
                    result.Key = slips > 0 ? "surgery-chop-miss" : "surgery-chop";
                }
                break;
            case SurgeryVerbKind.Retract:
                Retract(request, progress, ref result);
                break;
            case SurgeryVerbKind.Clamp:
                result.Bleed = -MathF.Min(request.CurrentBleed, 3f + progress * 7f);
                result.Key = "surgery-clamped";
                break;
            case SurgeryVerbKind.SawBone:
            case SurgeryVerbKind.Drill:
                Saw(request, progress, slips, messy, ref result);
                break;
            case SurgeryVerbKind.Amputate:
                Amputate(request, progress, slips, messy, ref result);
                break;
            case SurgeryVerbKind.Cauterize:
            case SurgeryVerbKind.SealSap:
                Cauterize(request, progress, slips, messy, ref result);
                if (request.Verb == SurgeryVerbKind.SealSap)
                {
                    result.Bleed = 0f;
                    result.Finished = progress >= 0.85f || result.Integrity >= 0.85f;
                    result.Key = result.Finished ? "surgery-sap-sealed" : "surgery-sap-short";
                }
                break;
            case SurgeryVerbKind.SetBone:
                SetBone(request, progress, slips, ref result);
                break;
            case SurgeryVerbKind.MendBone:
                MendBone(request, progress, ref result);
                break;
            case SurgeryVerbKind.Stanch:
                result.Bleed = -request.CurrentBleed * progress * (request.BoneFloor > 0f ? 0.7f : 1f);
                result.Germs = request.BoneFloor > 0f ? 4f : 0f;
                result.Finished = progress > 0.85f;
                result.Key = result.Finished ? "surgery-stanched" : "surgery-searing";
                break;
            case SurgeryVerbKind.Bandage:
                result.Finished = progress > 0.85f;
                result.Bleed = result.Finished ? -request.CurrentBleed : -request.CurrentBleed * progress * 0.5f;
                result.Key = result.Finished ? "surgery-bandaged" : "surgery-bandage-short";
                break;
            case SurgeryVerbKind.Inject:
                result.Finished = progress > 0.85f;
                result.Key = result.Finished ? "surgery-injected" : "surgery-inject-short";
                break;
            case SurgeryVerbKind.Append:
                result.Finished = progress > 0.85f;
                result.Key = result.Finished ? "surgery-appended" : "surgery-seat-short";
                break;
            case SurgeryVerbKind.Shock:
                result.Finished = progress > 0.5f;
                result.Key = result.Finished ? "surgery-shocked" : string.Empty;
                break;
            case SurgeryVerbKind.Extract:
                Extract(request, progress, slips, messy, ref result);
                break;
            case SurgeryVerbKind.Insert:
                Insert(request, progress, slips, messy, ref result);
                break;
            case SurgeryVerbKind.PullEmbedded:
                if (!request.Embedded)
                {
                    result.Rejected = true;
                    result.Key = "surgery-no-shard";
                    break;
                }

                if (progress > 0.75f)
                {
                    result.ClearEmbedded = true;
                    result.Flesh = slips * 2f;
                    result.Bleed = (1f + slips) * messy;
                    result.Key = slips > 2 ? "surgery-shard-torn" : "surgery-shard-out";
                }

                break;
            case SurgeryVerbKind.Inspect:
                result.Examined = progress > 0.9f;
                result.Key = result.Examined ? "surgery-examined" : "surgery-examine-short";
                break;
            case SurgeryVerbKind.Graft:
                if (progress <= 0.8f)
                {
                    result.Key = "surgery-graft-short";
                    break;
                }

                result.ClearSkinBare = true;
                result.Integrity = 0.4f;
                result.Retracted = false;
                result.Finished = true;
                result.Key = "surgery-grafted";
                break;
            case SurgeryVerbKind.Seat:
                result.Attach = progress > 0.75f;
                result.Crude = request.BoneFloor > 0f || slips > 2;
                result.Bleed = slips * messy;
                result.Key = result.Attach
                    ? result.Crude ? "surgery-seated-crude" : "surgery-seated"
                    : "surgery-seat-short";
                break;
        }

        return result;
    }

    private static void Incise(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        if (request.Kind is SurgeryLayerKind.Bone)
        {
            result.Bone = 3f + slips * 2f;
            result.Bleed = 2f * messy;
            result.Health = StrayHealth(request, Math.Max(1, slips));
            result.Key = "surgery-hit-bone";
            return;
        }

        if (request.Kind is SurgeryLayerKind.Cavity)
        {
            result.Rejected = true;
            result.Key = "surgery-cavity-open";
            return;
        }

        var power = 0.35f + request.ToolPrecision * 0.7f;
        result.Integrity = progress >= 0.95f
            ? 0f
            : MathF.Max(0f, request.Integrity - progress * power);
        result.Bleed = slips * 0.8f * messy;
        result.Flesh = request.Kind is SurgeryLayerKind.Globe ? slips * 1.6f : slips * 0.6f;
        result.Health = StrayHealth(request, slips);
        if (slips > 0)
            result.NextLoss = 0.12f + slips * (1f - request.ToolPrecision) * 0.1f;

        if (request.Kind is SurgeryLayerKind.Globe)
        {
            result.OrganDamage = 6f * progress + slips * 4f;
            result.Eyes = slips > 1 || progress > 0.5f ? 1 : 0;
            result.Rupture = slips > 4;
            result.Key = "surgery-eye-cut";
        }

        if (request.SkinBare && request.Kind is SurgeryLayerKind.Skin && progress > 0.9f && slips < 4)
        {
            result.Skin = -request.CurrentSkin;
            result.ClearSkinBare = true;
            result.Key = "surgery-debrided";
        }

        if (request.Atrophied && progress > 0.9f && slips < 4 && request.Kind is SurgeryLayerKind.Skin)
        {
            result.ClearAtrophy = true;
            result.SetHealth = true;
            result.Health = Pool(request) * (request.BoneFloor > 0f ? 0.3f : 0.6f);
            result.Flesh = LimbWoundRules.StrainFlesh - request.CurrentFlesh;
            result.Scar = 1;
            result.Key = "surgery-debrided";
        }

        if (request.Scar > 0
            && request.Kind is SurgeryLayerKind.Skin
            && progress > 0.95f
            && slips == 0
            && request.ToolPrecision >= 0.9f
            && request.BoneFloor <= 0f)
        {
            result.Scar = -1;
            result.Key = "surgery-scar";
        }

        if (result.Key.Length == 0)
            result.Key = result.Integrity <= 0.05f ? "surgery-layer-open" : "surgery-layer-cut";
    }

    private static void Retract(in SurgeryRequest request, float progress, ref SurgeryResult result)
    {
        if (request.Integrity > 0.92f && !request.Intact)
        {
            result.Rejected = true;
            result.Key = "surgery-not-open";
            return;
        }

        if (progress > 0.8f)
        {
            result.Retracted = true;
            result.Key = "surgery-retracted";
            return;
        }

        result.Key = "surgery-retract-short";
    }

    private static void Saw(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        if (request.Kind is SurgeryLayerKind.Bone)
        {
            var power = 0.4f + request.ToolPrecision * 0.65f;
            result.Integrity = MathF.Max(0f, request.Integrity - progress * power);
            result.Bleed = slips * 0.7f * messy;
            result.Health = StrayHealth(request, slips);
            result.Key = result.Integrity <= 0.05f ? "surgery-bone-open" : "surgery-bone-cut";
            return;
        }

        result.Integrity = request.Integrity;
        result.Flesh = 6f * progress + slips * 2f;
        result.Bleed = 4f * messy;
        result.Bone = 1.5f;
        result.Health = StrayHealth(request, Math.Max(1, slips));
        result.Key = "surgery-saw-flesh";
    }

    private static void Amputate(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        if (!LimbWoundRules.CanSever(request.Zone))
        {
            result.Bone = 5f;
            result.Bleed = 3f * messy;
            result.Flesh = 4f;
            result.Key = "surgery-no-sever";
            return;
        }

        if (progress <= 0.8f)
        {
            result.Bleed = progress * 4f * messy;
            result.Flesh = slips;
            result.Health = StrayHealth(request, slips);
            result.Key = "surgery-amp-short";
            return;
        }

        result.Sever = true;
        result.Bleed = (request.LayersOpen ? 4f : 10f) * messy + slips;
        result.Key = request.LayersOpen ? "surgery-amputated" : "surgery-amputated-raw";
    }

    private static void MendBone(in SurgeryRequest request, float progress, ref SurgeryResult result)
    {
        if (request.Kind is SurgeryLayerKind.Skin)
        {
            result.Flesh = -MathF.Max(0f, request.CurrentFlesh) * progress;
            result.Gel = progress > 0.7f;
            result.Finished = progress > 0.85f;
            if (result.Finished)
            {
                result.Integrity = 1f;
                result.Retracted = false;
            }

            result.Key = result.Finished ? "surgery-slime-gel" : "surgery-slime-gel-short";
            return;
        }

        if (request.Kind is not (SurgeryLayerKind.Bone or SurgeryLayerKind.Cavity))
        {
            result.Rejected = true;
            result.Key = "surgery-bone-shut";
            return;
        }

        var span = MathF.Max(0f, request.CurrentBone - request.BoneFloor);
        result.Bone = -span * progress;
        result.Gel = progress > 0.7f;
        result.Finished = progress > 0.85f;
        if (result.Finished && request.Kind is SurgeryLayerKind.Bone)
        {
            result.Integrity = 1f;
            result.Retracted = false;
        }

        result.Key = result.Finished ? "surgery-gel" : "surgery-gel-short";
    }

    private static void Cauterize(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        var seal = progress >= 0.95f ? 1f : progress * (0.4f + request.ToolPrecision * 0.6f);
        result.Integrity = MathF.Min(1f, request.Integrity + seal);
        if (result.Integrity > 0.2f)
            result.Retracted = false;

        if (request.DeeperCut)
        {
            result.Bleed = 2.5f * messy;
            result.Germs = 6f * messy;
            result.Health = StrayHealth(request, slips);
            result.Key = "surgery-pocket";
            return;
        }

        result.Bleed = progress >= 0.95f && slips == 0 ? -request.CurrentBleed : -request.CurrentBleed * progress * 0.65f;
        result.Germs = -5f * progress;
        result.Health = StrayHealth(request, slips);
        result.Key = result.Integrity > 0.85f ? "surgery-sealed" : "surgery-searing";
    }

    private static void SetBone(in SurgeryRequest request, float progress, int slips, ref SurgeryResult result)
    {
        if (request.Kind is not SurgeryLayerKind.Bone || request.Integrity > 0.2f && !request.Retracted && request.BoneFloor <= 0f)
        {
            result.Rejected = true;
            result.Key = "surgery-bone-shut";
            return;
        }

        var span = MathF.Max(0f, request.CurrentBone - request.BoneFloor);
        var kept = Math.Clamp(1f - slips * 0.08f, 0.25f, 1f);
        result.Bone = -span * progress * kept;
        result.Finished = progress > 0.85f;
        if (slips > 3)
            result.Scar = 1;
        result.Key = request.BoneFloor > 0f ? "surgery-bone-field" : "surgery-bone-set";
    }

    private static void Extract(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        if (!request.Implant && !request.CavityReady && request.Kind is not SurgeryLayerKind.Globe)
        {
            result.Rejected = true;
            result.Key = "surgery-cavity-shut";
            return;
        }

        if (request.Implant && !request.LayersOpen && !request.CavityReady && request.Kind is not SurgeryLayerKind.Globe)
        {
            result.Rejected = true;
            result.Key = "surgery-cavity-shut";
            return;
        }

        if (request.Yank || slips > 6)
        {
            if (request.Implant)
                result.RemoveImplant = true;
            else
            {
                result.RemoveOrgan = true;
                result.Rupture = true;
                result.OrganDamage = 20f;
                result.Eyes = request.Kind is SurgeryLayerKind.Globe ? 1 : 0;
            }

            result.Bleed = 8f * messy;
            result.Key = "surgery-yank";
            return;
        }

        if (progress > 0.85f)
        {
            if (request.Implant)
            {
                result.RemoveImplant = true;
                result.Bleed = 1f * messy;
                result.Key = "surgery-implant-out";
            }
            else
            {
                result.RemoveOrgan = true;
                result.Bleed = 1.5f * messy;
                result.Key = "surgery-organ-out";
            }
        }
    }

    private static void Insert(in SurgeryRequest request, float progress, int slips, float messy, ref SurgeryResult result)
    {
        if (!request.Implant && !request.CavityReady && request.Kind is not SurgeryLayerKind.Globe)
        {
            result.Rejected = true;
            result.Key = "surgery-cavity-shut";
            return;
        }

        if (request.Implant && !request.LayersOpen && !request.CavityReady && request.Kind is not SurgeryLayerKind.Globe)
        {
            result.Rejected = true;
            result.Key = "surgery-cavity-shut";
            return;
        }

        if (progress <= 0.8f)
        {
            result.Key = "surgery-insert-short";
            return;
        }

        if (request.Implant)
        {
            result.PlaceImplant = true;
            result.Bleed = slips * messy;
            result.Key = "surgery-implant-in";
            return;
        }

        result.PlaceOrgan = true;
        result.Rupture = request.Yank || slips > 5;
        result.OrganDamage = result.Rupture ? 25f : 0f;
        result.Bleed = slips * messy;
        result.Key = result.Rupture ? "surgery-insert-torn" : "surgery-inserted";
    }

    private static float Pool(in SurgeryRequest request)
    {
        return request.Pool > 0f ? request.Pool : LimbWoundRules.HealthFor(request.Zone);
    }

    private static float StrayHealth(in SurgeryRequest request, int slips)
    {
        if (slips <= 0)
            return 0f;

        return -Pool(request) * 0.4f * slips;
    }
}
