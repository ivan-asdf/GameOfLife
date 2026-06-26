using System.Net.Sockets;
using System.Text;
using Client.Tui;

const string Host = "127.0.0.1";
const int Port = 7777;

using CancellationTokenSource quitCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quitCts.Cancel();
};

using TcpClient tcpClient = new TcpClient();
await tcpClient.ConnectAsync(Host, Port, quitCts.Token);

NetworkStream stream = tcpClient.GetStream();
using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

TuiClient client = new TuiClient(stream, reader);
await client.RunAsync(quitCts.Token);
