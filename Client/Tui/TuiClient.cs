using System.Net.Sockets;
using System.Text;
using Protocol;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Client.Tui;

public sealed class TuiClient
{
    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly TuiModel _model = new();

    private TuiWindow? _window;
    private CancellationTokenSource? _cts;

    public TuiClient(NetworkStream stream, StreamReader reader)
    {
        _stream = stream;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        using IApplication application = Application.Create();
        application.Init();

        ConfigureQuit(application);

        TuiWindow window = TuiWindow.Create(application);
        window.BindCommand(command => SendCommandAsync(command, ct));
        _window = window;

        window.Render(_model);
        _ = ReceiveLoopAsync(ct);

        await application.RunAsync(window.Root, ct);
        _cts.Cancel();
    }

    private static void ConfigureQuit(IApplication application)
    {
        application.Keyboard.KeyDown += (_, key) =>
        {
            if (key == Key.Q.WithCtrl || key == Key.C.WithCtrl || key == Key.Esc)
            {
                application.RequestStop();
                key.Handled = true;
            }
        };
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                string? line = await _reader.ReadLineAsync(ct);
                if (line is null)
                {
                    _model.StatusText = "Disconnected.";
                    _window?.Render(_model);
                    _window?.RequestStop();
                    return;
                }

                ServerMessage? message = ServerMessage.Parse(line);
                if (message is null)
                    continue;

                switch (message)
                {
                    case StateMessage state:
                        _model.Generation = state.Generation;
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

                _window?.Render(_model);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _model.StatusText = $"Receive error: {ex.Message}";
            _window?.Render(_model);
            _window?.RequestStop();
        }
    }

    private async Task SendCommandAsync(string command, CancellationToken ct)
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
            _window?.Render(_model);
        }
    }
}
