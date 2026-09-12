// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

#nullable enable

using System.Numerics;
using Content.Client.DeadSpace.Audio;
using Content.IntegrationTests.Tests.Atmos;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.DeadSpace.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.DeadSpace.Audio;

public sealed class AtmosphericAudioTest : AtmosTest
{
    [Test]
    public async Task PressureChangesMuffleTheSameFloorAndSelfFootsteps()
    {
        var wasSimulated = ProcessEnt.Comp1.Simulated;
        await Server.WaitPost(() => SAtmos.SetAtmosphereSimulation(ProcessEnt, false));

        var grid = CEntMan.GetEntity(SEntMan.GetNetEntity(ProcessEnt));
        var cfg = Client.ResolveDependency<IConfigurationManager>();
        var oldSetting = cfg.GetCVar(AreaEchoCVars.SpaceMuffling);
        await Client.WaitPost(() => cfg.SetCVar(AreaEchoCVars.SpaceMuffling, true));
        var previousOcclusion = -1f;
        byte[]? initialOpacity = null;
        ThermalByte initialTemperature = default;

        try
        {
            // The map and floor never change, only invisible nitrogen at constant temperature.
            foreach (var fraction in new[] { 1f, 0.5f, 0.25f, 0f })
            {
                await Server.WaitAssertion(() =>
                {
                    var mixture = SAtmos.GetTileMixture(ProcessEnt, null, Vector2i.Zero)
                                  ?? throw new AssertionException("The test floor has no gas mixture.");
                    mixture.Clear();
                    mixture.Temperature = Atmospherics.T20C;
                    mixture.SetMoles(Gas.Nitrogen,
                        fraction * Atmospherics.OneAtmosphere * mixture.Volume /
                        (Atmospherics.R * mixture.Temperature));
                    SAtmos.InvalidateVisuals((ProcessEnt.Owner, ProcessEnt.Comp2), Vector2i.Zero);
                });

                await RunTicks(60);

                await Client.WaitAssertion(() =>
                {
                    var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(grid);
                    var chunk = overlay.Chunks[SharedGasTileOverlaySystem.GetGasChunkIndices(Vector2i.Zero)];
                    var local = Vector2i.Zero - chunk.Origin;
                    var data = chunk.TileData[local.X + local.Y * SharedGasTileOverlaySystem.ChunkSize];
                    Assert.That(data.ByteGasPressure,
                        Is.EqualTo(AtmosphericAudio.EncodePressure(fraction * Atmospherics.OneAtmosphere)).Within(1));
                    if (initialOpacity == null)
                    {
                        initialOpacity = (byte[]) data.Opacity.Clone();
                        initialTemperature = data.ByteGasTemperature;
                    }
                    else
                    {
                        Assert.That(data.Opacity, Is.EqualTo(initialOpacity), "Visible gas did not change.");
                        if (fraction > 0f)
                            Assert.That(data.ByteGasTemperature, Is.EqualTo(initialTemperature));
                    }

                    var position = Client.System<SharedTransformSystem>().ToMapCoordinates(
                        new EntityCoordinates(grid, new Vector2(0.5f)));
                    // Own footsteps have zero source-listener distance and must still receive the pressure filter.
                    var muffling = Client.System<SpaceMufflingSystem>();
                    var occlusion = muffling.GetOcclusion(position, Vector2.Zero, 0f, CPlayer);
                    Assert.That(occlusion, Is.GreaterThan(previousOcclusion));
                    if (fraction == 0f)
                        Assert.That(occlusion, Is.EqualTo(AtmosphericAudio.VacuumOcclusion));
                    previousOcclusion = occlusion;
                });
            }

            await Client.WaitAssertion(() =>
            {
                cfg.SetCVar(AreaEchoCVars.SpaceMuffling, false);
                var position = Client.System<SharedTransformSystem>().ToMapCoordinates(
                    new EntityCoordinates(grid, new Vector2(0.5f)));
                Assert.That(Client.System<SpaceMufflingSystem>().GetOcclusion(position, Vector2.Zero, 0f, CPlayer),
                    Is.Zero);
            });
        }
        finally
        {
            await Client.WaitPost(() => cfg.SetCVar(AreaEchoCVars.SpaceMuffling, oldSetting));
            await Server.WaitPost(() => SAtmos.SetAtmosphereSimulation(ProcessEnt, wasSimulated));
        }
    }
}
