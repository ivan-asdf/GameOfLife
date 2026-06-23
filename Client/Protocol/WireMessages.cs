namespace Client.Protocol;

public abstract record ServerMessage
{
    public static ServerMessage? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var parts = line.Split('|');

        return parts[0].ToUpperInvariant() switch
        {
            "STATE" => ParseState(parts),
            "RESULT" => ParseResult(parts),
            _ => null
        };
    }

    private static StateMessage ParseState(string[] parts)
    {
        var cells = new List<(long X, long Y)>();

        for (var i = 1; i + 1 < parts.Length; i += 2)
        {
            if (!parts[i].Equals("cells", StringComparison.OrdinalIgnoreCase))
                continue;

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

        return new StateMessage(cells);
    }

    private static ResultMessage ParseResult(string[] parts)
    {
        if (parts.Length < 2)
            return new ResultMessage("");

        var kind = parts[1];
        var description = parts.Length > 2 ? string.Join('|', parts[2..]) : "";

        return string.IsNullOrEmpty(description)
            ? new ResultMessage($"{kind}:")
            : new ResultMessage($"{kind}: {description}");
    }
}

public sealed record StateMessage(IReadOnlyList<(long X, long Y)> Cells) : ServerMessage;

public sealed record ResultMessage(string Text) : ServerMessage;
