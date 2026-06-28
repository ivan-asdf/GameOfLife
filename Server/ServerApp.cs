using System.Net;
using System.Net.Sockets;
using System.Text;
using Protocol;

namespace Server;

public sealed class ServerApp
{
    private volatile int _tickDelayMs = GameConstants.DefaultSimulationTickDelayMs;

    private readonly Universe _universe;
    private readonly SaveStore _saves;
    private readonly List<NetworkStream> _clients = new();
    private readonly object _clientsLock = new();
    private readonly object _simulationLock = new();
    private CancellationTokenSource? _simulationCts;
    private Task? _simulationTask;

    public ServerApp(Universe universe, SaveStore? saves = null)
    {
        _universe = universe;
        _saves = saves ?? new SaveStore();
    }

    public async Task ListenAsync(int port, CancellationToken cancellationToken = default)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);
                NetworkStream stream = tcpClient.GetStream();

                AddClient(stream);
                Console.WriteLine("Client connected.");
                _ = ReceiveLoopAsync(stream, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await StopSimulationAsync();
            listener.Stop();
        }
    }

    private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken ct)
    {
        using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        try
        {
            await SendAsync(stream, _universe.FormatState());

            while (!ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;

                Console.WriteLine($"Received command: {line}");

                ClientCommand? command = ClientCommand.Parse(line);
                if (command is null)
                    continue;

                await HandleCommandAsync(stream, command);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            RemoveClient(stream);
            stream.Close();
            Console.WriteLine("Client disconnected.");
        }
    }

    private async Task HandleCommandAsync(NetworkStream stream, ClientCommand command)
    {
        switch (command)
        {
            case CellCommand cell:
                await HandleCellCommandAsync(stream, cell);
                break;

            case ClearCommand:
                if (IsSimulationRunning())
                {
                    await SendAsync(stream, ServerMessage.FormatResultError(
                        "simulation is running; stop first"));
                    break;
                }

                _universe.Clear();
                await BroadcastStateAsync();
                await SendAsync(stream, ServerMessage.FormatResultOk("cleared grid"));
                break;

            case StartCommand:
                StartSimulation();
                await SendAsync(stream, ServerMessage.FormatResultOk("started"));
                break;

            case StopCommand:
                await StopSimulationAsync();
                await SendAsync(stream, ServerMessage.FormatResultOk("stopped"));
                break;

            case SaveCommand save:
                await HandleSaveAsync(stream, save.Name);
                break;

            case LoadCommand load:
                await HandleLoadAsync(stream, load.Name);
                break;

            case ListCommand:
                await HandleListAsync(stream);
                break;

            case FpsCommand fps:
                await HandleFpsAsync(stream, fps);
                break;

            case BadCommand bad:
                await SendAsync(stream, bad.ErrorMessage);
                break;

            case UnknownCommand unknown:
                await SendAsync(stream, ServerMessage.FormatUnknownCommand(unknown.RawLine));
                break;
        }
    }

    private async Task HandleCellCommandAsync(NetworkStream stream, CellCommand command)
    {
        var action = (Func<int, int, bool>)(command switch
        {
            ToggleCommand => _universe.ToggleCell,
            SetCommand => (x, y) => _universe.SetCell(x, y, alive: true),
            UnsetCommand => (x, y) => _universe.SetCell(x, y, alive: false),
            _ => throw new InvalidOperationException($"Unexpected cell command: {command}")
        });

        try
        {
            bool alive = action(command.X, command.Y);
            await BroadcastStateAsync();
            await SendAsync(stream, ServerMessage.FormatCellState(command.X, command.Y, alive));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            await SendAsync(stream, ServerMessage.FormatInvalidCoordinates(command.X, command.Y, ex.Message));
        }
    }

    private async Task HandleSaveAsync(NetworkStream stream, string name)
    {
        try
        {
            GameSaveData snapshot = _universe.ExportSnapshot();
            _saves.Save(name, snapshot);
            await SendAsync(stream, ServerMessage.FormatResultOk(
                $"saved \"{name}\" ({snapshot.Cells.Count} cells, gen {snapshot.Generation})"));
        }
        catch (Exception ex)
        {
            await SendAsync(stream, ServerMessage.FormatResultError($"save failed: {ex.Message}"));
        }
    }

    private async Task HandleListAsync(NetworkStream stream)
    {
        IReadOnlyList<string> names = _saves.List();
        string description = names.Count == 0
            ? "no saves"
            : string.Join(", ", names);
        await SendAsync(stream, ServerMessage.FormatResultOk(description));
    }

    private async Task HandleFpsAsync(NetworkStream stream, FpsCommand command)
    {
        if (command.Fps is null)
        {
            await SendAsync(stream, ServerMessage.FormatResultOk($"fps is {GetSimulationFps()}"));
            return;
        }

        _tickDelayMs = 1000 / command.Fps.Value;
        await SendAsync(stream, ServerMessage.FormatResultOk($"fps set to {command.Fps.Value}"));
    }

    private int GetSimulationFps() => 1000 / _tickDelayMs;

    private async Task HandleLoadAsync(NetworkStream stream, string name)
    {
        try
        {
            await StopSimulationAsync();

            GameSaveData snapshot = _saves.Load(name);
            _universe.LoadSnapshot(snapshot.Generation, snapshot.Cells);
            await BroadcastStateAsync();
            await SendAsync(stream, ServerMessage.FormatResultOk(
                $"loaded \"{name}\" ({snapshot.Cells.Count} cells, gen {snapshot.Generation})"));
        }
        catch (FileNotFoundException)
        {
            await SendAsync(stream, ServerMessage.FormatResultError($"save file not found: \"{name}\""));
        }
        catch (FormatException ex)
        {
            await SendAsync(stream, ServerMessage.FormatResultError($"invalid save file: {ex.Message}"));
        }
        catch (Exception ex)
        {
            await SendAsync(stream, ServerMessage.FormatResultError($"load failed: {ex.Message}"));
        }
    }

    private bool IsSimulationRunning()
    {
        lock (_simulationLock)
            return _simulationCts is not null;
    }

    private void StartSimulation()
    {
        lock (_simulationLock)
        {
            if (_simulationCts is not null)
                return;

            _simulationCts = new CancellationTokenSource();
            CancellationToken ct = _simulationCts.Token;
            _simulationTask = SimulationLoopAsync(ct);
        }
    }

    private async Task StopSimulationAsync()
    {
        Task? task;
        lock (_simulationLock)
        {
            if (_simulationCts is null)
                return;

            _simulationCts.Cancel();
            task = _simulationTask;
            _simulationCts = null;
            _simulationTask = null;
        }

        try
        {
            await task!;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SimulationLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                _universe.Step();
                await BroadcastStateAsync();
                await Task.Delay(_tickDelayMs, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void AddClient(NetworkStream stream)
    {
        lock (_clientsLock)
            _clients.Add(stream);
    }

    private void RemoveClient(NetworkStream stream)
    {
        lock (_clientsLock)
            _clients.Remove(stream);
    }

    private async Task BroadcastStateAsync()
    {
        await BroadcastAsync(_universe.FormatState());
    }

    private async Task BroadcastAsync(string message)
    {
        List<NetworkStream> snapshot;
        lock (_clientsLock)
            snapshot = _clients.ToList();

        foreach (NetworkStream clientStream in snapshot)
        {
            try
            {
                await SendAsync(clientStream, message);
            }
            catch
            {
                // Keep broadcasting to other clients if one stream is dead.
            }
        }
    }

    private static async Task SendAsync(NetworkStream stream, string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
        await stream.WriteAsync(bytes);
    }
}
