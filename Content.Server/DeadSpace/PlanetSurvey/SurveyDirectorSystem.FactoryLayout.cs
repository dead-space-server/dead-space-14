// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.DeadSpace.PlanetSurvey;

public sealed partial class SurveyDirectorSystem
{
    private const int ForgeWidth = 92;
    private const int ForgeHeight = 70;
    private const int PadGap = 7;
    private const int PadSize = 21;
    private const int DockSize = 13;
    private const int MinBlockWidth = 13;
    private const int MinBlockHeight = 11;
    private const int MinRoom = 5;

    private const string ForgeDoor = "SurveyAirlockNative";
    private const string ForgeLamp = "SurveyFactoryLamp";
    private const string ForgeWallLight = "SurveyFactoryWallLight";
    private const string ForgeStreet = "FloorSteelDirty";
    private const string ForgePad = "FloorDarkMono";
    private const string ForgeBridge = "Plating";
    private const string ForgeCatwalk = "Catwalk";
    private const string ForgeRailing = "Railing";
    private const string ForgeGrille = "Grille";
    private const string ForgeWindow = "ReinforcedWindow";
    private const string CoreProto = "SurveyFactoryCore";
    private const string RepairBay = "SurveyRepairBay";
    private const string Conveyor = "ConveyorBelt";

    private static readonly string[] ForgeWalls = { "WallReinforced", "WallReinforced", "WallReinforcedRust", "WallPlastitanium" };
    private static readonly string[] Shells = { "SurveyFactoryShellHeavy", "SurveyFactoryShellScout", "SurveyFactoryShellSec", "SurveyFactoryShellEngi" };
    private static readonly string[] StreetJunk = { "SurveyCrateLoot", "MachineFrameDestroyed", "SurveyFactoryCondenser", "SurveyFactorySilo", "Rack", "SurveyFactorySmes" };
    private static readonly (string Kind, int Weight)[] FillerRooms =
    {
        ("assembly", 3), ("foundry", 3), ("press", 2), ("storage", 2), ("charging", 2), ("silo", 1), ("server", 1), ("biomass", 1),
    };

    private static readonly Dictionary<string, string> RoomFloors = new()
    {
        ["core"] = "FloorRedCircuit",
        ["power"] = "FloorReinforced",
        ["comms"] = "FloorDarkMono",
        ["repair"] = "FloorWhite",
        ["assembly"] = "FloorSteelCheckerDark",
        ["foundry"] = "FloorMetalDiamond",
        ["press"] = "FloorTechMaint",
        ["storage"] = "FloorSteelDirty",
        ["charging"] = "FloorDarkDiagonal",
        ["silo"] = "FloorMining",
        ["server"] = "FloorGreenCircuit",
        ["biomass"] = "FloorDarkPlastic",
    };

    private enum ForgeCell : byte
    {
        Void,
        Street,
        Floor,
        Wall,
        Door,
        Window,
        Pad,
        Bridge,
    }

    private sealed class ForgeRoom(Box2i box, int block)
    {
        public readonly Box2i Box = box;
        public readonly int Block = block;
        public string Kind = "storage";
        public int Area => Box.Width * Box.Height;
        public Vector2 Centre => new(Box.Left + Box.Width / 2f, Box.Bottom + Box.Height / 2f);
    }

    private sealed class ForgePlan
    {
        public readonly int Offset = PadGap + PadSize;
        public readonly int Height = ForgeHeight + 2 * PadGap + PadSize + DockSize;
        public readonly ForgeCell[,] Cells;
        public readonly int[,] Room;
        public readonly List<ForgeRoom> Rooms = new();
        public readonly List<Box2i> Blocks = new();
        public readonly List<string> BlockWalls = new();
        public readonly List<(Vector2i Cell, Angle Rotation)> Railings = new();
        public int MainStreet = -1;
        public int CoreBlock = -1;

        public ForgePlan()
        {
            Cells = new ForgeCell[ForgeWidth, Height];
            Room = new int[ForgeWidth, Height];
            for (var x = 0; x < ForgeWidth; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    Room[x, y] = -1;
                }
            }
        }

        public bool Inside(int x, int y) => x >= 0 && x < ForgeWidth && y + Offset >= 0 && y + Offset < Height;

        public ForgeCell Get(int x, int y) => Inside(x, y) ? Cells[x, y + Offset] : ForgeCell.Void;

        public void Set(int x, int y, ForgeCell cell)
        {
            if (Inside(x, y))
                Cells[x, y + Offset] = cell;
        }

        public int RoomAt(int x, int y) => Inside(x, y) ? Room[x, y + Offset] : -1;

