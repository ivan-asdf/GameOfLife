using System.Net.Sockets;
using System.Text;
using Client.Protocol;

namespace Client.Tui;

public sealed class TuiApp
{
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly TuiModel _model = new();
    private CancellationTokenSource? _cts;

    public TuiApp(NetworkStream stream, StreamReader reader)
    {
        _stream = stream;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Console.CursorVisible = false;

        try
        {
            _model.Render();

            var ct = _cts.Token;
            await Task.WhenAll(InputLoopAsync(ct), ReceiveLoopAsync(ct));
        }
        catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            Console.CursorVisible = true;
        }
    }

    private void Stop() => _cts?.Cancel();

    private async Task InputLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(50, ct);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);
                var sendCommand = false;
                string command = "";

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

                _model.Render();

                if (sendCommand)
                    await SendAsync(command, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _model.StatusText = $"Input error: {ex.Message}";
            _model.Render();
            Stop();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(ct);
                if (line is null)
                {
                    _model.StatusText = "Disconnected.";
                    _model.Render();
                    Stop();
                    return;
                }

                var message = ServerMessage.Parse(line);
                if (message is null)
                    continue;

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

                _model.Render();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _model.StatusText = $"Receive error: {ex.Message}";
            _model.Render();
            Stop();
        }
    }

    private async Task SendAsync(string command, CancellationToken ct)
    {
        try
        {
            await _stream.WriteAsync(Encoding.UTF8.GetBytes(command + "\n"), ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _model.StatusText = $"Send error: {ex.Message}";
            _model.Render();
        }
    }
}
