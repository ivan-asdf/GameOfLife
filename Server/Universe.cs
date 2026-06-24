using Protocol;

namespace Server;

/// <summary>
/// Game universe state. Initial editing is limited to a 100 x 100 area at the origin.
/// </summary>
public sealed class Universe
{
    private readonly HashSet<(int X, int Y)> _alive = new();
    private readonly object _lock = new();

    public bool Toggle(int x, int y)
    {
        Validate(x, y);
        lock (_lock)
        {
            (int X, int Y) coord = (x, y);
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
        if (x < 0 || x >= GameConstants.InitialStateWidth)
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X must be 0..{GameConstants.InitialStateWidth - 1}.");
        if (y < 0 || y >= GameConstants.InitialStateHeight)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y must be 0..{GameConstants.InitialStateHeight - 1}.");
    }
}