        public void SetRoom(int x, int y, int room)
        {
            if (Inside(x, y))
                Room[x, y + Offset] = room;
        }
    }

    private (Vector2 Min, Vector2 Max, Vector2 Landing, Vector2 Far) BuildFactory(Entity<SurveyDirectorComponent> ent, SurveyWorldPrototype world, Vector2 centre, List<SurveySpot> spots, System.Random random)
    {
        var map = ent.Owner;
        var grid = Comp<MapGridComponent>(map);
        RemComp<BiomeComponent>(map);

        var plan = new ForgePlan();
        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = 0; y < ForgeHeight; y++)
            {
                plan.Set(x, y, ForgeCell.Street);
            }
        }

        SplitBlocks(plan, new Box2i(0, 0, ForgeWidth, ForgeHeight), 0, random);
        plan.CoreBlock = PickCoreBlock(plan);

        for (var b = 0; b < plan.Blocks.Count; b++)
        {
            BuildBlock(plan, b, random);
        }

        AssignRooms(plan, world, random);
        var (landing, dock) = BuildApproach(plan, random);
        CapStreets(plan);
        CollectRailings(plan);

        var origin = new Vector2i((int) (centre.X - ForgeWidth / 2f), (int) (centre.Y - ForgeHeight / 2f));
        LayForge(ent, plan, origin, map, grid, world, spots, random);

        var min = (Vector2) origin;
        var max = origin + new Vector2(ForgeWidth, ForgeHeight);
        return (min, max, origin + landing, origin + dock);
    }

    private void SplitBlocks(ForgePlan plan, Box2i area, int depth, System.Random random)
    {
        var street = depth == 0 ? 3 : 2;
        var canSplitX = area.Width >= 2 * MinBlockWidth + street;
        var canSplitY = area.Height >= 2 * MinBlockHeight + street;
        var small = area.Width <= 30 && area.Height <= 24;

        if (!canSplitX && !canSplitY || depth >= 5 || depth > 0 && small && random.NextDouble() < 0.7)
        {
            plan.Blocks.Add(area);
            return;
        }

        var vertical = depth == 0 && canSplitX || canSplitX && (!canSplitY || area.Width > area.Height * 1.1f || area.Width * 1.1f >= area.Height && random.NextDouble() < 0.5);
        if (vertical)
        {
            var left = Math.Clamp((int) (area.Width * (0.38 + random.NextDouble() * 0.24)), MinBlockWidth, area.Width - street - MinBlockWidth);
            if (depth == 0)
                plan.MainStreet = area.Left + left;

            SplitBlocks(plan, new Box2i(area.Left, area.Bottom, area.Left + left, area.Top), depth + 1, random);
            SplitBlocks(plan, new Box2i(area.Left + left + street, area.Bottom, area.Right, area.Top), depth + 1, random);
            return;
        }

        var low = Math.Clamp((int) (area.Height * (0.38 + random.NextDouble() * 0.24)), MinBlockHeight, area.Height - street - MinBlockHeight);
        SplitBlocks(plan, new Box2i(area.Left, area.Bottom, area.Right, area.Bottom + low), depth + 1, random);
        SplitBlocks(plan, new Box2i(area.Left, area.Bottom + low + street, area.Right, area.Top), depth + 1, random);
    }

    private static int PickCoreBlock(ForgePlan plan)
    {
        var middle = new Vector2(ForgeWidth / 2f, ForgeHeight / 2f);
        var best = 0;
        var bestScore = float.MaxValue;
        for (var i = 0; i < plan.Blocks.Count; i++)
        {
            var block = plan.Blocks[i];
            if (block.Width < 15 || block.Height < 13)
                continue;

            var centre = new Vector2(block.Left + block.Width / 2f, block.Bottom + block.Height / 2f);
            var score = Vector2.Distance(centre, middle) - block.Width * block.Height * 0.02f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = i;
        }

        return best;
    }

    private void BuildBlock(ForgePlan plan, int index, System.Random random)
    {
        var block = plan.Blocks[index];
        plan.BlockWalls.Add(index == plan.CoreBlock ? "WallPlastitanium" : ForgeWalls[random.Next(ForgeWalls.Length)]);

        for (var x = block.Left; x < block.Right; x++)
        {
            for (var y = block.Bottom; y < block.Top; y++)
            {
                var edge = x == block.Left || y == block.Bottom || x == block.Right - 1 || y == block.Top - 1;
                plan.Set(x, y, edge ? ForgeCell.Wall : ForgeCell.Floor);
            }
        }

        var interior = new Box2i(block.Left + 1, block.Bottom + 1, block.Right - 1, block.Top - 1);
        SplitRooms(plan, interior, index, index == plan.CoreBlock, random);
        PunchEntrances(plan, block, random);
    }

    private void SplitRooms(ForgePlan plan, Box2i area, int block, bool core, System.Random random)
    {
        var canSplitX = area.Width >= 2 * MinRoom + 1;
        var canSplitY = area.Height >= 2 * MinRoom + 1;
        var stop = core
            ? area.Width <= 22 && area.Height <= 18
            : area.Width <= 13 && area.Height <= 11 && random.NextDouble() < 0.6 || area.Width * area.Height <= 60;

        if (!canSplitX && !canSplitY || stop)
        {
            var room = plan.Rooms.Count;
            plan.Rooms.Add(new ForgeRoom(area, block));
            for (var x = area.Left; x < area.Right; x++)
            {
                for (var y = area.Bottom; y < area.Top; y++)
                {
                    plan.SetRoom(x, y, room);
                }
            }

            return;
        }

        var vertical = canSplitX && (!canSplitY || area.Width >= area.Height);
        if (vertical)
        {
            var at = area.Left + Math.Clamp((int) (area.Width * (0.35 + random.NextDouble() * 0.3)), MinRoom, area.Width - MinRoom - 1);
            for (var y = area.Bottom; y < area.Top; y++)
            {
                plan.Set(at, y, ForgeCell.Wall);
            }

            SplitRooms(plan, new Box2i(area.Left, area.Bottom, at, area.Top), block, false, random);
            SplitRooms(plan, new Box2i(at + 1, area.Bottom, area.Right, area.Top), block, false, random);
            InnerDoor(plan, Enumerable.Range(area.Bottom, area.Height).Select(y => new Vector2i(at, y)).ToList(), new Vector2i(1, 0), random);
            return;
        }

        var row = area.Bottom + Math.Clamp((int) (area.Height * (0.35 + random.NextDouble() * 0.3)), MinRoom, area.Height - MinRoom - 1);
        for (var x = area.Left; x < area.Right; x++)
        {
            plan.Set(x, row, ForgeCell.Wall);
        }

        SplitRooms(plan, new Box2i(area.Left, area.Bottom, area.Right, row), block, false, random);
        SplitRooms(plan, new Box2i(area.Left, row + 1, area.Right, area.Top), block, false, random);
        InnerDoor(plan, Enumerable.Range(area.Left, area.Width).Select(x => new Vector2i(x, row)).ToList(), new Vector2i(0, 1), random);
    }

    private static void InnerDoor(ForgePlan plan, List<Vector2i> wall, Vector2i across, System.Random random)
    {
        var options = wall.Where(c => plan.Get(c.X + across.X, c.Y + across.Y) == ForgeCell.Floor &&
                                      plan.Get(c.X - across.X, c.Y - across.Y) == ForgeCell.Floor)
            .OrderBy(_ => random.Next())
            .ToList();

        if (options.Count == 0)
            return;

        var door = options[0];
        plan.Set(door.X, door.Y, ForgeCell.Door);

        if (wall.Count < 11 || random.NextDouble() > 0.4)
            return;

        var along = new Vector2i(across.Y, across.X);
        var second = door + along;
        if (options.Contains(second))
            plan.Set(second.X, second.Y, ForgeCell.Door);
    }

    private static void PunchEntrances(ForgePlan plan, Box2i block, System.Random random)
    {
        var sides = new List<(List<Vector2i> Cells, Vector2i Out)>
        {
            (Enumerable.Range(block.Left + 1, block.Width - 2).Select(x => new Vector2i(x, block.Bottom)).ToList(), new Vector2i(0, -1)),
            (Enumerable.Range(block.Left + 1, block.Width - 2).Select(x => new Vector2i(x, block.Top - 1)).ToList(), new Vector2i(0, 1)),
            (Enumerable.Range(block.Bottom + 1, block.Height - 2).Select(y => new Vector2i(block.Left, y)).ToList(), new Vector2i(-1, 0)),
            (Enumerable.Range(block.Bottom + 1, block.Height - 2).Select(y => new Vector2i(block.Right - 1, y)).ToList(), new Vector2i(1, 0)),
        };

        var opened = 0;
        foreach (var (cells, outward) in sides.OrderBy(_ => random.Next()))
        {
            var options = cells.Where(c => plan.Get(c.X + outward.X, c.Y + outward.Y) == ForgeCell.Street &&
                                           plan.Get(c.X - outward.X, c.Y - outward.Y) == ForgeCell.Floor &&
                                           c.X + outward.X is > 0 and < ForgeWidth - 1 &&
                                           c.Y + outward.Y is > 0 and < ForgeHeight - 1)
                .ToList();

            if (options.Count == 0)
                continue;

            if (opened > 0 && random.NextDouble() < 0.35)
                continue;

            var door = options[random.Next(options.Count)];
            plan.Set(door.X, door.Y, ForgeCell.Door);
            var along = new Vector2i(Math.Abs(outward.Y), Math.Abs(outward.X));
            if (cells.Count > 14 && random.NextDouble() < 0.5 && options.Contains(door + along))
                plan.Set(door.X + along.X, door.Y + along.Y, ForgeCell.Door);
            opened++;

            foreach (var cell in options)
            {
                if (plan.Get(cell.X, cell.Y) != ForgeCell.Wall || random.NextDouble() > 0.12)
                    continue;

                if (plan.Get(cell.X + along.X, cell.Y + along.Y) == ForgeCell.Door || plan.Get(cell.X - along.X, cell.Y - along.Y) == ForgeCell.Door)
                    continue;

                plan.Set(cell.X, cell.Y, ForgeCell.Window);
            }
        }
    }

    private static void AssignRooms(ForgePlan plan, SurveyWorldPrototype world, System.Random random)
    {
        var order = plan.Rooms.OrderByDescending(r => r.Area).ToList();
        var free = new List<ForgeRoom>(order);

        var core = order.FirstOrDefault(r => r.Block == plan.CoreBlock) ?? order[0];
        core.Kind = "core";
        free.Remove(core);

        void Take(string kind, int count, Func<ForgeRoom, bool>? fit = null)
        {
            for (var i = 0; i < count; i++)
            {
                var room = free.FirstOrDefault(r => fit?.Invoke(r) ?? true) ?? free.FirstOrDefault();
                if (room == null)
                    return;

                room.Kind = kind;
                free.Remove(room);
            }
        }

        Take("power", 1, r => r.Box.Width >= 7 && r.Box.Height >= 7 && r.Block != plan.CoreBlock);
        Take("assembly", Math.Max(4, world.StampCount), r => r.Box.Width >= 7 || r.Box.Height >= 7);
        Take("comms", 1, r => r.Block != plan.CoreBlock);
        Take("server", 1, r => r.Block == plan.CoreBlock);
        Take("repair", 2);
        Take("biomass", 1, r => r.Box.Width >= 6 && r.Box.Height >= 6);

        var total = FillerRooms.Sum(f => f.Weight);
        foreach (var room in free.OrderBy(_ => random.Next()))
        {
            var roll = random.Next(total);
            foreach (var (kind, weight) in FillerRooms)
            {
                roll -= weight;
                if (roll >= 0)
                    continue;

                room.Kind = kind;
                break;
            }
        }
    }

    private (Vector2 Landing, Vector2 Dock) BuildApproach(ForgePlan plan, System.Random random)
    {
        var street = plan.MainStreet >= 0 ? plan.MainStreet + 1 : ForgeWidth / 2;
        var half = PadSize / 2;
        var padX = Math.Clamp(street, half, ForgeWidth - half - 1);
        var padTop = -PadGap - 1;
        var padBottom = -PadGap - PadSize;

        for (var x = padX - half; x <= padX + half; x++)
        {
            for (var y = padBottom; y <= padTop; y++)
            {
                plan.Set(x, y, ForgeCell.Pad);
            }
        }

        for (var x = street - 1; x <= street + 1; x++)
        {
            for (var y = padTop + 1; y < 0; y++)
            {
                plan.Set(x, y, ForgeCell.Bridge);
            }
        }

        var middleRow = padBottom + PadSize / 2;
        foreach (var side in new[] { -1, 1 })
        {
            var gate = -1;
            for (var reach = 14; reach <= 38 && gate < 0; reach++)
            {
                var x = padX + side * reach;
                if (x < 2 || x > ForgeWidth - 3)
                    break;

                var fits = true;
                for (var dx = -1; dx <= 1 && fits; dx++)
                {
                    fits = plan.Get(x + dx, 0) == ForgeCell.Wall && plan.Get(x + dx, 1) == ForgeCell.Floor;
                }

                if (fits && random.NextDouble() < 0.6)
                    gate = x;
            }

            if (gate < 0)
                continue;

            var from = side < 0 ? padX - half - 1 : padX + half + 1;
            for (var x = Math.Min(from, gate - 1); x <= Math.Max(from, gate + 1); x++)
            {
                for (var y = middleRow - 1; y <= middleRow + 1; y++)
                {
                    if (plan.Get(x, y) == ForgeCell.Void)
                        plan.Set(x, y, ForgeCell.Bridge);
                }
            }

            for (var x = gate - 1; x <= gate + 1; x++)
            {
                for (var y = middleRow - 1; y < 0; y++)
                {
                    if (plan.Get(x, y) == ForgeCell.Void)
                        plan.Set(x, y, ForgeCell.Bridge);
                }

                plan.Set(x, 0, ForgeCell.Door);
            }
        }

        var dockHalf = DockSize / 2;
        var dockX = Math.Clamp(street, dockHalf, ForgeWidth - dockHalf - 1);
        var dockBottom = ForgeHeight + PadGap;
        for (var x = dockX - dockHalf; x <= dockX + dockHalf; x++)
        {
            for (var y = dockBottom; y < dockBottom + DockSize; y++)
            {
                plan.Set(x, y, ForgeCell.Pad);
            }
        }

        for (var x = street - 1; x <= street + 1; x++)
        {
            for (var y = ForgeHeight; y < dockBottom; y++)
            {
                plan.Set(x, y, ForgeCell.Bridge);
            }
        }

        return (new Vector2(padX + 0.5f, padBottom + PadSize / 2f), new Vector2(dockX + 0.5f, dockBottom + DockSize / 2f));
    }

    private static void CapStreets(ForgePlan plan)
    {
        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = 0; y < ForgeHeight; y++)
            {
                if (plan.Get(x, y) != ForgeCell.Street)
                    continue;

                if (x != 0 && y != 0 && x != ForgeWidth - 1 && y != ForgeHeight - 1)
                    continue;

                var bridged = plan.Get(x, y - 1) == ForgeCell.Bridge || plan.Get(x, y + 1) == ForgeCell.Bridge ||
                              plan.Get(x - 1, y) == ForgeCell.Bridge || plan.Get(x + 1, y) == ForgeCell.Bridge;
                if (!bridged)
                    plan.Set(x, y, ForgeCell.Wall);
            }
        }
    }

    private static void CollectRailings(ForgePlan plan)
    {
        var sides = new (Vector2i Dir, Angle Rotation)[]
        {
            (new Vector2i(0, -1), Angle.Zero),
            (new Vector2i(1, 0), new Angle(MathF.PI / 2f)),
            (new Vector2i(0, 1), new Angle(MathF.PI)),
            (new Vector2i(-1, 0), new Angle(-MathF.PI / 2f)),
        };

        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = -plan.Offset; y < plan.Height - plan.Offset; y++)
            {
                var cell = plan.Get(x, y);
                if (cell != ForgeCell.Pad && cell != ForgeCell.Bridge)
                    continue;

                foreach (var (dir, rotation) in sides)
                {
                    if (plan.Get(x + dir.X, y + dir.Y) == ForgeCell.Void)
                        plan.Railings.Add((new Vector2i(x, y), rotation));
                }
            }
        }
    }

    private void LayForge(Entity<SurveyDirectorComponent> ent, ForgePlan plan, Vector2i origin, EntityUid map, MapGridComponent grid, SurveyWorldPrototype world, List<SurveySpot> spots, System.Random random)
    {
        var tiles = new List<(Vector2i, Tile)>();
        var street = new Tile(_tileDefs[ForgeStreet].TileId);
        var pad = new Tile(_tileDefs[ForgePad].TileId);
        var bridge = new Tile(_tileDefs[ForgeBridge].TileId);
        var floors = plan.Rooms.Select(r => new Tile(_tileDefs[RoomFloors.GetValueOrDefault(r.Kind, ForgeStreet)].TileId)).ToList();

        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = -plan.Offset; y < plan.Height - plan.Offset; y++)
            {
                var cell = plan.Get(x, y);
                if (cell == ForgeCell.Void)
                    continue;

                var room = plan.RoomAt(x, y);
                var tile = cell switch
                {
                    ForgeCell.Pad => pad,
                    ForgeCell.Bridge => bridge,
                    ForgeCell.Floor when room >= 0 => floors[room],
                    _ => street,
                };
                tiles.Add((origin + new Vector2i(x, y), tile));
            }
        }

        _map.SetTiles(map, grid, tiles);

        EntityCoordinates At(int x, int y) => new(map, origin + new Vector2(x + 0.5f, y + 0.5f));

        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = -plan.Offset; y < plan.Height - plan.Offset; y++)
            {
                switch (plan.Get(x, y))
                {
                    case ForgeCell.Wall:
                        SpawnAtPosition(WallFor(plan, x, y), At(x, y));
                        break;
                    case ForgeCell.Door:
                        SpawnAtPosition(ForgeDoor, At(x, y));
                        break;
                    case ForgeCell.Window:
                        SpawnAtPosition(ForgeGrille, At(x, y));
                        SpawnAtPosition(ForgeWindow, At(x, y));
                        break;
                    case ForgeCell.Bridge:
                        SpawnAtPosition(ForgeCatwalk, At(x, y));
                        break;
                }
            }
        }

        foreach (var (cell, rotation) in plan.Railings)
        {
            var rail = SpawnAtPosition(ForgeRailing, At(cell.X, cell.Y));
            _xform.SetLocalRotation(rail, rotation);
        }

        LightApproach(ent, plan, At);
        DressStreets(ent, plan, At, world, random);

        var taken = new HashSet<Vector2i>();
        for (var i = 0; i < plan.Rooms.Count; i++)
        {
            var room = plan.Rooms[i];
            FurnishRoom(ent, plan, room, At, taken, random);

            var cells = new List<Vector2i>();
            for (var x = room.Box.Left; x < room.Box.Right; x++)
            {
                for (var y = room.Box.Bottom; y < room.Box.Top; y++)
                {
                    cells.Add(origin + new Vector2i(x, y));
                }
            }

            var centre = origin + room.Centre;
            spots.Add(new SurveySpot(centre, cells) { Tag = room.Kind });
            ent.Comp.Anchors.Add(centre);

            switch (room.Kind)
            {
                case "core":
                case "power":
                case "comms":
                    AddPoi(ent, centre, SurveyPoiKind.Device);
                    break;
                case "assembly":
                    AddPoi(ent, centre, SurveyPoiKind.Stamp);
                    break;
            }
        }
    }

    private static string WallFor(ForgePlan plan, int x, int y)
    {
        for (var b = 0; b < plan.Blocks.Count; b++)
        {
            var block = plan.Blocks[b];
            if (x >= block.Left && x < block.Right && y >= block.Bottom && y < block.Top)
                return plan.BlockWalls[b];
        }

        return ForgeWalls[0];
    }

    private void LightApproach(Entity<SurveyDirectorComponent> ent, ForgePlan plan, Func<int, int, EntityCoordinates> at)
    {
        for (var x = 0; x < ForgeWidth; x++)
        {
            for (var y = -plan.Offset; y < plan.Height - plan.Offset; y++)
            {
                var cell = plan.Get(x, y);
                var lit = cell switch
                {
                    ForgeCell.Pad => (x + y) % 7 == 0 && plan.Railings.Any(r => r.Cell == new Vector2i(x, y)),
                    ForgeCell.Bridge => y % 6 == 0 && x % 3 == 0,
                    _ => false,
                };

                if (lit)
                    ent.Comp.Lamps.Add(SpawnAtPosition(ForgeLamp, at(x, y)));

                if (cell != ForgeCell.Street || (x * 3 + y * 7) % 10 != 0)
                    continue;

                foreach (var (dir, facing) in WallSides)
                {
                    if (plan.Get(x + dir.X, y + dir.Y) != ForgeCell.Wall)
                        continue;

                    var lamp = SpawnAtPosition(ForgeWallLight, at(x, y));
                    _xform.SetLocalRotation(lamp, facing);
                    ent.Comp.Lamps.Add(lamp);
                    break;
                }
            }
        }
    }

    private void DressStreets(Entity<SurveyDirectorComponent> ent, ForgePlan plan, Func<int, int, EntityCoordinates> at, SurveyWorldPrototype world, System.Random random)
    {
        var lanes = new List<Vector2i>();
        for (var x = 1; x < ForgeWidth - 1; x++)
        {
            for (var y = 1; y < ForgeHeight - 1; y++)
            {
                if (plan.Get(x, y) != ForgeCell.Street)
                    continue;

                if (x % 5 == 0 && y % 5 == 0)
                    ent.Comp.Anchors.Add(at(x, y).Position);

                var nearDoor = false;
                var againstWall = false;
                for (var dx = -2; dx <= 2 && !nearDoor; dx++)
                {
                    for (var dy = -2; dy <= 2; dy++)
                    {
                        var other = plan.Get(x + dx, y + dy);
                        if (other is ForgeCell.Door or ForgeCell.Bridge)
                        {
                            nearDoor = true;
                            break;
                        }

                        if (Math.Abs(dx) + Math.Abs(dy) == 1 && other == ForgeCell.Wall)
                            againstWall = true;
                    }
                }

                if (nearDoor)
                    continue;

                lanes.Add(new Vector2i(x, y));
                if (againstWall && random.NextDouble() < 0.09)
                    SpawnAtPosition(StreetJunk[random.Next(StreetJunk.Length)], at(x, y));
            }
        }

        for (var i = 0; i < world.TrapCount && world.Traps.Count > 0 && lanes.Count > 0; i++)
        {
            var cell = lanes[random.Next(lanes.Count)];
            EnsureComp<SurveyNativeMineComponent>(SpawnAtPosition(world.Traps[random.Next(world.Traps.Count)], at(cell.X, cell.Y)));
        }
    }

    private static readonly (Vector2i Dir, Angle Rotation)[] WallSides =
    {
        (new Vector2i(0, 1), Angle.Zero),
        (new Vector2i(0, -1), new Angle(MathF.PI)),
        (new Vector2i(1, 0), new Angle(-MathF.PI / 2f)),
        (new Vector2i(-1, 0), new Angle(MathF.PI / 2f)),
    };

    private static readonly string[] TableLoot = { "Wrench", "Welder", "Screwdriver", "Crowbar", "SheetSteel10", "PartRodMetal10", "PowerCellMedium", "CyborgEndoskeleton", "LeftArmBorg" };
    private static readonly string[] RackLoot = { "SheetSteel10", "PartRodMetal10", "PowerCellMedium", "SteelOre1" };

    private static readonly Dictionary<string, (string[] Wall, float Density, string? Sign)> RoomWalls = new()
    {
        ["core"] = (new[] { "SurveyFactoryServer", "SurveyFactoryServer", "SurveyFactoryRelay", "SurveyFactoryConsole" }, 0.85f, "SignSecureMed"),
        ["power"] = (new[] { "SurveyFactorySmes", "SurveyFactorySmes", "SurveyFactoryPowerConsole", "SurveyFactoryCondenser" }, 0.8f, "SignElectricalMed"),
        ["comms"] = (new[] { "SurveyFactoryRelay", "SurveyFactoryRelay", "SurveyFactoryConsole", "SurveyFactoryServer" }, 0.8f, "SignSecureMed"),
        ["repair"] = (new[] { "TableReinforced", "TableReinforced", "Rack", "SurveyLockerNative", "SurveyFactorySmes", "SurveyFactoryConsole", "WeldingFuelTankFull" }, 0.75f, null),
        ["assembly"] = (new[] { "SurveyFactoryAssembler", "SurveyFactoryPress", "Rack", "SurveyFactoryConsole", "TableReinforced" }, 0.6f, null),
        ["foundry"] = (new[] { "SurveyFactoryFurnace", "SurveyFactoryFurnace", "SurveyFactoryFurnace", "SurveyFactorySilo", "WeldingFuelTankFull" }, 0.8f, "SignDanger"),
        ["press"] = (new[] { "SurveyFactoryPress", "SurveyFactorySheeter", "Rack", "TableReinforced" }, 0.75f, null),
        ["biomass"] = (new[] { "SurveyFactoryCrusher", "SurveyFactoryCrusher", "SurveyFactoryVat", "WaterTankFull" }, 0.75f, "SignDanger"),
        ["server"] = (new[] { "SurveyFactoryServer", "SurveyFactoryServer", "SurveyFactoryServer", "SurveyFactoryConsole" }, 0.9f, "SignSecureMed"),
        ["charging"] = (new[] { "SurveyFactorySmes", "SurveyFactoryConsole", "Rack", "SurveyLockerNative" }, 0.6f, null),
        ["silo"] = (new[] { "SurveyFactorySilo", "SurveyFactorySilo", "SurveyFactoryCondenser" }, 0.7f, null),
        ["storage"] = (new[] { "SurveyCrateNative", "SurveyCrateLoot", "SurveyCrateLoot", "Rack", "Rack", "SurveyLockerNative" }, 0.9f, null),
    };

    private void FurnishRoom(Entity<SurveyDirectorComponent> ent, ForgePlan plan, ForgeRoom room, Func<int, int, EntityCoordinates> at, HashSet<Vector2i> taken, System.Random random)
    {
        var box = room.Box;
        var doors = new List<Vector2i>();
        for (var x = box.Left - 1; x <= box.Right; x++)
        {
            for (var y = box.Bottom - 1; y <= box.Top; y++)
            {
                if (plan.Get(x, y) == ForgeCell.Door)
                    doors.Add(new Vector2i(x, y));
            }
        }

        bool NearDoor(Vector2i cell) => doors.Any(d => Math.Abs(d.X - cell.X) <= 1 && Math.Abs(d.Y - cell.Y) <= 1);

        var slots = new List<(Vector2i Cell, Angle Facing)>();
        var lit = new HashSet<Vector2i>();
        foreach (var (dir, facing) in WallSides)
        {
            var side = new List<Vector2i>();
            for (var x = box.Left; x < box.Right; x++)
            {
                for (var y = box.Bottom; y < box.Top; y++)
                {
                    var cell = new Vector2i(x, y);
                    var wall = plan.Get(x + dir.X, y + dir.Y);
                    if (wall is ForgeCell.Wall or ForgeCell.Window && !(x + dir.X >= box.Left && x + dir.X < box.Right && y + dir.Y >= box.Bottom && y + dir.Y < box.Top))
                        side.Add(cell);
                }
            }

            for (var i = 0; i < side.Count; i++)
            {
                var cell = side[i];
                if (i % 5 == 2 && plan.Get(cell.X + dir.X, cell.Y + dir.Y) == ForgeCell.Wall && lit.Add(cell))
                {
                    var lamp = SpawnAtPosition(ForgeWallLight, at(cell.X, cell.Y));
                    _xform.SetLocalRotation(lamp, facing);
                    ent.Comp.Lamps.Add(lamp);
                }

                if (!NearDoor(cell) && slots.All(s => s.Cell != cell))
                    slots.Add((cell, facing));
            }
        }

        EntityUid? Put(string proto, Vector2i cell, Angle facing)
        {
            if (!taken.Add(cell))
                return null;

            var uid = SpawnAtPosition(proto, at(cell.X, cell.Y));
            if (proto.Contains("Console"))
                _xform.SetLocalRotation(uid, facing);

            if (proto.StartsWith("Table") && random.NextDouble() < 0.6)
                SpawnAtPosition(TableLoot[random.Next(TableLoot.Length)], at(cell.X, cell.Y));
            else if (proto == "Rack" && random.NextDouble() < 0.5)
                SpawnAtPosition(RackLoot[random.Next(RackLoot.Length)], at(cell.X, cell.Y));

            return uid;
        }

        var palette = RoomWalls.GetValueOrDefault(room.Kind, RoomWalls["storage"]);
        var inner = new Box2i(box.Left + 2, box.Bottom + 2, box.Right - 2, box.Top - 2);
        var cx = box.Left + box.Width / 2;
        var cy = box.Bottom + box.Height / 2;
        var middle = new Vector2i(cx, cy);

        var forced = new Queue<string>();
        switch (room.Kind)
        {
            case "repair":
                forced.Enqueue(RepairBay);
                forced.Enqueue(RepairBay);
                break;
            case "power":
                forced.Enqueue("SurveyPowerNode");
                break;
            case "comms":
                forced.Enqueue("SurveyCommsMast");
                break;
        }

        foreach (var (cell, facing) in slots.OrderBy(_ => random.Next()))
        {
            string proto;
            if (forced.Count > 0)
                proto = forced.Dequeue();
            else if (random.NextDouble() < palette.Density)
                proto = palette.Wall[random.Next(palette.Wall.Length)];
            else
                continue;

            if (Put(proto, cell, facing) is not { } placed)
                continue;

            if (proto == RepairBay)
                ent.Comp.Autodocs.Add(placed);
            else if (proto == "SurveyPowerNode")
                ent.Comp.Outposts.Add(new SurveyOutpost { Pos = at(cell.X, cell.Y).Position, Role = SurveyDeviceRole.Power });
            else if (proto == "SurveyCommsMast")
            {
                ent.Comp.Outposts.Add(new SurveyOutpost { Pos = at(cell.X, cell.Y).Position, Role = SurveyDeviceRole.Comms });
                SpawnAtPosition(RadioKey, at(middle.X, middle.Y));
            }
        }

        if (palette.Sign is { } sign)
        {
            foreach (var door in doors)
            {
                foreach (var along in new[] { new Vector2i(1, 0), new Vector2i(-1, 0), new Vector2i(0, 1), new Vector2i(0, -1) })
                {
                    var wall = door + along;
                    if (plan.Get(wall.X, wall.Y) != ForgeCell.Wall || !taken.Add(wall))
                        continue;

                    SpawnAtPosition(sign, at(wall.X, wall.Y));
                    break;
                }
            }
        }

        if (inner.Width < 1 || inner.Height < 1)
            return;

        void Island(string proto, int x, int y)
        {
            if (x >= inner.Left && x < inner.Right && y >= inner.Bottom && y < inner.Top)
                Put(proto, new Vector2i(x, y), Angle.Zero);
        }

        void Belt(int x, int y, bool along)
        {
            var cell = new Vector2i(x, y);
            if (x < inner.Left || x >= inner.Right || y < inner.Bottom || y >= inner.Top || !taken.Add(cell))
                return;

            var belt = SpawnAtPosition(Conveyor, at(x, y));
            _xform.SetLocalRotation(belt, along ? Angle.Zero : new Angle(MathF.PI / 2f));
        }

        var wide = inner.Width >= inner.Height;
        switch (room.Kind)
        {
            case "core":
                Put(CoreProto, middle, Angle.Zero);
                Island("SurveyFactoryRelay", cx - 3, cy - 3);
                Island("SurveyFactoryRelay", cx + 3, cy - 3);
                Island("SurveyFactoryRelay", cx - 3, cy + 3);
                Island("SurveyFactoryRelay", cx + 3, cy + 3);
                break;

            case "power":
                Island("SurveyFactoryReactor", cx, cy);
                Island("SurveyFactorySmes", cx - 2, cy);
                Island("SurveyFactorySmes", cx + 2, cy);
                break;

            case "server":
                for (var x = inner.Left; x < inner.Right; x += 2)
                {
                    for (var y = inner.Bottom; y < inner.Top; y++)
                    {
                        if (y != cy)
                            Island("SurveyFactoryServer", x, y);
                    }
                }
                break;

            case "assembly":
            case "biomass":
            case "foundry":
                for (var line = wide ? inner.Bottom : inner.Left; line < (wide ? inner.Top : inner.Right); line += 3)
                {
                    var start = wide ? inner.Left : inner.Bottom;
                    var end = wide ? inner.Right : inner.Top;
                    for (var step = start; step < end; step++)
                    {
                        var (x, y) = wide ? (step, line) : (line, step);
                        if (room.Kind == "assembly" && (step - start) % 3 == 1 && random.NextDouble() < 0.7)
                            Island(Shells[random.Next(Shells.Length)], x, y);
                        else if (room.Kind == "biomass" && step == end - 1)
                            Island("SurveyFactoryCrusher", x, y);
                        else if (room.Kind == "foundry" && step == end - 1)
                            Island("SurveyFactorySilo", x, y);
                        else
                            Belt(x, y, wide);
                    }
                }
                break;

            case "press":
                for (var x = inner.Left; x < inner.Right; x += 3)
                {
                    for (var y = inner.Bottom; y < inner.Top; y += 2)
                    {
                        Island("TableReinforced", x, y);
                        Island("TableReinforced", x + 1, y);
                    }
                }
                break;

            case "repair":
            case "charging":
                for (var x = inner.Left; x < inner.Right; x += 2)
                {
                    for (var y = inner.Bottom; y < inner.Top; y += 2)
                    {
                        Island(random.NextDouble() < 0.3 ? "TableReinforced" : Shells[random.Next(Shells.Length)], x, y);
                    }
                }
                break;

            case "silo":
                for (var x = inner.Left; x < inner.Right; x += 3)
                {
                    for (var y = inner.Bottom; y < inner.Top; y += 3)
                    {
                        Island("SurveyFactorySilo", x, y);
                        Belt(x + 1, y, true);
                    }
                }
                break;

            case "comms":
                Island("SurveyFactoryServer", cx, cy);
                Island("SurveyFactoryServer", cx + 1, cy);
                break;

            default:
                for (var x = inner.Left; x < inner.Right; x += 3)
                {
                    for (var y = inner.Bottom; y < inner.Top; y += 2)
                    {
                        Island("SurveyCrateLoot", x, y);
                        if (random.NextDouble() < 0.6)
                            Island("SurveyCrateLoot", x + 1, y);
                    }
                }
                break;
        }
    }
}
