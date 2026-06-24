using Server;

const int Port = 7777;

using CancellationTokenSource quitCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quitCts.Cancel();
};

ServerApp app = new ServerApp(new Universe());

Console.WriteLine($"Server listening on port {Port}");
Console.WriteLine("Clients can edit the 100x100 grid: toggle/set/unset x y, clear, start, stop");

await app.ListenAsync(Port, quitCts.Token);
