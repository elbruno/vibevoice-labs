// =============================================================================
// VoicePresetManager — Voice Preset Loader (.npy files)
// =============================================================================
// Loads voice conditioning tensors from NumPy .npy files and a manifest.json.
// Each voice preset is a collection of named float tensors that condition the
// diffusion model to produce a specific speaker's voice characteristics.
// =============================================================================

using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace VoiceLabs.OnnxNative.Pipeline;

/// <summary>
/// Manages voice presets stored as .npy files in a voices directory.
/// </summary>
public sealed class VoicePresetManager
{
    private readonly string _voicesDir;
    private readonly Dictionary<string, VoiceManifestEntry> _manifest;

    /// <summary>
    /// Initializes the voice preset manager from a voices directory.
    /// </summary>
    /// <param name="voicesDir">
    /// Path to the voices directory containing manifest.json and .npy files.
    /// </param>
    public VoicePresetManager(string voicesDir)
    {
        _voicesDir = voicesDir;
        _manifest = new Dictionary<string, VoiceManifestEntry>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(voicesDir))
            return; // No voices directory — GetAvailableVoices() will return empty

        LoadManifest();
    }

    /// <summary>
    /// Returns the list of available voice preset names.
    /// </summary>
    public string[] GetAvailableVoices() => _manifest.Keys.ToArray();

    /// <summary>
    /// Loads all tensors for a voice preset.
    /// </summary>
    /// <param name="voiceName">Name of the voice (e.g., "Carter", "Emma").</param>
    /// <returns>Dictionary mapping tensor names to float arrays.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the voice preset is not found.</exception>
    public Dictionary<string, float[]> GetVoicePreset(string voiceName)
    {
        if (!_manifest.TryGetValue(voiceName, out var entry))
        {
            throw new FileNotFoundException(
                $"Voice preset '{voiceName}' not found. " +
                $"Available voices: {string.Join(", ", GetAvailableVoices())}");
        }

        var tensors = new Dictionary<string, float[]>();

        foreach (var (tensorName, fileName) in entry.Files)
        {
            var filePath = Path.Combine(_voicesDir, fileName);
            if (File.Exists(filePath))
            {
                tensors[tensorName] = ReadNpyFile(filePath);
            }
            else
            {
                throw new FileNotFoundException(
                    $"Voice preset file not found: {fileName} (for voice '{voiceName}').", filePath);
            }
        }

        return tensors;
    }

    // =========================================================================
    // Manifest Loading
    // =========================================================================

    /// <summary>Loads manifest.json from the voices directory.</summary>
    private void LoadManifest()
    {
        var manifestPath = Path.Combine(_voicesDir, "manifest.json");

        if (File.Exists(manifestPath))
        {
            LoadFromManifestFile(manifestPath);
        }
        else
        {
            // Auto-discover voices from directory structure:
            // voices/Carter/speaker_embedding.npy → voice "Carter"
            DiscoverVoicesFromDirectory();
        }
    }

    /// <summary>Parses manifest.json with expected structure.</summary>
    private void LoadFromManifestFile(string manifestPath)
    {
        // Expected manifest.json format:
        // {
        //   "voices": {
        //     "Carter": {
        //       "files": {
        //         "speaker_embedding": "Carter/speaker_embedding.npy",
        //         "voice_conditioning": "Carter/voice_conditioning.npy"
        //       }
        //     }
        //   }
        // }

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
                {
                    entry.Files[file.Name] = file.Value.GetString() ?? "";
                }
            }

            _manifest[voiceEntry.Name] = entry;
        }
    }

    /// <summary>
    /// Auto-discovers voice presets from subdirectory structure when no manifest exists.
    /// </summary>
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

        // Also check for flat .npy files named like "Carter.npy"
        foreach (var npyFile in Directory.GetFiles(_voicesDir, "*.npy"))
        {
            var voiceName = Path.GetFileNameWithoutExtension(npyFile);
            if (_manifest.ContainsKey(voiceName)) continue;

            var entry = new VoiceManifestEntry { Name = voiceName };
            entry.Files["speaker_embedding"] = Path.GetFileName(npyFile);
            _manifest[voiceName] = entry;
        }
    }

    // =========================================================================
    // NumPy .npy File Reader
    // =========================================================================

    /// <summary>
    /// Reads a NumPy .npy file and returns its data as a float array.
    /// Supports float32 and float64 data types (float64 is downcast to float32).
    /// </summary>
    /// <param name="path">Path to the .npy file.</param>
    /// <returns>Float array containing the tensor data.</returns>
    /// <exception cref="InvalidDataException">Thrown for unsupported .npy formats.</exception>
    public static float[] ReadNpyFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // Validate magic number: \x93NUMPY
        byte[] magic = reader.ReadBytes(6);
        if (magic.Length < 6 || magic[0] != 0x93 || magic[1] != (byte)'N' ||
            magic[2] != (byte)'U' || magic[3] != (byte)'M' ||
            magic[4] != (byte)'P' || magic[5] != (byte)'Y')
        {
            throw new InvalidDataException($"Not a valid NumPy .npy file: {path}");
        }

        // Read version
        byte majorVersion = reader.ReadByte();
        byte minorVersion = reader.ReadByte();
        _ = minorVersion; // Not used, but part of the format

        // Read header length (depends on version)
        int headerLen;
        if (majorVersion == 1)
        {
            headerLen = BinaryPrimitives.ReadUInt16LittleEndian(reader.ReadBytes(2));
        }
        else if (majorVersion >= 2)
        {
            headerLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(reader.ReadBytes(4));
        }
        else
        {
            throw new InvalidDataException($"Unsupported .npy version: {majorVersion}.{minorVersion}");
        }

        // Read and parse the header string (Python dict format)
        string header = Encoding.ASCII.GetString(reader.ReadBytes(headerLen)).Trim();

        // Extract dtype from header
        var dtype = ExtractHeaderValue(header, "'descr'") ?? ExtractHeaderValue(header, "\"descr\"");
        bool isLittleEndian = dtype?.Contains('<') == true || dtype?.Contains('|') == true;
        bool isFloat32 = dtype?.Contains("f4") == true || dtype?.Contains("float32") == true;
        bool isFloat64 = dtype?.Contains("f8") == true || dtype?.Contains("float64") == true;

        if (!isFloat32 && !isFloat64)
        {
            throw new InvalidDataException(
                $"Unsupported .npy dtype: {dtype}. Only float32 and float64 are supported.");
        }

        // Read the data
        var dataBytes = reader.ReadBytes((int)(stream.Length - stream.Position));

        if (isFloat32)
        {
            var floats = new float[dataBytes.Length / 4];
            Buffer.BlockCopy(dataBytes, 0, floats, 0, dataBytes.Length);
            return floats;
        }
        else // float64 → downcast to float32
        {
            var doubles = new double[dataBytes.Length / 8];
            Buffer.BlockCopy(dataBytes, 0, doubles, 0, dataBytes.Length);
            return doubles.Select(d => (float)d).ToArray();
        }
    }

    /// <summary>Extracts a value from the NumPy header dict string.</summary>
    private static string? ExtractHeaderValue(string header, string key)
    {
        int keyIndex = header.IndexOf(key, StringComparison.Ordinal);
        if (keyIndex < 0) return null;

        int colonIndex = header.IndexOf(':', keyIndex + key.Length);
        if (colonIndex < 0) return null;

        // Find the value between quotes after the colon
        int firstQuote = header.IndexOf('\'', colonIndex);
        if (firstQuote < 0) firstQuote = header.IndexOf('"', colonIndex);
        if (firstQuote < 0) return null;

        char quoteChar = header[firstQuote];
        int secondQuote = header.IndexOf(quoteChar, firstQuote + 1);
        if (secondQuote < 0) return null;

        return header[(firstQuote + 1)..secondQuote];
    }

    // =========================================================================
    // Internal Types
    // =========================================================================

    private sealed class VoiceManifestEntry
    {
        public required string Name { get; init; }
        public Dictionary<string, string> Files { get; } = new();
    }
}
