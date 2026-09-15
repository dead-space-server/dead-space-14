// SPDX-FileCopyrightText: 2026 Kofeecheks
// SPDX-License-Identifier: LicenseRef-Kofeecheks

using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;

namespace Content.Server.DeadSpace._Soyuz.Atmos.Reactions;

internal static class SoyuzGasReactionHelpers
{
    public static bool HasApproximateRatios(float tolerance, params (float Amount, float Ratio)[] reactants)
    {
        var totalAmount = 0f;
        var totalRatio = 0f;

        foreach (var (amount, ratio) in reactants)
        {
            totalAmount += amount;
            totalRatio += ratio;
        }

        if (totalAmount <= 0f || totalRatio <= 0f)
            return false;

        foreach (var (amount, ratio) in reactants)
        {
            var share = amount / totalAmount;
            var target = ratio / totalRatio;

            if (MathF.Abs(share - target) > tolerance)
                return false;
        }

        return true;
    }

    public static void ApplyEnergy(
        GasMixture mixture,
        AtmosphereSystem atmosphereSystem,
        float heatScale,
        float oldHeatCapacity,
        float oldTemperature,
        float energyReleased)
    {
        if (energyReleased == 0f)
            return;

        var adjustedEnergy = energyReleased / heatScale;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity <= Atmospherics.MinimumHeatCapacity)
            return;

        mixture.Temperature = MathF.Max(
            Atmospherics.TCMB,
            (oldTemperature * oldHeatCapacity + adjustedEnergy) / newHeatCapacity);
    }
}
