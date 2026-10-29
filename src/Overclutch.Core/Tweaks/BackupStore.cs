using System.Text.Json;

namespace Overclutch.Core.Tweaks;

/// <summary>Persists the original state captured for each applied tweak.</summary>
public sealed class BackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<BackupEntry>> _entries;

    public BackupStore(string path)
    {
        _path = path;
        _entries = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, List<BackupEntry>>>(File.ReadAllText(path), JsonOptions) ?? []
            : [];
    }

    public static BackupStore Default() => new(Path.Combine(AppPaths.DataDirectory, "backups.json"));

    public bool Has(string tweakId)
    {
        lock (_lock)
            return _entries.ContainsKey(tweakId);
    }

    public IReadOnlyList<string> TweakIds
    {
        get
        {
            lock (_lock)
                return [.. _entries.Keys];
        }
    }

    public IReadOnlyList<BackupEntry>? Get(string tweakId)
    {
        lock (_lock)
            return _entries.TryGetValue(tweakId, out var list) ? list : null;
    }

    public void Set(string tweakId, List<BackupEntry> entries)
    {
        lock (_lock)
        {
            _entries[tweakId] = entries;
            Save();
        }
    }

    public void Remove(string tweakId)
    {
        lock (_lock)
        {
            if (_entries.Remove(tweakId))
                Save();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_entries, JsonOptions));
        File.Move(temp, _path, overwrite: true);
    }
}
