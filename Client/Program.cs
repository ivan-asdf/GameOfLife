using System.Net.Sockets;
using System.Text;
using Client.Tui;

const string Host = "127.0.0.1";
const int Port = 7777;

using var quitCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    quitCts.Cancel();
};

using var tcpClient = new TcpClient();
await tcpClient.ConnectAsync(Host, Port, quitCts.Token);

var stream = tcpClient.GetStream();
using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

var app = new TuiApp(stream, reader);
await app.RunAsync(quitCts.Token);
