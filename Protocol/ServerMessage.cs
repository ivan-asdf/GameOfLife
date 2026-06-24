namespace Protocol;

public abstract record ServerMessage
{
    public static ServerMessage? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        string[] parts = line.Split('|', StringSplitOptions.TrimEntries);

        return parts[0].ToUpperInvariant() switch
        {
            "STATE" => ParseState(parts, line),
            "RESULT" => ParseResult(parts, line),
            _ => new UnknownMessage(line)
        };
    }

    public static string FormatState(long generation, string cells) =>
        $"STATE|gen|{generation}|cells|{cells}";

    public static string FormatResultOk(string description) =>
        FormatResult("ok", description);

    public static string FormatResultError(string description) =>
        FormatResult("error", description);

    public static string FormatCellUsageError(string verb) =>
        FormatResultError(
            $"usage \"{verb} x y\" (x and y: 0..{GameConstants.InitialStateWidth - 1})");

    public static string FormatUnknownCommand(string rawLine) =>
        FormatResultError($"unknown command \"{rawLine.Trim()}\"");

    public static string FormatCellState(int x, int y, bool alive) =>
        FormatResultOk($"cell ({x},{y}) is now {(alive ? "alive" : "dead")}");

    public static string FormatInvalidCoordinates(int x, int y, string reason) =>
        FormatResultError($"invalid coordinates \"{x},{y}\" ({reason})");

    private static string FormatResult(string kind, string description) =>
        $"RESULT|{kind}|{description}";

    private static ServerMessage ParseState(string[] parts, string rawLine)
    {
        if (parts.Length < 3)
            return new BadMessage(rawLine);

        var cells = new List<(long X, long Y)>();
        bool foundCells = false;

        for (int i = 1; i + 1 < parts.Length; i += 2)
        {
            if (!parts[i].Equals("cells", StringComparison.OrdinalIgnoreCase))
                continue;

            foundCells = true;

            foreach (string pair in parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] xy = pair.Split(',', 2);
                if (xy.Length == 2
                    && long.TryParse(xy[0], out long x)
                    && long.TryParse(xy[1], out long y))
                {
                    cells.Add((x, y));
                }
            }
        }

        if (!foundCells)
            return new BadMessage(rawLine);

        return new StateMessage(cells);
    }

    private static ServerMessage ParseResult(string[] parts, string rawLine)
    {
        if (parts.Length < 3)
            return new BadMessage(rawLine);

        string kind = parts[1];
        string description = string.Join('|', parts[2..]);

        return new ResultMessage(kind, description);
    }
}

public sealed record StateMessage(IReadOnlyList<(long X, long Y)> Cells) : ServerMessage;

public sealed record ResultMessage(string Kind, string Description) : ServerMessage;

public sealed record BadMessage(string RawLine) : ServerMessage;

public sealed record UnknownMessage(string RawLine) : ServerMessage;
