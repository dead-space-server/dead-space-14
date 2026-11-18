// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Husbandry.Core;

/// <summary>
/// Rules for the place animals live in (a pen): a flood over the cells they can stand on, cut off at a number of
/// cells. Engine independent on purpose: the cells are plain pairs, what is free is told by the caller.
/// </summary>
public static class PenRules
{
    /// <summary>
    /// Floods from the starting cells over the free cells, up and down and sideways (not across the corners, a
    /// diagonal gap between two walls does not let anybody through). The cells are reached evenly from all the starts,
    /// so a flood that is cut off gives the cells nearest to the starts.
    /// </summary>
    /// <param name="starts">Where it starts, the ones that are not free are left out.</param>
    /// <param name="isFree">Whether somebody can stand on the cell: no wall, no door, there is a floor.</param>
    /// <param name="cells">The cells that were reached.</param>
    /// <param name="maxCells">The flood stops when it has this many cells.</param>
    /// <returns>True if the flood was cut off: there are free cells that it did not take. False if the place is
    /// closed in and the whole of it is in <paramref name="cells"/>.</returns>
    public static bool Flood(
        IEnumerable<(int X, int Y)> starts,
        Func<(int X, int Y), bool> isFree,
        HashSet<(int X, int Y)> cells,
        int maxCells)
    {
        cells.Clear();
        var queue = new Queue<(int X, int Y)>();

        foreach (var start in starts)
        {
            if (cells.Contains(start) || !isFree(start))
                continue;

            if (cells.Count >= maxCells)
                return true;

            cells.Add(start);
            queue.Enqueue(start);
        }

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            for (var i = 0; i < 4; i++)
            {
                // Up and down and sideways, no array for it: this runs for every cell of a pen.
                var next = i switch
                {
                    0 => (x + 1, y),
                    1 => (x - 1, y),
                    2 => (x, y + 1),
                    _ => (x, y - 1),
                };

                if (cells.Contains(next) || !isFree(next))
                    continue;

                if (cells.Count >= maxCells)
                    return true;

                cells.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }

    /// <summary>
    /// The shortest way over the walkable cells (up and down and sideways) from <paramref name="start"/> to the
    /// nearest cell that is a goal. A breadth-first search: one run finds the nearest of any number of goals, so an
    /// animal that looks for food among many things needs one run, not a path for each of them. Engine independent.
    /// </summary>
    /// <param name="isWalkable">Whether the cell can be stepped on (the start does not have to be).</param>
    /// <param name="isGoal">Whether the cell is a place to stop at.</param>
    /// <param name="maxCells">The search gives up after looking at this many cells.</param>
    /// <returns>The cells from the start to the goal, both included, or null if no goal can be reached.</returns>
    public static List<(int X, int Y)>? FindRoute(
        (int X, int Y) start,
        Func<(int X, int Y), bool> isWalkable,
        Func<(int X, int Y), bool> isGoal,
        int maxCells = 1024)
    {
        if (isGoal(start))
            return new List<(int X, int Y)> { start };

        var parents = new Dictionary<(int X, int Y), (int X, int Y)> { [start] = start };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            for (var i = 0; i < 4; i++)
            {
                var next = i switch
                {
                    0 => (current.X + 1, current.Y),
                    1 => (current.X - 1, current.Y),
                    2 => (current.X, current.Y + 1),
                    _ => (current.X, current.Y - 1),
                };

                if (parents.ContainsKey(next) || !isWalkable(next))
                    continue;

                parents[next] = current;

                if (isGoal(next))
                {
                    var route = new List<(int X, int Y)> { next };
                    var step = next;
                    while (step != start)
                    {
                        step = parents[step];
                        route.Add(step);
                    }

                    route.Reverse();
                    return route;
                }

                if (parents.Count >= maxCells)
                    return null;

                queue.Enqueue(next);
            }
        }

        return null;
    }
}
