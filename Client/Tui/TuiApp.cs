using System.Net.Sockets;
using System.Text;
using Client.Protocol;

namespace Client.Tui;

public sealed class TuiApp
{
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly TuiModel _model = new();
    private readonly object _lock = new();
    private volatile bool _running = true;

    public TuiApp(NetworkStream stream, StreamReader reader)
    {
        _stream = stream;
        _reader = reader;
    }

    public async Task RunAsync()
    {
        Console.CursorVisible = false;
        RenderLocked();

        _ = Task.Run(ReceiveLoopAsync);

        while (_running)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(50);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            var sendCommand = false;
            string command = "";

            lock (_lock)
            {
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        command = _model.InputBuffer.Trim();
                        _model.InputBuffer = "";
                        sendCommand = command.Length > 0;
                        break;
                    case ConsoleKey.Backspace when _model.InputBuffer.Length > 0:
                        _model.InputBuffer = _model.InputBuffer[..^1];
                        break;
                    case ConsoleKey.Escape:
                        _model.InputBuffer = "";
                        break;
                    default:
                        if (!char.IsControl(key.KeyChar))
                            _model.InputBuffer += key.KeyChar;
                        break;
                }
            }

            RenderLocked();

            if (sendCommand)
                await SendAsync(command);
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (_running)
            {
                var line = await _reader.ReadLineAsync();
                if (line is null)
                {
                    SetStatus("Disconnected.");
                    _running = false;
                    return;
                }

                var message = ServerMessage.Parse(line);
                if (message is null)
                    continue;

                lock (_lock)
                {
                    switch (message)
                    {
                        case StateMessage state:
                            _model.DrawLines = TuiScreen.BuildDrawLines(state.Cells);
                            _model.CoordLines = TuiScreen.BuildCoordLines(state.Cells);
                            break;
                        case ResultMessage result:
                            _model.StatusText = $"{result.Kind}: {result.Description}";
                            break;
                        case BadMessage bad:
                            _model.StatusText = $"bad message: \"{bad.RawLine}\"";
                            break;
                        case UnknownMessage unknown:
                            _model.StatusText = $"unknown message: \"{unknown.RawLine}\"";
                            break;
                    }
                }

                RenderLocked();
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Receive error: {ex.Message}");
            _running = false;
        }
    }

    private void SetStatus(string text)
    {
        lock (_lock)
            _model.StatusText = text;
        RenderLocked();
    }

    private async Task SendAsync(string command)
    {
        try
        {
            await _stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\n"));
        }
        catch (Exception ex)
        {
            SetStatus($"Send error: {ex.Message}");
        }
    }

    private void RenderLocked()
    {
        lock (_lock)
            TuiScreen.Render(_model);
    }
}
