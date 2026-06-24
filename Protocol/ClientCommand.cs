namespace Protocol;

public abstract record ClientCommand
{
    public static ClientCommand? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        return parts[0].ToLowerInvariant() switch
        {
            "toggle" => ParseCellCommand(parts, (x, y) => new ToggleCommand(x, y)),
            "set" => ParseCellCommand(parts, (x, y) => new SetCommand(x, y)),
            "unset" => ParseCellCommand(parts, (x, y) => new UnsetCommand(x, y)),
            "clear" => new ClearCommand(),
            "start" => new StartCommand(),
            "stop" => new StopCommand(),
            _ => new UnknownCommand(line)
        };
    }

    private static ClientCommand ParseCellCommand(
        string[] parts,
        Func<int, int, CellCommand> create)
    {
        if (parts.Length != 3
            || !int.TryParse(parts[1], out var x)
            || !int.TryParse(parts[2], out var y))
        {
            return new BadCommand(ServerMessage.FormatCellUsageError(parts[0]));
        }

        return create(x, y);
    }
}

public abstract record CellCommand(int X, int Y) : ClientCommand;

public sealed record ToggleCommand(int X, int Y) : CellCommand(X, Y);

public sealed record SetCommand(int X, int Y) : CellCommand(X, Y);

public sealed record UnsetCommand(int X, int Y) : CellCommand(X, Y);

public sealed record ClearCommand() : ClientCommand;

public sealed record StartCommand() : ClientCommand;

public sealed record StopCommand() : ClientCommand;

public sealed record BadCommand(string ErrorMessage) : ClientCommand;

public sealed record UnknownCommand(string RawLine) : ClientCommand;
