namespace Client.Protocol;

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

    private static ServerMessage ParseState(string[] parts, string rawLine)
    {
        if (parts.Length < 3)
            return new BadMessage(rawLine);

        var cells = new List<(long X, long Y)>();
        var foundCells = false;

        for (var i = 1; i + 1 < parts.Length; i += 2)
        {
            if (!parts[i].Equals("cells", StringComparison.OrdinalIgnoreCase))
                continue;

            foundCells = true;

            foreach (var pair in parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var xy = pair.Split(',', 2);
                if (xy.Length == 2
                    && long.TryParse(xy[0], out var x)
                    && long.TryParse(xy[1], out var y))
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
