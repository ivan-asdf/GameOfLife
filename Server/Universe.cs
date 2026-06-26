using Protocol;

namespace Server;

/// <summary>
/// Sparse Game of Life universe on a 2^64 × 2^64 torus.
/// Initial editing is limited to a 100 × 100 area centered on the origin.
/// </summary>
public sealed class Universe
{
    private HashSet<(long X, long Y)> _alive = new();
    private readonly object _lock = new();

    public long Generation { get; private set; }

    public bool ToggleCell(int localX, int localY)
    {
        ValidateLocal(localX, localY);
        (long X, long Y) coord = LocalToUniverseCoord(localX, localY);
        lock (_lock)
        {
            if (!_alive.Add(coord))
                _alive.Remove(coord);
            return _alive.Contains(coord);
        }
    }

    public bool SetCell(int localX, int localY, bool alive)
    {
        ValidateLocal(localX, localY);
        (long X, long Y) coord = LocalToUniverseCoord(localX, localY);
        lock (_lock)
        {
            if (alive)
                _alive.Add(coord);
            else
                _alive.Remove(coord);
            return alive;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _alive.Clear();
            Generation = 0;
        }
    }

    public void Step()
    {
        lock (_lock)
        {
            var candidates = new HashSet<(long X, long Y)>();
            foreach ((long x, long y) in _alive)
                CollectNeighborhood(candidates, x, y);

            var next = new HashSet<(long X, long Y)>();
            foreach ((long x, long y) in candidates)
            {
                int neighbors = CountNeighbors(x, y);
                bool alive = _alive.Contains((x, y));

                if (alive && (neighbors == 2 || neighbors == 3))
                    next.Add((x, y));
                else if (!alive && neighbors == 3)
                    next.Add((x, y));
            }

            _alive = next;
            Generation++;
        }
    }

    public string FormatState()
    {
        lock (_lock)
            return ServerMessage.FormatState(Generation, FormatCells());
    }

    private string FormatCells() =>
        string.Join(';', _alive.Select(c => $"{c.X},{c.Y}"));

    private int CountNeighbors(long x, long y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (_alive.Contains((Offset(x, dx), Offset(y, dy))))
                    count++;
            }
        }

        return count;
    }

    private static void CollectNeighborhood(HashSet<(long X, long Y)> candidates, long x, long y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
                candidates.Add((Offset(x, dx), Offset(y, dy)));
        }
    }

    /// <summary>Offset on the torus — wraps at 2^64 via unchecked long overflow.</summary>
    private static long Offset(long value, int delta) => unchecked(value + delta);

    private static (long X, long Y) LocalToUniverseCoord(int localX, int localY) =>
        (GameConstants.LocalToUniverseX(localX), GameConstants.LocalToUniverseY(localY));

    private static void ValidateLocal(int localX, int localY)
    {
        if (localX < 0 || localX >= GameConstants.InitialStateWidth)
            throw new ArgumentOutOfRangeException(nameof(localX), localX,
                $"X must be 0..{GameConstants.InitialStateWidth - 1}.");
        if (localY < 0 || localY >= GameConstants.InitialStateHeight)
            throw new ArgumentOutOfRangeException(nameof(localY), localY,
                $"Y must be 0..{GameConstants.InitialStateHeight - 1}.");
    }
}
