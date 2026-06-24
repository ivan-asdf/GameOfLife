using System.Net;
using System.Net.Sockets;
using System.Text;
using Server;
using Server.Protocol;

const int Port = 7777;

var universe = new Universe();

var clients = new List<NetworkStream>();
var clientsLock = new object();

Console.WriteLine($"Server listening on port {Port}");
Console.WriteLine("Clients can edit the 100x100 grid: toggle/set/unset x y, clear, start, stop");

var listener = new TcpListener(IPAddress.Any, Port);
listener.Start();

while (true)
{
    var tcpClient = await listener.AcceptTcpClientAsync();
    var stream = tcpClient.GetStream();

    lock (clientsLock)
        clients.Add(stream);

    Console.WriteLine("Client connected.");
    _ = Task.Run(() => ClientLoopAsync(stream));
}

async Task ClientLoopAsync(NetworkStream stream)
{
    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

    try
    {
        await SendStateAsync(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            Console.WriteLine($"Received command: {line}");

            var command = ClientCommand.Parse(line);
            if (command is null)
                continue;

            await HandleCommandAsync(stream, command);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client error: {ex.Message}");
    }
    finally
    {
        lock (clientsLock)
            clients.Remove(stream);

        stream.Close();
        Console.WriteLine("Client disconnected.");
    }
}

async Task HandleCommandAsync(NetworkStream stream, ClientCommand command)
{
    switch (command)
    {
        case ToggleCommand(var x, var y):
            await HandleCellCommandAsync(stream, x, y, universe.Toggle);
            break;

        case SetCommand(var x, var y):
            await HandleCellCommandAsync(stream, x, y, (cx, cy) => universe.Set(cx, cy, alive: true));
            break;

        case UnsetCommand(var x, var y):
            await HandleCellCommandAsync(stream, x, y, (cx, cy) => universe.Set(cx, cy, alive: false));
            break;

        case ClearCommand:
            universe.Clear();
            await BroadcastStateAsync();
            await SendAsync(stream, "RESULT|ok|cleared grid");
            break;

        case StartCommand:
            await SendAsync(stream, "RESULT|ok|started");
            break;

        case StopCommand:
            await SendAsync(stream, "RESULT|ok|stopped");
            break;

        case BadCommand(var errorMessage):
            await SendAsync(stream, errorMessage);
            break;

        case UnknownCommand(var rawLine):
            await SendAsync(stream, $"RESULT|error|unknown command \"{rawLine.Trim()}\"");
            break;
    }
}

async Task HandleCellCommandAsync(NetworkStream stream, int x, int y, Func<int, int, bool> action)
{
    try
    {
        var alive = action(x, y);
        await BroadcastStateAsync();
        await SendAsync(stream, $"RESULT|ok|cell ({x},{y}) is now {(alive ? "alive" : "dead")}");
    }
    catch (ArgumentOutOfRangeException ex)
    {
        await SendAsync(stream, $"RESULT|error|invalid coordinates \"{x},{y}\" ({ex.Message})");
    }
}

async Task SendStateAsync(NetworkStream stream)
{
    var cells = universe.FormatCells();
    await SendAsync(stream, $"STATE|gen|0|cells|{cells}");
}

async Task BroadcastStateAsync()
{
    var cells = universe.FormatCells();
    await BroadcastAsync($"STATE|gen|0|cells|{cells}");
}

async Task BroadcastAsync(string message)
{
    List<NetworkStream> snapshot;
    lock (clientsLock)
        snapshot = clients.ToList();

    foreach (var clientStream in snapshot)
    {
        try
        {
            await SendAsync(clientStream, message);
        }
        catch
        {
            // Client will be cleaned up on its read loop failing.
        }
    }
}

static async Task SendAsync(NetworkStream stream, string message)
{
    var bytes = Encoding.UTF8.GetBytes(message + "\n");
    await stream.WriteAsync(bytes);
}
