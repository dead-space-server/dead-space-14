// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Content.Shared.DeadSpace.Husbandry.Core;
using NUnit.Framework;

namespace Content.Tests.DeadSpace.Husbandry;

[TestFixture]
public sealed class HusbandryRulesTest
{
    [Test]
    public void NeedDrainsAndNeverGoesBelowZero()
    {
        var value = 1f;
        var empty = NeedRules.Tick(ref value, decayPerMinute: 60f, seconds: 2f);

        Assert.That(value, Is.EqualTo(0f));
        Assert.That(empty, Is.GreaterThan(0f), "the need ran out, the time on zero counts");
    }

    [Test]
    public void NeedDrainIsProportionalToTime()
    {
        var value = 100f;
        var empty = NeedRules.Tick(ref value, decayPerMinute: 6f, seconds: 10f);

        Assert.That(value, Is.EqualTo(99f).Within(0.001f));
        Assert.That(empty, Is.EqualTo(0f));
    }

    [Test]
    public void ModifyIsClampedAndReportsRealChange()
    {
        var value = 90f;
        var change = NeedRules.Modify(ref value, 100f, 30f);

        Assert.That(value, Is.EqualTo(100f));
        Assert.That(change, Is.EqualTo(10f));

        change = NeedRules.Modify(ref value, 100f, -500f);
        Assert.That(value, Is.EqualTo(0f));
        Assert.That(change, Is.EqualTo(-100f));
    }

    [Test]
    public void CanAffordKeepsTheFloor()
    {
        Assert.That(NeedRules.CanAfford(50f, 10f, 40f), Is.True);
        Assert.That(NeedRules.CanAfford(50f, 11f, 40f), Is.False);
        Assert.That(NeedRules.CanAfford(5f, 10f), Is.False);
    }

