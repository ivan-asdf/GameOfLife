using Protocol;

namespace Server;

public sealed class SaveStore
{
    private const string Extension = ".gol";
    private readonly string _directory;

    public SaveStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(Directory.GetCurrentDirectory(), "saves");
    }

    public string DirectoryPath => _directory;

    public void Save(string name, GameSaveData data)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(ResolvePath(name), GameSaveFile.Write(data));
    }

    public GameSaveData Load(string name)
    {
        string path = ResolvePath(name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"save file not found: \"{name}\"", path);

        return GameSaveFile.Parse(File.ReadAllText(path));
    }

    public IReadOnlyList<string> List()
    {
        if (!Directory.Exists(_directory))
            return Array.Empty<string>();

        return Directory.GetFiles(_directory, "*" + Extension)
            .Select(path => Path.GetFileNameWithoutExtension(path)!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ResolvePath(string name) =>
        Path.Combine(_directory, name + Extension);
}
