using System.Text.Json;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Serialization;

namespace AethericForge.Runtime.Models.Archive.Serialization;

public sealed class JsonArchiveSerializer : IArchiveSerializer
{
    private readonly JsonSerializerOptions _options;

    public string ContentType => "application/json";

    public JsonArchiveSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public ValueTask SerializeAsync<T>(
        Stream destination,
        T value,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        return new ValueTask(
            JsonSerializer.SerializeAsync(destination, value, _options, ct));
    }

    public async ValueTask<T?> DeserializeAsync<T>(
        Stream source,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return await JsonSerializer.DeserializeAsync<T>(
            source,
            _options,
            ct);
    }
}