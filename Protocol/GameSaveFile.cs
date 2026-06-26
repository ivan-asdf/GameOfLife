using System.Text;
using System.Text.RegularExpressions;

namespace Protocol;

public sealed record GameSaveData(long Generation, IReadOnlyList<(long X, long Y)> Cells);

public static partial class GameSaveFile
{
    public const string FormatHeader = "# GameOfLife v1";

    [GeneratedRegex("^[a-zA-Z0-9_-]+$")]
    private static partial Regex ValidNamePattern();

    public static bool IsValidName(string name) =>
        !string.IsNullOrWhiteSpace(name) && ValidNamePattern().IsMatch(name);

    public static string FormatUsageError(string verb) =>
        ServerMessage.FormatResultError($"usage \"{verb} name\" (name: letters, digits, _ or -)");

    public static string FormatInvalidNameError(string name) =>
        ServerMessage.FormatResultError($"invalid save name \"{name}\"");

    public static string Write(GameSaveData data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine(FormatHeader);
        sb.AppendLine($"gen {data.Generation}");

        foreach ((long x, long y) in data.Cells.OrderBy(c => c.Y).ThenBy(c => c.X))
            sb.AppendLine($"{x},{y}");

        return sb.ToString();
    }

    public static GameSaveData Parse(string text)
    {
        long generation = 0;
        bool foundGen = false;
        List<(long X, long Y)> cells = new();

        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("gen ", StringComparison.OrdinalIgnoreCase))
            {
                string value = line[4..].Trim();
                if (!long.TryParse(value, out generation))
                    throw new FormatException($"invalid gen line: \"{rawLine}\"");

                foundGen = true;
                continue;
            }

            string[] xy = line.Split(',', 2);
            if (xy.Length != 2
                || !long.TryParse(xy[0], out long x)
                || !long.TryParse(xy[1], out long y))
            {
                throw new FormatException($"invalid cell line: \"{rawLine}\"");
            }

            cells.Add((x, y));
        }

        if (!foundGen)
            throw new FormatException("missing gen line");

        return new GameSaveData(generation, cells);
    }
}
