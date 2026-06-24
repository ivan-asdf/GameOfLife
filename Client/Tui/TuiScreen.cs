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

    public const int GridWidth = 100;
    public const int GridHeight = 100;

    public static string[] BuildDrawLines(IReadOnlyList<(long X, long Y)> cells) =>
        BuildGridLines(cells, GridWidth, GridHeight);

    public static string[] BuildGridLines(
        IReadOnlyList<(long X, long Y)> cells,
        int width,
        int height,
        long originX = 0,
        long originY = 0)
    {
        var alive = cells.ToHashSet();
        var lines = new string[height];

        for (var row = 0; row < height; row++)
        {
            var chars = new char[width];
            for (var col = 0; col < width; col++)
            {
                var x = originX + col;
                var y = originY + row;
                chars[col] = alive.Contains((x, y)) ? '#' : '.';
            }

            lines[row] = new string(chars);
        }

        return lines;
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
