using Protocol;

namespace Client.Tui;

public static class TuiScreen
{
    public static void Render(TuiModel model)
    {
        Console.Clear();
        Console.WriteLine("=== Draw ===");
        foreach (string line in model.DrawLines)
            Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine("=== Coords ===");
        foreach (string line in model.CoordLines)
            Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine("=== Status ===");
        Console.WriteLine($"Generation: {model.Generation}");
        Console.WriteLine(model.StatusText);

        Console.WriteLine();
        Console.WriteLine("=== Input ===");
        Console.Write($"> {model.InputBuffer}_");
    }

    public static string[] BuildDrawLines(IReadOnlyList<(long X, long Y)> cells) =>
        BuildGridLines(
            cells,
            GameConstants.InitialStateWidth,
            GameConstants.InitialStateHeight,
            GameConstants.InitialAreaOriginX,
            GameConstants.InitialAreaOriginY);

    public static string[] BuildGridLines(
        IReadOnlyList<(long X, long Y)> cells,
        int width,
        int height,
        long originX = 0,
        long originY = 0)
    {
        var alive = cells.ToHashSet();
        string[] lines = new string[height];

        for (int row = 0; row < height; row++)
        {
            char[] chars = new char[width];
            for (int col = 0; col < width; col++)
            {
                long x = originX + col;
                long y = originY + row;
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
