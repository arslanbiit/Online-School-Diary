using System.Text.Json;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Server.Infrastructure;

public sealed class JsonFileStore
{
    private readonly string _dataDir;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonFileStore(string dataDir)
    {
        _dataDir = dataDir;
        Directory.CreateDirectory(_dataDir);
    }

    private string PathFor(string name) => System.IO.Path.Combine(_dataDir, $"{name}.json");

    public async Task<IReadOnlyList<T>> LoadListAsync<T>(string name)
    {
        await _gate.WaitAsync();
        try
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return Array.Empty<T>();
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonDefaults.Options) ?? new List<T>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveListAsync<T>(string name, IReadOnlyList<T> items)
    {
        await _gate.WaitAsync();
        try
        {
            var path = PathFor(name);
            var json = JsonSerializer.Serialize(items, JsonDefaults.Options);
            await File.WriteAllTextAsync(path, json);
        }
        finally
        {
            _gate.Release();
        }
    }
}

