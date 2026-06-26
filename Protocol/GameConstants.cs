namespace Protocol;

public static class GameConstants
{
    public const int InitialStateWidth = 100;
    public const int InitialStateHeight = 100;

    /// <summary>
    /// Universe coordinates wrap on a 2^64 torus (unchecked long arithmetic).
    /// The editable 100×100 window is centered on (0, 0).
    /// Local grid (0, 0) maps to universe (InitialAreaOriginX, InitialAreaOriginY).
    /// </summary>
    public const long InitialAreaOriginX = -(InitialStateWidth / 2);
    public const long InitialAreaOriginY = -(InitialStateHeight / 2);

    public static long LocalToUniverseX(int localX) => InitialAreaOriginX + localX;

    public static long LocalToUniverseY(int localY) => InitialAreaOriginY + localY;
}
