using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Loads voice conditioning tensors from .npy files.
/// </summary>
internal sealed class VoicePresetLoader
{
    private readonly string _voicesDir;
    private readonly Dictionary<string, VoiceManifestEntry> _manifest;

    public VoicePresetLoader(string voicesDir)
    {
        _voicesDir = voicesDir;
        _manifest = new Dictionary<string, VoiceManifestEntry>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(voicesDir))
            return;

        LoadManifest();
    }

    public string[] GetAvailableVoices() => _manifest.Keys.ToArray();

    public Dictionary<string, float[]> GetVoicePreset(string voiceName)
    {
        if (!_manifest.TryGetValue(voiceName, out var entry))
        {
            throw new FileNotFoundException(
                $"Voice preset '{voiceName}' not found. Available: {string.Join(", ", GetAvailableVoices())}");
        }

        var tensors = new Dictionary<string, float[]>();
        foreach (var (tensorName, fileName) in entry.Files)
        {
            var filePath = Path.Combine(_voicesDir, fileName);
            if (File.Exists(filePath))
                tensors[tensorName] = ReadNpyFile(filePath);
            else
                throw new FileNotFoundException($"Voice preset file not found: {fileName}", filePath);
        }
        return tensors;
    }

    private void LoadManifest()
    {
        var manifestPath = Path.Combine(_voicesDir, "manifest.json");
        if (File.Exists(manifestPath))
            LoadFromManifestFile(manifestPath);
        else
            DiscoverVoicesFromDirectory();
    }

    private void LoadFromManifestFile(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("voices", out var voicesElement))
            return;

        foreach (var voiceEntry in voicesElement.EnumerateObject())
        {
            var entry = new VoiceManifestEntry { Name = voiceEntry.Name };
            if (voiceEntry.Value.TryGetProperty("files", out var filesElement))
            {
                foreach (var file in filesElement.EnumerateObject())
                    entry.Files[file.Name] = file.Value.GetString() ?? "";
            }
            _manifest[voiceEntry.Name] = entry;
        }
    }

    private void DiscoverVoicesFromDirectory()
    {
        foreach (var dir in Directory.GetDirectories(_voicesDir))
        {
            var voiceName = Path.GetFileName(dir);
            var npyFiles = Directory.GetFiles(dir, "*.npy");
            if (npyFiles.Length == 0) continue;

            var entry = new VoiceManifestEntry { Name = voiceName };
            foreach (var npyFile in npyFiles)
            {
                var tensorName = Path.GetFileNameWithoutExtension(npyFile);
                entry.Files[tensorName] = Path.Combine(voiceName, Path.GetFileName(npyFile));
            }
            _manifest[voiceName] = entry;
        }

        foreach (var npyFile in Directory.GetFiles(_voicesDir, "*.npy"))
        {
            var voiceName = Path.GetFileNameWithoutExtension(npyFile);
            if (_manifest.ContainsKey(voiceName)) continue;
            var entry = new VoiceManifestEntry { Name = voiceName };
            entry.Files["speaker_embedding"] = Path.GetFileName(npyFile);
            _manifest[voiceName] = entry;
        }
    }

    public static float[] ReadNpyFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        byte[] magic = reader.ReadBytes(6);
        if (magic.Length < 6 || magic[0] != 0x93 || magic[1] != (byte)'N' ||
            magic[2] != (byte)'U' || magic[3] != (byte)'M' ||
            magic[4] != (byte)'P' || magic[5] != (byte)'Y')
            throw new InvalidDataException($"Not a valid .npy file: {path}");

        byte majorVersion = reader.ReadByte();
        reader.ReadByte(); // minor

        int headerLen = majorVersion == 1
            ? BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(2))
            : majorVersion >= 2
                ? (int)BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4))
                : throw new InvalidDataException($"Unsupported .npy version: {majorVersion}");

        string header = Encoding.ASCII.GetString(reader.ReadBytes(headerLen)).Trim();
        var dtype = ExtractHeaderValue(header, "'descr'") ?? ExtractHeaderValue(header, "\"descr\"");
        bool isFloat32 = dtype?.Contains("f4") == true || dtype?.Contains("float32") == true;
        bool isFloat64 = dtype?.Contains("f8") == true || dtype?.Contains("float64") == true;

        if (!isFloat32 && !isFloat64)
            throw new InvalidDataException($"Unsupported .npy dtype: {dtype}. Only float32/float64 supported.");

        var dataBytes = reader.ReadBytes((int)(stream.Length - stream.Position));

        if (isFloat32)
        {
            var floats = new float[dataBytes.Length / 4];
            Buffer.BlockCopy(dataBytes, 0, floats, 0, dataBytes.Length);
            return floats;
        }
        else
        {
            var doubles = new double[dataBytes.Length / 8];
            Buffer.BlockCopy(dataBytes, 0, doubles, 0, dataBytes.Length);
            return doubles.Select(d => (float)d).ToArray();
        }
    }

    private static string? ExtractHeaderValue(string header, string key)
    {
        int keyIndex = header.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0) return null;
        int colonIndex = header.IndexOf(':', keyIndex + key.Length);
        if (colonIndex < 0) return null;
        int firstQuote = header.IndexOf('\'', colonIndex);
        if (firstQuote < 0) firstQuote = header.IndexOf('"', colonIndex);
        if (firstQuote < 0) return null;
        char quoteChar = header[firstQuote];
        int secondQuote = header.IndexOf(quoteChar, firstQuote + 1);
        if (secondQuote < 0) return null;
        return header[(firstQuote + 1)..secondQuote];
    }

    private sealed class VoiceManifestEntry
    {
        public required string Name { get; init; }
        public Dictionary<string, string> Files { get; } = new();
    }
}
