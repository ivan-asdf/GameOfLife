using System.Net.Sockets;
using System.Text;
using Protocol;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Client.Tui;

public sealed class TuiApp
{
    private const string BaseSchemeName = "Base";
    private const int BottomInset = 1; // window bottom border
    private const int CoordsMinHeight = 3;
    private const int CoordsMaxHeight = 10;
    // Bottom-up: command field, input header, status text, generation, status header
    private const int BottomBlockHeight = 5;
    private const int CoordsLabelAnchorEnd = BottomInset + BottomBlockHeight + CoordsMaxHeight + 1;

    private readonly NetworkStream _stream;
    private readonly StreamReader _reader;
    private readonly TuiModel _model = new();

    private IApplication? _guiApp;
    private Window? _window;
    private Label? _gridView;
    private TextView? _coordsView;
    private Label? _generationLabel;
    private Label? _statusTextLabel;
    private TextField? _commandField;
    private CancellationTokenSource? _cts;

    public TuiApp(NetworkStream stream, StreamReader reader)
    {
        _stream = stream;
        _reader = reader;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        using IApplication guiApp = Application.Create();
        guiApp.Init();
        _guiApp = guiApp;

        ConfigureQuit(guiApp);
        BuildWindow();
        RefreshUi();

        _ = ReceiveLoopAsync(ct);

        await guiApp.RunAsync(_window!, ct);
        _cts.Cancel();
    }

    private static void ConfigureQuit(IApplication guiApp)
    {
        guiApp.Keyboard.KeyDown += (_, key) =>
        {
            if (key == Key.Q.WithCtrl || key == Key.C.WithCtrl || key == Key.Esc)
            {
                guiApp.RequestStop();
                key.Handled = true;
            }
        };
    }

    private void BuildWindow()
    {
        Window window = new Window
        {
            Title = "Game of Life (Esc or Ctrl+Q to quit)",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Label drawLabel = new Label { Text = "=== Draw ===", X = 0, Y = 0, CanFocus = false };

        TextField commandField = new TextField
        {
            X = 0,
            Y = Pos.AnchorEnd(BottomInset + 0),
            Width = Dim.Fill(),
            Height = 1
        };

        Label inputLabel = new Label { Text = "=== Input ===", X = 0, Y = Pos.AnchorEnd(BottomInset + 1), CanFocus = false };

        Label statusTextLabel = new Label
        {
            CanFocus = false,
            X = 0,
            Y = Pos.AnchorEnd(BottomInset + 2),
            Width = Dim.Fill(),
            Height = 1
        };

        Label generationLabel = new Label
        {
            CanFocus = false,
            X = 0,
            Y = Pos.AnchorEnd(BottomInset + 3),
            Width = Dim.Fill(),
            Height = 1
        };

        Label statusHeaderLabel = new Label
        {
            Text = "=== Status ===",
            X = 0,
            Y = Pos.AnchorEnd(BottomInset + BottomBlockHeight - 1),
            CanFocus = false
        };

        Label coordsLabel = new Label
        {
            Text = "=== Coords ===",
            X = 0,
            Y = Pos.AnchorEnd(CoordsLabelAnchorEnd),
            CanFocus = false
        };

        TextView coordsView = new TextView
        {
            ReadOnly = true,
            CanFocus = false,
            SchemeName = BaseSchemeName,
            ScrollBars = true,
            X = 0,
            Y = Pos.Bottom(coordsLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(margin: 0, minimumContentDim: CoordsMinHeight, to: statusHeaderLabel)
        };

        Label gridView = new Label
        {
            CanFocus = false,
            X = 0,
            Y = Pos.Bottom(drawLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(coordsLabel)
        };

        commandField.Accepting += (_, e) =>
        {
            string command = commandField.Text.ToString()?.Trim() ?? "";
            commandField.Text = "";
            if (command.Length > 0)
                _ = SendCommandAsync(command, ct: _cts?.Token ?? default);
            e.Handled = true;
        };

        window.Add(
            drawLabel, gridView, coordsLabel, coordsView,
            statusHeaderLabel, generationLabel, statusTextLabel, inputLabel, commandField);

        _window = window;
        _gridView = gridView;
        _coordsView = coordsView;
        _generationLabel = generationLabel;
        _statusTextLabel = statusTextLabel;
        _commandField = commandField;

        commandField.SetFocus();
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
                    RefreshUi();
                    _guiApp?.RequestStop();
                    return;
                }

                ServerMessage? message = ServerMessage.Parse(line);
                if (message is null)
                    continue;

                // bool refreshUi = true;

                switch (message)
                {
                    case StateMessage state:
                        _model.Generation = state.Generation;
                        _model.DrawLines = TuiScreen.BuildDrawLines(state.Cells);
                        _model.CoordLines = TuiScreen.BuildCoordLines(state.Cells);
                        // refreshUi = ShouldRefreshStateUi();
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

                // if (refreshUi)
                    RefreshUi();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _model.StatusText = $"Receive error: {ex.Message}";
            RefreshUi();
            _guiApp?.RequestStop();
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
            RefreshUi();
        }
    }

    private void RefreshUi()
    {
        if (_guiApp is null || _gridView is null || _coordsView is null
            || _generationLabel is null || _statusTextLabel is null)
            return;

        string gridText = string.Join('\n', _model.DrawLines);
        string coordsText = string.Join('\n', _model.CoordLines);

        _guiApp.Invoke(() =>
        {
            bool commandHadFocus = _commandField?.HasFocus == true;

            _gridView.Text = gridText;
            _coordsView.Text = coordsText;
            _generationLabel.Text = $"Generation: {_model.Generation}";
            _statusTextLabel.Text = _model.StatusText;

            if (commandHadFocus)
                _commandField?.SetFocus();
        });
    }
}
