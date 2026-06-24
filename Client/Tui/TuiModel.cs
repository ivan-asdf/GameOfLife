namespace Client.Tui;

public sealed class TuiModel
{
    private readonly object _lock = new();
    private string[] _drawLines = ["(waiting for state)"];
    private string[] _coordLines = ["(empty)"];
    private string _statusText = "Connected. Commands: toggle/set/unset x y, clear, start, stop";
    private string _inputBuffer = "";

    public string[] DrawLines
    {
        get { lock (_lock) return _drawLines; }
        set { lock (_lock) _drawLines = value; }
    }

    public string[] CoordLines
    {
        get { lock (_lock) return _coordLines; }
        set { lock (_lock) _coordLines = value; }
    }

    public string StatusText
    {
        get { lock (_lock) return _statusText; }
        set { lock (_lock) _statusText = value; }
    }

    public string InputBuffer
    {
        get { lock (_lock) return _inputBuffer; }
        set { lock (_lock) _inputBuffer = value; }
    }

    public void Render()
    {
        lock (_lock)
            TuiScreen.Render(this);
    }
}
