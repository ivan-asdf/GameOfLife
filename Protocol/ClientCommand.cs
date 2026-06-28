namespace Protocol;

public abstract record ClientCommand
{
    public static ClientCommand? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        string[] parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
            "save" => ParseNameCommand(parts, name => new SaveCommand(name)),
            "load" => ParseNameCommand(parts, name => new LoadCommand(name)),
            "list" => new ListCommand(),
            "fps" => ParseFpsCommand(parts),
            _ => new UnknownCommand(line)
        };
    }

    private static ClientCommand ParseCellCommand(
        string[] parts,
        Func<int, int, CellCommand> create)
    {
        if (parts.Length != 3
            || !int.TryParse(parts[1], out int x)
            || !int.TryParse(parts[2], out int y))
        {
            return new BadCommand(ServerMessage.FormatCellUsageError(parts[0]));
        }

        return create(x, y);
    }

    private static ClientCommand ParseNameCommand(string[] parts, Func<string, NameCommand> create)
    {
        if (parts.Length != 2)
            return new BadCommand(GameSaveFile.FormatUsageError(parts[0]));

        string name = parts[1];
        if (!GameSaveFile.IsValidName(name))
            return new BadCommand(GameSaveFile.FormatInvalidNameError(name));

        return create(name);
    }

    private static ClientCommand ParseFpsCommand(string[] parts)
    {
        if (parts.Length == 1)
            return new FpsCommand(null);

        if (parts.Length == 2
            && int.TryParse(parts[1], out int fps)
            && fps >= GameConstants.MinSimulationFps
            && fps <= GameConstants.MaxSimulationFps)
        {
            return new FpsCommand(fps);
        }

        return new BadCommand(ServerMessage.FormatFpsUsageError());
    }
}

public abstract record CellCommand(int X, int Y) : ClientCommand;

public sealed record ToggleCommand(int X, int Y) : CellCommand(X, Y);

public sealed record SetCommand(int X, int Y) : CellCommand(X, Y);

public sealed record UnsetCommand(int X, int Y) : CellCommand(X, Y);

public sealed record ClearCommand() : ClientCommand;

public sealed record StartCommand() : ClientCommand;

public sealed record StopCommand() : ClientCommand;

public abstract record NameCommand(string Name) : ClientCommand;

public sealed record SaveCommand(string Name) : NameCommand(Name);

public sealed record LoadCommand(string Name) : NameCommand(Name);

public sealed record ListCommand() : ClientCommand;

public sealed record FpsCommand(int? Fps) : ClientCommand;

public sealed record BadCommand(string ErrorMessage) : ClientCommand;

public sealed record UnknownCommand(string RawLine) : ClientCommand;
