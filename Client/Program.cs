using System.Net.Sockets;
using System.Text;

const string Host = "127.0.0.1";
const int Port = 7777;

using var tcpClient = new TcpClient();
Console.WriteLine($"Connecting to {Host}:{Port}...");
await tcpClient.ConnectAsync(Host, Port);

var stream = tcpClient.GetStream();
using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

Console.WriteLine("Connected. Type 'start' or 'stop' (Ctrl+C to quit).");

// Background task: print everything the server broadcasts.
_ = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;

            Console.WriteLine($"<< {line}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Receive error: {ex.Message}");
    }
});

// Foreground: read your commands and send them to the server.
while (true)
{
    Console.Write(">> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
        continue;

    var bytes = Encoding.UTF8.GetBytes(input.Trim() + "\n");
    await stream.WriteAsync(bytes);
}
