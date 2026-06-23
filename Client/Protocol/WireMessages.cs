namespace Client.Protocol;

public abstract record ServerMessage
{
    public static ServerMessage? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);

        return parts[0] switch
        {
            "state" => ParseState(parts.Length > 1 ? parts[1] : ""),
            "result" => new ResultMessage(parts.Length > 1 ? parts[1] : ""),
            _ => new ResultMessage(line)
        };
    }

    private static StateMessage ParseState(string payload)
    {
        var cells = new List<(long X, long Y)>();

        foreach (var token in payload.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!token.StartsWith("cells=", StringComparison.Ordinal))
                continue;

            foreach (var pair in token["cells=".Length..].Split(';', StringSplitOptions.RemoveEmptyEntries))
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
}

public sealed record StateMessage(IReadOnlyList<(long X, long Y)> Cells) : ServerMessage;

public sealed record ResultMessage(string Text) : ServerMessage;
