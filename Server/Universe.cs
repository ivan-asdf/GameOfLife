namespace Server;

/// <summary>
/// Game universe state. Initial editing is limited to a 100 x 100 area at the origin.
/// </summary>
public sealed class Universe
{
    public const int InitialStateWidth = 100;
    public const int InitialStateHeight = 100;

    private readonly HashSet<(int X, int Y)> _alive = new();
    private readonly object _lock = new();

    public bool Toggle(int x, int y)
    {
        Validate(x, y);
        lock (_lock)
        {
            var coord = (x, y);
            if (!_alive.Add(coord))
                _alive.Remove(coord);
            return _alive.Contains(coord);
        }
    }

    public bool Set(int x, int y, bool alive)
    {
        Validate(x, y);
        lock (_lock)
        {
            if (alive)
                _alive.Add((x, y));
            else
                _alive.Remove((x, y));
            return alive;
        }
    }

    public void Clear()
    {
        lock (_lock)
            _alive.Clear();
    }

    public (int X, int Y)[] Snapshot()
    {
        lock (_lock)
            return _alive.ToArray();
    }

    public string FormatCells()
    {
        lock (_lock)
            return string.Join(';', _alive.Select(c => $"{c.X},{c.Y}"));
    }

    private static void Validate(int x, int y)
    {
        if (x < 0 || x >= InitialStateWidth)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be 0..{InitialStateWidth - 1}.");
        if (y < 0 || y >= InitialStateHeight)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be 0..{InitialStateHeight - 1}.");
    }
}
