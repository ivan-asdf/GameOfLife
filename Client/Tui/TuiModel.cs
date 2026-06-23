namespace Client.Tui;

public sealed class TuiModel
{
    public string[] DrawLines { get; set; } = ["(waiting for state)"];
    public string[] CoordLines { get; set; } = ["(empty)"];
    public string StatusText { get; set; } = "Connected.";
    public string InputBuffer { get; set; } = "";
}