    [Test]
    public void FractionIsClamped()
    {
        Assert.That(NeedRules.Fraction(50f, 100f), Is.EqualTo(0.5f));
        Assert.That(NeedRules.Fraction(500f, 100f), Is.EqualTo(1f));
        Assert.That(NeedRules.Fraction(-5f, 100f), Is.EqualTo(0f));
        Assert.That(NeedRules.Fraction(5f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void GrowthStagesGoInOrderAndEnd()
    {
        Assert.That(GrowthRules.TryGetNext(0, 3, out var next), Is.True);
        Assert.That(next, Is.EqualTo(1));
        Assert.That(GrowthRules.TryGetNext(2, 3, out _), Is.False);
    }

    [Test]
    public void SexRollFollowsTheChance()
    {
        Assert.That(GrowthRules.RollSex(0.1, 0.5f), Is.EqualTo(AnimalSexKind.Male));
        Assert.That(GrowthRules.RollSex(0.9, 0.5f), Is.EqualTo(AnimalSexKind.Female));
        Assert.That(GrowthRules.RollSex(0.0, 0f), Is.EqualTo(AnimalSexKind.Female));
    }

    [Test]
    public void OnlyAMaleAndAFemaleArePair()
    {
        Assert.That(BreedingRules.IsPair(AnimalSexKind.Male, AnimalSexKind.Female), Is.True);
        Assert.That(BreedingRules.IsPair(AnimalSexKind.Female, AnimalSexKind.Male), Is.True);
        Assert.That(BreedingRules.IsPair(AnimalSexKind.Female, AnimalSexKind.Female), Is.False);
    }

    [Test]
    public void BreedingStopsWhenCrowdedOrWhenTheServerIsFull()
    {
        Assert.That(BreedingRules.HasRoom(crowd: 3, crowdLimit: 6, total: 10, totalLimit: 100), Is.True);
        Assert.That(BreedingRules.HasRoom(crowd: 6, crowdLimit: 6, total: 10, totalLimit: 100), Is.False);
        Assert.That(BreedingRules.HasRoom(crowd: 1, crowdLimit: 6, total: 100, totalLimit: 100), Is.False);
        Assert.That(BreedingRules.HasRoom(crowd: 99, crowdLimit: 0, total: 999, totalLimit: 0), Is.True, "zero means no limit");
    }

    [Test]
    public void LitterIsCutToWhatTheServerCanTake()
    {
        Assert.That(BreedingRules.LitterSize(3, total: 10, totalLimit: 100), Is.EqualTo(3));
        Assert.That(BreedingRules.LitterSize(3, total: 99, totalLimit: 100), Is.EqualTo(1));
        Assert.That(BreedingRules.LitterSize(3, total: 150, totalLimit: 100), Is.EqualTo(0));
        Assert.That(BreedingRules.LitterSize(3, total: 150, totalLimit: 0), Is.EqualTo(3));
    }

    [Test]
    public void PenFillStopsAtWallsAndReportsClosed()
    {
        // A room of 3 by 3 with walls round it.
        Func<(int X, int Y), bool> free = c => c.X is >= 0 and <= 2 && c.Y is >= 0 and <= 2;
        var cells = new HashSet<(int X, int Y)>();

        var open = PenRules.Fill((1, 1), free, _ => true, cells);

        Assert.That(open, Is.False, "closed in by walls");
        Assert.That(cells, Has.Count.EqualTo(9));
    }

    [Test]
    public void PenFillOfAFieldIsCutByTheLimitAndOpen()
    {
        Func<(int X, int Y), bool> free = _ => true;
        var cells = new HashSet<(int X, int Y)>();

        var open = PenRules.Fill((0, 0), free, c => Math.Abs(c.X) <= 1 && Math.Abs(c.Y) <= 1, cells);

        Assert.That(open, Is.True, "free ground continues beyond the limit");
        Assert.That(cells, Has.Count.EqualTo(9));
    }

    [Test]
    public void PenFillDoesNotCrossDiagonalGaps()
    {
        // Two free cells that touch only by a corner are not connected.
        Func<(int X, int Y), bool> free = c => c is (0, 0) or (1, 1);
        var cells = new HashSet<(int X, int Y)>();

        PenRules.Fill((0, 0), free, _ => true, cells);

        Assert.That(cells, Has.Count.EqualTo(1));
    }

    [Test]
    public void RouteIsTheShortestWayOverTheWalkableCells()
    {
        // A wall of three cells with a gap at the bottom: the way goes round it.
        Func<(int X, int Y), bool> walkable = c => c.X is >= 0 and <= 4 && c.Y is >= 0 and <= 4 && !(c.X == 2 && c.Y is >= 1 and <= 3);

        var route = PenRules.FindRoute((0, 2), walkable, c => c == (4, 2));

        Assert.That(route, Is.Not.Null);
        Assert.That(route![0], Is.EqualTo((0, 2)));
        Assert.That(route[^1], Is.EqualTo((4, 2)));
        Assert.That(route, Has.Count.EqualTo(9), "over the top or the bottom of the wall: four steps there, two round, four on");
        Assert.That(route, Does.Not.Contain((2, 2)));
    }

    [Test]
    public void RouteFindsTheNearestOfManyGoalsAtOnce()
    {
        Func<(int X, int Y), bool> walkable = c => c.X is >= 0 and <= 9 && c.Y == 0;

        var route = PenRules.FindRoute((5, 0), walkable, c => c is (1, 0) or (8, 0));

        Assert.That(route![^1], Is.EqualTo((8, 0)), "three steps to the one, four to the other");
    }

    [Test]
    public void RouteIsNullWhenThereIsNoWayAndSimpleWhenAlreadyThere()
    {
        Func<(int X, int Y), bool> walkable = c => c.X is 0 or 1 or 3;

        Assert.That(PenRules.FindRoute((0, 0), walkable, c => c == (3, 0)), Is.Null);
        Assert.That(PenRules.FindRoute((0, 0), walkable, c => c == (0, 0)), Has.Count.EqualTo(1));
    }
}
