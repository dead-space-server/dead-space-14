// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.IO;
using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.DeadSpace.Surgery;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Surgery;

[TestFixture]
public sealed class SurgeryProcedureTest
{
    [Test]
    public void TakeHeartOpensCutsOutAndCloses()
    {
        var steps = Load("TakeHeart");
        var lungs = Load("TakeLungs");
        Assert.That(steps.Find(step => step.Verb == SurgeryVerbKind.Extract)!.Organ, Is.EqualTo("Heart"));
        Assert.That(lungs.Find(step => step.Verb == SurgeryVerbKind.Extract)!.Organ, Is.EqualTo("Lungs"));

        var layers = Fresh(SurgeryLayerKind.Skin, SurgeryLayerKind.Bone, SurgeryLayerKind.Cavity);
        var cursor = 0;
        string picture;

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.Incise);
        Assert.That(picture, Is.EqualTo("torso_cut4"));

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.Retract);
        Assert.That(picture, Is.EqualTo("torso_open"));
        Assert.That(layers[0].Retracted, Is.True);

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.SawBone);
        Assert.That(picture, Is.EqualTo("torso_ribs"));

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.Retract);
        Assert.That(picture, Is.EqualTo("torso_organs"));
        Assert.That(layers[^1].Retracted, Is.True);

        var extracted = false;
        Run(steps, layers, ref cursor, SurgeryVerbKind.Clamp);
        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.Extract, sawOrgan: true, removed: value => extracted = value);
        Assert.That(extracted, Is.True);
        Assert.That(picture, Is.EqualTo("torso_organs"));

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.MendBone);
        Assert.That(picture, Is.EqualTo("torso_open"));

        picture = Run(steps, layers, ref cursor, SurgeryVerbKind.Cauterize);
        Assert.That(picture, Is.EqualTo("none"));
        Assert.That(layers[0].Retracted, Is.False);
        Assert.That(cursor, Is.EqualTo(steps.Count));
    }

    [Test]
    public void AShortCutStillOpensWhenPulled()
    {
        var steps = Load("TakeHeart");
        var layers = Fresh(SurgeryLayerKind.Skin, SurgeryLayerKind.Bone, SurgeryLayerKind.Cavity);
        var cursor = 0;

        var cut = Run(steps, layers, ref cursor, SurgeryVerbKind.Incise, progress: 0.3f);
        Assert.That(cut, Is.EqualTo("torso_cut1"));
        Assert.That(steps[cursor].Verb, Is.EqualTo(SurgeryVerbKind.Incise));

        Run(steps, layers, ref cursor, SurgeryVerbKind.Incise);
        Assert.That(steps[cursor].Verb, Is.EqualTo(SurgeryVerbKind.Retract));

        var open = Run(steps, layers, ref cursor, SurgeryVerbKind.Retract);
        Assert.That(layers[0].Retracted, Is.True);
        Assert.That(open, Is.EqualTo("torso_open"));
    }

    [Test]
    public void OpenHeadIsOnlyData()
    {
        var torso = Load("OpenTorso");
        var head = Load("OpenHead");

        Assert.That(head.Count, Is.EqualTo(torso.Count));
        Assert.That(head[0].Layer, Is.EqualTo(SurgeryLayerKind.Skin));
        Assert.That(head.Exists(step => step.Verb == SurgeryVerbKind.SawBone && step.Layer == SurgeryLayerKind.Bone), Is.True);
        Assert.That(head[^1].Verb, Is.EqualTo(SurgeryVerbKind.Cauterize));
        Assert.That(head[^1].Layer, Is.EqualTo(SurgeryLayerKind.Skin));
    }

    [Test]
    public void AnOpenChestStaysWhenTheHeadIsChosen()
    {
        var site = new SurgerySiteComponent
        {
            Zone = LimbZone.Head,
            Done = false,
            Layers =
            {
                new SurgeryLayerState { Kind = SurgeryLayerKind.Skin, Integrity = 1f },
            },
            Kept =
            {
                [SurgeryWounds.Key(LimbZone.Torso, "", false)] = new SurgeryKept
                {
                    Zone = LimbZone.Torso,
                    Layers =
                    {
                        new SurgeryLayerState { Kind = SurgeryLayerKind.Skin, Integrity = 0f, Retracted = true },
                        new SurgeryLayerState { Kind = SurgeryLayerKind.Bone, Integrity = 0f, Retracted = true },
                    },
                },
            },
        };

        var open = new List<SurgeryKept>();
        SurgeryWounds.Collect(site, open);
        Assert.That(open.ConvertAll(wound => wound.Zone), Is.EqualTo(new[] { LimbZone.Torso }));
    }

    private static string Run(
        List<SurgeryStep> steps,
        List<SurgeryLayerState> layers,
        ref int cursor,
        SurgeryVerbKind verb,
        bool sawOrgan = false,
        Action<bool> removed = null,
        float progress = 1f)
    {
        Assert.That(steps[cursor].Verb, Is.EqualTo(verb));
        var index = SurgeryProcedure.Index(layers, steps[cursor].Layer);
        var layer = layers[index];
        var cavity = false;
        foreach (var item in layers)
        {
            if (item.Kind is SurgeryLayerKind.Cavity or SurgeryLayerKind.Globe && (item.Retracted || item.Integrity < 0.2f))
                cavity = true;
        }

        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = verb,
            Zone = LimbZone.Torso,
            Kind = layer.Kind,
            Integrity = layer.Integrity,
            Retracted = layer.Retracted,
            CavityReady = cavity,
            Progress = progress,
            ToolPrecision = 0.92f,
        });
        layer.Integrity = result.Integrity;
        layer.Retracted = result.Retracted;
        layers[index] = layer;
        SurgeryProcedure.Reveal(layers);
        if (sawOrgan)
            removed?.Invoke(result.RemoveOrgan);
        if (verb == SurgeryVerbKind.Clamp)
        {
            if (progress >= 0.8f)
                cursor++;
        }
        else if (SurgeryProcedure.Advances(verb, layers[index], result))
            cursor++;

        var amount = layer.Retracted || layer.Integrity < 0.2f ? 1f : Math.Clamp(1f - layer.Integrity, 0f, 1f);
        if (verb == SurgeryVerbKind.Cauterize && !layers[0].Retracted && layers[0].Integrity > SurgeryProcedure.CutDone)
            amount = 0f;
        return Picture(layers, amount);
    }

    private static void Act(List<SurgeryStep> steps, List<SurgeryLayerState> layers, SurgeryVerbKind verb, float progress)
    {
        var step = SurgeryProcedure.Current(steps, layers);
        Assert.That(steps[step].Verb, Is.EqualTo(verb));
        var index = SurgeryProcedure.Index(layers, steps[step].Layer);
        var layer = layers[index];
        var result = SurgeryCut.Resolve(new SurgeryRequest
        {
            Verb = verb,
            Zone = LimbZone.Torso,
            Kind = layer.Kind,
            Integrity = layer.Integrity,
            Retracted = layer.Retracted,
            Progress = progress,
            ToolPrecision = 0.92f,
        });
        layer.Integrity = result.Integrity;
        layer.Retracted = result.Retracted;
        layers[index] = layer;
        SurgeryProcedure.Reveal(layers);
    }

    private static string Picture(List<SurgeryLayerState> layers, float amount)
    {
        var kinds = new byte[layers.Count];
        var integrity = new float[layers.Count];
        var retracted = new byte[layers.Count];
        for (var i = 0; i < layers.Count; i++)
        {
            kinds[i] = (byte) layers[i].Kind;
            integrity[i] = layers[i].Integrity;
            retracted[i] = layers[i].Retracted ? (byte) 1 : (byte) 0;
        }

        return SurgeryProcedure.Wound("torso", kinds, integrity, retracted, amount, true);
    }

    private static List<SurgeryLayerState> Fresh(params SurgeryLayerKind[] kinds)
    {
        var layers = new List<SurgeryLayerState>();
        foreach (var kind in kinds)
            layers.Add(new SurgeryLayerState { Kind = kind, Integrity = 1f });
        return layers;
    }

    private static List<SurgeryStep> Load(string id)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string path = "";
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Resources", "Prototypes", "_DeadSpace", "Surgery", "surgery.yml");
            if (File.Exists(candidate))
            {
                path = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.That(path, Is.Not.Empty);
        var lines = File.ReadAllLines(path);
        var steps = new List<SurgeryStep>();
        var grab = false;
        SurgeryVerbKind verb = SurgeryVerbKind.Inspect;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line == "id: " + id)
            {
                grab = true;
                continue;
            }

            if (!grab)
                continue;

            if (line.StartsWith("- type:", StringComparison.Ordinal))
                break;

            if (line.StartsWith("- verb:", StringComparison.Ordinal) || line.StartsWith("verb:", StringComparison.Ordinal))
            {
                var value = line.StartsWith("- verb:", StringComparison.Ordinal)
                    ? line["- verb:".Length..]
                    : line["verb:".Length..];
                verb = Enum.Parse<SurgeryVerbKind>(value.Trim());
                continue;
            }

            if (line.StartsWith("layer:", StringComparison.Ordinal))
            {
                steps.Add(new SurgeryStep
                {
                    Verb = verb,
                    Layer = Enum.Parse<SurgeryLayerKind>(line["layer:".Length..].Trim()),
                });
                continue;
            }

            if (line.StartsWith("organ:", StringComparison.Ordinal) && steps.Count > 0)
                steps[^1].Organ = line["organ:".Length..].Trim();
        }

        Assert.That(steps, Is.Not.Empty, id);
        return steps;
    }
}
