using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Byte-Pair Encoding tokenizer that loads from HuggingFace tokenizer.json format.
/// Supports Qwen2.5 tokenizer with ByteLevel pre-tokenizer.
/// </summary>
internal sealed partial class BpeTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _reverseVocab;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<(string, string), int> _mergeRanks;
    private readonly Regex _preTokenizeRegex;
    private readonly Dictionary<byte, char> _byteEncoder;
    private readonly Dictionary<char, byte> _byteDecoder;

    public int VocabSize => _vocab.Count;

    public BpeTokenizer(string tokenizerPath)
    {
        if (!File.Exists(tokenizerPath))
            throw new FileNotFoundException("Tokenizer file not found.", tokenizerPath);

        var json = File.ReadAllText(tokenizerPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _vocab = ParseVocabulary(root);
        _reverseVocab = _vocab.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        _merges = ParseMerges(root);
        _mergeRanks = new Dictionary<(string, string), int>();
        for (int i = 0; i < _merges.Count; i++)
            _mergeRanks[_merges[i]] = i;

        // Build GPT-2 byte-to-unicode mapping for ByteLevel encoding
        (_byteEncoder, _byteDecoder) = BuildByteEncoder();

        // Parse pre-tokenizer regex from tokenizer.json
        _preTokenizeRegex = ParsePreTokenizeRegex(root);
    }

    public int[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var tokenIds = new List<int>();

        // Pre-tokenize using the regex from tokenizer.json
        var matches = _preTokenizeRegex.Matches(text);
        foreach (Match match in matches)
        {
            // Apply ByteLevel encoding: convert each byte to its unicode char
            string word = ByteLevelEncode(match.Value);

            // Apply BPE merges
            var bpeTokens = ApplyBpe(word);
            foreach (var token in bpeTokens)
            {
                if (_vocab.TryGetValue(token, out int id))
                    tokenIds.Add(id);
            }
        }

        return tokenIds.ToArray();
    }

    public string Decode(int[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var sb = new StringBuilder();
        foreach (int id in ids)
        {
            if (_reverseVocab.TryGetValue(id, out var token))
                sb.Append(token);
        }
        return ByteLevelDecode(sb.ToString());
    }

    private string ByteLevelEncode(string text)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
        var sb = new StringBuilder(utf8Bytes.Length);
        foreach (byte b in utf8Bytes)
            sb.Append(_byteEncoder[b]);
        return sb.ToString();
    }

    private string ByteLevelDecode(string encoded)
    {
        var bytes = new List<byte>(encoded.Length);
        foreach (char c in encoded)
        {
            if (_byteDecoder.TryGetValue(c, out byte b))
                bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private List<string> ApplyBpe(string word)
    {
        if (word.Length <= 1)
            return [word];

        var symbols = word.Select(c => c.ToString()).ToList();
        while (symbols.Count > 1)
        {
            int bestRank = int.MaxValue;
            int bestIndex = -1;
            for (int i = 0; i < symbols.Count - 1; i++)
            {
                var pair = (symbols[i], symbols[i + 1]);
                if (_mergeRanks.TryGetValue(pair, out int rank) && rank < bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                }
            }
            if (bestIndex < 0) break;
            symbols[bestIndex] = symbols[bestIndex] + symbols[bestIndex + 1];
            symbols.RemoveAt(bestIndex + 1);
        }
        return symbols;
    }

    /// <summary>
    /// Builds the GPT-2 byte-to-unicode character mapping used by ByteLevel tokenizers.
    /// </summary>
    private static (Dictionary<byte, char>, Dictionary<char, byte>) BuildByteEncoder()
    {
        var byteToChar = new Dictionary<byte, char>();
        var charToByte = new Dictionary<char, byte>();

        int n = 0;
        for (int b = 0; b < 256; b++)
        {
            if ((b >= '!' && b <= '~') ||
                (b >= 0xA1 && b <= 0xAC) ||
                (b >= 0xAE && b <= 0xFF))
            {
                byteToChar[(byte)b] = (char)b;
                charToByte[(char)b] = (byte)b;
            }
            else
            {
                char c = (char)(256 + n);
                byteToChar[(byte)b] = c;
                charToByte[c] = (byte)b;
                n++;
            }
        }
        return (byteToChar, charToByte);
    }

    private static Regex ParsePreTokenizeRegex(JsonElement root)
    {
        if (root.TryGetProperty("pre_tokenizer", out var preTokenizer))
        {
            if (preTokenizer.TryGetProperty("type", out var type) &&
                type.GetString() == "Sequence" &&
                preTokenizer.TryGetProperty("pretokenizers", out var pretokenizers))
            {
                foreach (var pt in pretokenizers.EnumerateArray())
                {
                    if (pt.TryGetProperty("type", out var ptType) &&
                        ptType.GetString() == "Split" &&
                        pt.TryGetProperty("pattern", out var pattern) &&
                        pattern.TryGetProperty("Regex", out var regex))
                    {
                        return new Regex(regex.GetString()!, RegexOptions.Compiled);
                    }
                }
            }
            else if (preTokenizer.TryGetProperty("pattern", out var directPattern) &&
                     directPattern.TryGetProperty("Regex", out var directRegex))
            {
                return new Regex(directRegex.GetString()!, RegexOptions.Compiled);
            }
        }
        return DefaultPreTokenizePattern();
    }

    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+", RegexOptions.Compiled)]
    private static partial Regex DefaultPreTokenizePattern();

    private static Dictionary<string, int> ParseVocabulary(JsonElement root)
    {
        var vocab = new Dictionary<string, int>();
        if (root.TryGetProperty("model", out var model) && model.TryGetProperty("vocab", out var vocabElement))
        {
            foreach (var entry in vocabElement.EnumerateObject())
                vocab[entry.Name] = entry.Value.GetInt32();
            return vocab;
        }
        if (root.TryGetProperty("vocab", out var topVocab))
        {
            foreach (var entry in topVocab.EnumerateObject())
                vocab[entry.Name] = entry.Value.GetInt32();
            return vocab;
        }
        throw new InvalidDataException("Could not find vocabulary in tokenizer.json.");
    }

    private static List<(string, string)> ParseMerges(JsonElement root)
    {
        var merges = new List<(string, string)>();
        JsonElement? mergesElement = null;
        if (root.TryGetProperty("model", out var model) && model.TryGetProperty("merges", out var modelMerges))
            mergesElement = modelMerges;
        else if (root.TryGetProperty("merges", out var topMerges))
            mergesElement = topMerges;

        if (mergesElement is null) return merges;

        foreach (var mergeEntry in mergesElement.Value.EnumerateArray())
        {
            if (mergeEntry.ValueKind == JsonValueKind.String)
            {
                var mergeStr = mergeEntry.GetString();
                if (mergeStr is null) continue;
                var parts = mergeStr.Split(' ', 2);
                if (parts.Length == 2)
                    merges.Add((parts[0], parts[1]));
            }
            else if (mergeEntry.ValueKind == JsonValueKind.Array && mergeEntry.GetArrayLength() == 2)
            {
                var a = mergeEntry[0].GetString();
                var b = mergeEntry[1].GetString();
                if (a is not null && b is not null)
                    merges.Add((a, b));
            }
        }
        return merges;
    }
}
