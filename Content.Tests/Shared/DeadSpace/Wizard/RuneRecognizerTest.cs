// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using NUnit.Framework;

namespace Content.Tests.Shared.DeadSpace.Wizard;

[TestFixture]
public sealed class RuneRecognizerTest
{
    private static readonly Dictionary<string, Vector2[]> Shapes = new()
    {
        ["fire"] = new[] { new Vector2(0.1f, 0.9f), new Vector2(0.5f, 0.1f), new Vector2(0.9f, 0.9f), new Vector2(0.1f, 0.9f) },
        ["void"] = new[] { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f), new Vector2(0.5f, 0.9f), new Vector2(0.1f, 0.1f) },
        ["ice"] = new[] { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f), new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.9f) },
        ["lightning"] = new[] { new Vector2(0.6f, 0.05f), new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), new Vector2(0.4f, 0.95f) },
        ["life"] = new[] { new Vector2(0.15f, 0.9f), new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f), new Vector2(0.85f, 0.1f) },
        ["earth"] = new[] { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f), new Vector2(0.9f, 0.9f), new Vector2(0.1f, 0.9f), new Vector2(0.1f, 0.1f) },
    };

    private static List<(string Id, Vector2[] Template)> Templates()
    {
        return Shapes.Select(pair => (pair.Key, RuneRecognizer.Normalize(pair.Value))).ToList();
    }

    private static List<Vector2> Draw(IReadOnlyList<Vector2> shape, float scale, Vector2 offset, float noise, int seed, bool reverse = false)
    {
        var random = new Random(seed);
        var points = new List<Vector2>();
        var source = reverse ? shape.Reverse().ToList() : shape.ToList();

        for (var i = 1; i < source.Count; i++)
        {
            for (var s = 0; s < 12; s++)
            {
                var point = Vector2.Lerp(source[i - 1], source[i], s / 12f) * scale + offset;
                points.Add(point + new Vector2(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f) * noise);
            }
        }

        points.Add(source[^1] * scale + offset);
        return points;
    }

    [Test]
    public void EveryShapeIsRecognisedWhenDrawnSloppily()
    {
        var templates = Templates();
        foreach (var (id, shape) in Shapes)
        {
            for (var seed = 0; seed < 5; seed++)
            {
                var stroke = Draw(shape, 180f + seed * 40f, new Vector2(300f, 120f), 14f, seed);
                Assert.That(RuneRecognizer.TryMatch(stroke, templates, 0.72f, out var match, out var score), Is.True, $"{id} seed {seed} score {score}");
                Assert.That(match, Is.EqualTo(id), $"{id} seed {seed}");
            }
        }
    }

    [Test]
    public void DirectionOfTheStrokeDoesNotMatter()
    {
        var stroke = Draw(Shapes["lightning"], 200f, Vector2.Zero, 6f, 7, reverse: true);
        Assert.That(RuneRecognizer.TryMatch(stroke, Templates(), 0.72f, out var match, out _), Is.True);
        Assert.That(match, Is.EqualTo("lightning"));
    }

    [Test]
    public void UpsideDownTriangleIsNotFire()
    {
        var stroke = Draw(Shapes["void"], 200f, Vector2.Zero, 4f, 3);
        RuneRecognizer.TryMatch(stroke, Templates(), 0.72f, out var match, out _);
        Assert.That(match, Is.EqualTo("void"));
    }

    [Test]
    public void FusionSlashIsNotAStraightModifier()
    {
        var fusion = new[] { new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.1f) };
        var range = new[] { new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.5f) };
        var whisper = new[] { new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.95f) };
        var blood = new[] { new Vector2(0.2f, 0.1f), new Vector2(0.2f, 0.9f), new Vector2(0.85f, 0.9f) };
        var shapes = new Dictionary<string, Vector2[]>
        {
            ["fusion"] = fusion,
            ["range"] = range,
            ["whisper"] = whisper,
            ["blood"] = blood,
        };
        var templates = shapes.Select(pair => (pair.Key, RuneRecognizer.Normalize(pair.Value))).ToList();

        foreach (var (id, shape) in shapes)
        {
            for (var seed = 0; seed < 5; seed++)
            {
                RuneRecognizer.TryMatch(Draw(shape, 200f, Vector2.Zero, 14f, seed), templates, 0.6f, out var match, out _);
                Assert.That(match, Is.EqualTo(id), $"{id} seed {seed}");
            }
        }
    }

    [Test]
    public void ScribbleIsRejected()
    {
        var random = new Random(42);
        var stroke = new List<Vector2>();
        for (var i = 0; i < 80; i++)
            stroke.Add(new Vector2(random.NextSingle(), random.NextSingle()) * 200f);

        Assert.That(RuneRecognizer.TryMatch(stroke, Templates(), 0.72f, out _, out _), Is.False);
    }

    [Test]
    public void DotIsNotAStroke()
    {
        var stroke = new List<Vector2> { new(10f, 10f), new(10f, 10f) };
        Assert.That(RuneRecognizer.Normalize(stroke), Is.Null);
    }
}
