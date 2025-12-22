using RRCServices.Season;
using System.Text.Json;

namespace PublicApp
{
    /// <summary>
    /// Persists SeasonSettings to a dedicated JSON file.
    /// NOT appsettings.json.
    /// </summary>
    public sealed class JsonSeasonSettingsStore : ISeasonSettingsStore
    {
        private readonly string _filePath;

        public JsonSeasonSettingsStore(string filePath)
        {
            _filePath = filePath
                ?? throw new ArgumentNullException(nameof(filePath));
        }

        public async Task<SeasonSettings?> ReadAsync(CancellationToken ct = default)
        {
            if (!File.Exists(_filePath))
                return null;

            var json = await File.ReadAllTextAsync(_filePath, ct);

            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<SeasonSettings>(json);
        }

        public async Task WriteAsync(SeasonSettings settings, CancellationToken ct = default)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true });

            await File.WriteAllTextAsync(_filePath, json, ct);
        }
    }
}
