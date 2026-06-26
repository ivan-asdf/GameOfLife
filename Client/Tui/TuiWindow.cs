using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618 // TextView: scrollable coords panel

namespace Client.Tui;

internal sealed class TuiWindow
{
    private const string BaseSchemeName = "Base";
    private const int BottomInset = 1; // window bottom border
    private const int CoordsMinHeight = 3;
    private const int CoordsMaxHeight = 10;
    // Bottom-up: command field, input header, status text, generation, status header
    private const int BottomBlockHeight = 5;
    private const int CoordsLabelAnchorEnd = BottomInset + BottomBlockHeight + CoordsMaxHeight + 1;

    private readonly IApplication _app;
    private readonly Label _gridView;
    private readonly TextView _coordsView;
    private readonly Label _generationLabel;
    private readonly Label _statusTextLabel;

    public Window Root { get; }
    public TextField CommandField { get; }

    private TuiWindow(
        IApplication app,
        Window root,
        TextField commandField,
        Label gridView,
        TextView coordsView,
        Label generationLabel,
        Label statusTextLabel)
    {
        _app = app;
        Root = root;
        CommandField = commandField;
        _gridView = gridView;
        _coordsView = coordsView;
        _generationLabel = generationLabel;
        _statusTextLabel = statusTextLabel;
    }

    public static TuiWindow Create(IApplication app)
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
            ScrollBars = true,
            SchemeName = BaseSchemeName,
            X = 0,
            Y = Pos.Bottom(coordsLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(margin: 0, minimumContentDim: CoordsMinHeight, to: statusHeaderLabel)
        };
        FixCoordsViewColors(coordsView);

        Label gridView = new Label
        {
            CanFocus = false,
            X = 0,
            Y = Pos.Bottom(drawLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(coordsLabel)
        };

        window.Add(
            drawLabel, gridView, coordsLabel, coordsView,
            statusHeaderLabel, generationLabel, statusTextLabel, inputLabel, commandField);

        return new TuiWindow(app, window, commandField, gridView, coordsView, generationLabel, statusTextLabel);
    }

    public void BindCommand(Func<string, Task> onCommand)
    {
        CommandField.Accepting += (_, e) =>
        {
            string command = CommandField.Text.ToString()?.Trim() ?? "";
            CommandField.Text = "";
            if (command.Length > 0)
                _ = onCommand(command);
            e.Handled = true;
        };

        CommandField.SetFocus();
    }

    public void RequestStop() => _app.RequestStop();

    public void Render(TuiModel model)
    {
        string gridText = string.Join('\n', model.DrawLines);
        string coordsText = string.Join('\n', model.CoordLines);

        _app.Invoke(() =>
        {
            bool commandHadFocus = CommandField.HasFocus;

            _gridView.Text = gridText;

            ScrollBar? scroll = _coordsView.VerticalScrollBar;
            int scrollRow = scroll?.Value ?? 0;
            _coordsView.Text = coordsText;
            if (scroll is not null)
                scroll.Value = scrollRow;

            _generationLabel.Text = $"Generation: {model.Generation}";
            _statusTextLabel.Text = model.StatusText;

            if (commandHadFocus)
                CommandField.SetFocus();
        });
    }

    private static void FixCoordsViewColors(TextView coordsView)
    {
        coordsView.GettingAttributeForRole += (_, args) =>
        {
            if (args.Role is VisualRole.ReadOnly or VisualRole.Editable or VisualRole.Focus)
            {
                args.Result = coordsView.GetAttributeForRole(VisualRole.Normal);
                args.Handled = true;
            }
        };

        coordsView.DrawReadOnlyColor += (_, _) =>
            coordsView.SetAttribute(coordsView.GetAttributeForRole(VisualRole.Normal));
    }
}
