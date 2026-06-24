using Server;

const int Port = 7777;

ServerApp app = new ServerApp(new Universe());

Console.WriteLine($"Server listening on port {Port}");
Console.WriteLine("Clients can edit the 100x100 grid: toggle/set/unset x y, clear, start, stop");

await app.ListenAsync(Port);
