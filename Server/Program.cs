using System.Net;
using System.Net.Sockets;
using System.Text;

const int Port = 7777;

// All connected clients receive broadcasts on these streams.
var clients = new List<NetworkStream>();
var clientsLock = new object();

var generation = 0L;
var running = false;
var runningLock = new object();

Console.WriteLine($"Server listening on port {Port}");
Console.WriteLine("Waiting for clients. Any client can send: start / stop");

var listener = new TcpListener(IPAddress.Any, Port);
listener.Start();

// Background loop: when running, bump generation and broadcast to everyone.
_ = Task.Run(async () =>
{
    while (true)
    {
        bool isRunning;
        lock (runningLock)
            isRunning = running;

        if (isRunning)
        {
            long gen;
            lock (runningLock)
                generation++;

            lock (runningLock)
                gen = generation;

            // Dummy moving pattern — later this becomes real Game of Life cells.
            var x = gen % 20;
            var cells = $"{x},0;{x + 1},0;{x + 2},1";
            await BroadcastAsync($"state gen={gen} cells={cells}");
            await Task.Delay(1000);
        }
        else
        {
            await Task.Delay(100);
        }
    }
});

// Accept clients forever.
while (true)
{
    var tcpClient = await listener.AcceptTcpClientAsync();
    var stream = tcpClient.GetStream();

    lock (clientsLock)
        clients.Add(stream);

    Console.WriteLine("Client connected.");

    // Handle one client in the background (read its commands).
    _ = Task.Run(() => HandleClientAsync(stream));
}

async Task HandleClientAsync(NetworkStream stream)
{
    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

    try
    {
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            Console.WriteLine($"Received command: {line}");

            switch (line.Trim().ToLowerInvariant())
            {
                case "start":
                    lock (runningLock)
                        running = true;
                    await SendAsync(stream, "result ok started");
                    break;

                case "stop":
                    lock (runningLock)
                        running = false;
                    await SendAsync(stream, "result ok stopped");
                    break;

                default:
                    await SendAsync(stream, "result error unknown command");
                    break;
            }
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

async Task BroadcastAsync(string message)
{
    List<NetworkStream> snapshot;
    lock (clientsLock)
        snapshot = clients.ToList();

    foreach (var stream in snapshot)
    {
        try
        {
            await SendAsync(stream, message);
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
