using Protocol;

namespace Client.Tui;

public static class TuiScreen
{
    public static string[] BuildDrawLines(IReadOnlyList<(long X, long Y)> cells) =>
        CompressRows(BuildGridLines(
            cells,
            GameConstants.InitialStateWidth,
            GameConstants.InitialStateHeight,
            GameConstants.InitialAreaOriginX,
            GameConstants.InitialAreaOriginY));

    /// <summary>
    /// Packs two grid rows into one terminal row using half-block characters,
    /// so a 100×100 grid fits in 50 lines.
    /// </summary>
    private static string[] CompressRows(string[] rows)
    {
        int outHeight = rows.Length / 2;
        string[] lines = new string[outHeight];

        for (int i = 0; i < outHeight; i++)
        {
            ReadOnlySpan<char> top = rows[i * 2];
            ReadOnlySpan<char> bottom = rows[i * 2 + 1];
            char[] chars = new char[top.Length];

            for (int col = 0; col < top.Length; col++)
            {
                bool topAlive = top[col] == '#';
                bool bottomAlive = bottom[col] == '#';
                chars[col] = (topAlive, bottomAlive) switch
                {
                    (true, true) => '█',
                    (true, false) => '▀',
                    (false, true) => '▄',
                    _ => ' ',
                };
            }

            lines[i] = new string(chars);
        }

        return lines;
    }

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
        string[] lines;
        if (cells.Count == 0)
            lines = ["(empty)"];
        else
        {
            lines = cells
                .OrderBy(c => c.Y)
                .ThenBy(c => c.X)
                .Select(c => $"({c.X}, {c.Y})")
                .ToArray();
        }

        return lines;
    }
}
