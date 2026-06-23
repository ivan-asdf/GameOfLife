namespace Client.Tui;

public static class TuiScreen
{
    public static void Render(TuiModel model)
    {
        Console.Clear();
        Console.WriteLine("=== Draw ===");
        foreach (var line in model.DrawLines)
            Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine("=== Coords ===");
        foreach (var line in model.CoordLines)
            Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine("=== Status ===");
        Console.WriteLine(model.StatusText);

        Console.WriteLine();
        Console.WriteLine("=== Input ===");
        Console.Write($"> {model.InputBuffer}_");
    }

    public static string[] BuildDrawLines(IReadOnlyList<(long X, long Y)> cells)
    {
        if (cells.Count == 0)
            return ["(no cells)"];

        var minX = cells.Min(c => c.X);
        var minY = cells.Min(c => c.Y);
        var maxX = cells.Max(c => c.X);
        var maxY = cells.Max(c => c.Y);
        var alive = cells.ToHashSet();

        var lines = new List<string>();
        for (var y = minY; y <= maxY; y++)
        {
            var row = new char[maxX - minX + 1];
            for (var i = 0; i < row.Length; i++)
                row[i] = alive.Contains((minX + i, y)) ? '#' : '.';
            lines.Add(new string(row));
        }

        return lines.ToArray();
    }

    public static string[] BuildCoordLines(IReadOnlyList<(long X, long Y)> cells)
    {
        if (cells.Count == 0)
            return ["(empty)"];

        return cells
            .OrderBy(c => c.Y)
            .ThenBy(c => c.X)
            .Select(c => $"({c.X}, {c.Y})")
            .ToArray();
    }
}
