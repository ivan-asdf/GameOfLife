# GameOfLife

Multiplayer Conway's Game of Life in C# (.NET 10): one server, many TUI clients, sparse storage on a 2^64 torus.

## Run

From the repo root:

```bash
dotnet run --project Server
dotnet run --project Client   # in another terminal
```

The server listens on port **7777**. Save files are read/written under `saves/` relative to the server's working directory, so start the server from the repo root.

## Commands

Type these in the client input field:

| Command | Description |
|---------|-------------|
| `toggle x y` | Flip cell at local coordinates |
| `set x y` | Set cell alive |
| `unset x y` | Set cell dead |
| `clear` | Clear grid (simulation must be stopped) |
| `start` / `stop` | Run or pause the simulation |
| `save name` / `load name` | Save or load a snapshot (`saves/name.gol`) |
| `list` | List available saves |
| `fps` / `fps N` | Query or set simulation speed (1–60) |

Cell edits and `clear` are blocked while the simulation is running.

## Coordinates

- **Commands** use local grid coordinates `0..99` for the editable 100×100 window.
- **Coord panel** shows universe coordinates (window centered on origin; local `(0,0)` is universe `(-50,-50)`).
- The grid view shows only that 100×100 window. Cells outside it (e.g. after many generations) appear in the coord list but not on the grid.

## Saves

Example: `load gosper` loads `saves/gosper.gol` (Gosper glider gun, gen 0).

Save format:

```
# GameOfLife v1
gen 0
-19,-8
...
```

## Stack

- **Protocol** — shared command/message parsing and save format
- **Server** — TCP listener, simulation loop, universe state
- **Client** — [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) TUI