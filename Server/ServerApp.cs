using System.Net;
using System.Net.Sockets;
using System.Text;
using Protocol;

namespace Server;

public sealed class ServerApp
{
    private const int TickDelayMs = 150;

    private readonly Universe _universe;
    private readonly List<NetworkStream> _clients = new();
    private readonly object _clientsLock = new();
    private readonly object _simulationLock = new();
    private CancellationTokenSource? _simulationCts;
    private Task? _simulationTask;

    public ServerApp(Universe universe)
    {
        _universe = universe;
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

            case BadCommand(string errorMessage):
                await SendAsync(stream, errorMessage);
                break;

            case UnknownCommand(string rawLine):
                await SendAsync(stream, ServerMessage.FormatUnknownCommand(rawLine));
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
                await Task.Delay(TickDelayMs, ct);
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
