using System.Net;
using System.Net.Sockets;
using System.Text;
using Server;

const int Port = 7777;

var grid = new Universe();

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
            await HandleCommandAsync(stream, line);
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

async Task HandleCommandAsync(NetworkStream stream, string line)
{
    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 0)
        return;

    switch (parts[0].ToLowerInvariant())
    {
        case "toggle":
            await HandleCellCommandAsync(stream, parts, grid.Toggle);
            break;

        case "set":
            await HandleCellCommandAsync(stream, parts, (x, y) => grid.Set(x, y, alive: true));
            break;

        case "unset":
            await HandleCellCommandAsync(stream, parts, (x, y) => grid.Set(x, y, alive: false));
            break;

        case "clear":
            grid.Clear();
            await BroadcastStateAsync();
            await SendAsync(stream, "RESULT|ok|cleared grid");
            break;

        case "start":
            await SendAsync(stream, "RESULT|ok|started");
            break;

        case "stop":
            await SendAsync(stream, "RESULT|ok|stopped");
            break;

        default:
            await SendAsync(stream, $"RESULT|error|unknown command \"{line.Trim()}\"");
            break;
    }
}

async Task HandleCellCommandAsync(NetworkStream stream, string[] parts, Func<int, int, bool> action)
{
    if (!TryParseCoords(parts, out var x, out var y, out var usageError))
    {
        await SendAsync(stream, usageError);
        return;
    }

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

static bool TryParseCoords(string[] parts, out int x, out int y, out string errorMessage)
{
    x = 0;
    y = 0;
    errorMessage = "";

    if (parts.Length != 3 || !int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y))
    {
        errorMessage = $"RESULT|error|usage \"{parts[0]} x y\" (x and y: 0..{Universe.InitialStateWidth - 1})";
        return false;
    }

    return true;
}

async Task SendStateAsync(NetworkStream stream)
{
    var cells = grid.FormatCells();
    await SendAsync(stream, $"STATE|gen|0|cells|{cells}");
}

async Task BroadcastStateAsync()
{
    var cells = grid.FormatCells();
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
