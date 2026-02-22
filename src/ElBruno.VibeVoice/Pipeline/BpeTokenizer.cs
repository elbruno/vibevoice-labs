using System.Text.Json;
using System.Text.RegularExpressions;

namespace ElBruno.VibeVoice.Pipeline;

/// <summary>
/// Byte-Pair Encoding tokenizer that loads from HuggingFace tokenizer.json format.
/// </summary>
internal sealed partial class BpeTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _reverseVocab;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<(string, string), int> _mergeRanks;
    private readonly int _bosTokenId;
    private readonly int _eosTokenId;
    private readonly int _padTokenId;

    public int VocabSize => _vocab.Count;

    public BpeTokenizer(string tokenizerPath)
    {
        if (!File.Exists(tokenizerPath))
            throw new FileNotFoundException("Tokenizer file not found.", tokenizerPath);

        var json = File.ReadAllText(tokenizerPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _vocab = ParseVocabulary(root);
        _reverseVocab = _vocab.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        _merges = ParseMerges(root);
        _mergeRanks = new Dictionary<(string, string), int>();
        for (int i = 0; i < _merges.Count; i++)
            _mergeRanks[_merges[i]] = i;

        _bosTokenId = ResolveSpecialToken(root, "bos_token", "<|startoftext|>", "<s>", "<bos>");
        _eosTokenId = ResolveSpecialToken(root, "eos_token", "<|endoftext|>", "</s>", "<eos>");
        _padTokenId = ResolveSpecialToken(root, "pad_token", "<|padding|>", "<pad>");
    }

    public int[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            return [_bosTokenId, _eosTokenId];

        var words = PreTokenize(text);
        var tokenIds = new List<int> { _bosTokenId };

        foreach (var word in words)
        {
            var bpeTokens = ApplyBpe(word);
            foreach (var token in bpeTokens)
            {
                if (_vocab.TryGetValue(token, out int id))
                {
                    tokenIds.Add(id);
                }
                else
                {
                    foreach (char c in token)
                    {
                        var byteToken = $"<0x{(int)c:X2}>";
                        if (_vocab.TryGetValue(byteToken, out int byteId))
                            tokenIds.Add(byteId);
                    }
                }
            }
        }

        tokenIds.Add(_eosTokenId);
        return tokenIds.ToArray();
    }

    public string Decode(int[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var tokens = new List<string>();
        foreach (int id in ids)
        {
            if (id == _bosTokenId || id == _eosTokenId || id == _padTokenId)
                continue;
            if (_reverseVocab.TryGetValue(id, out var token))
                tokens.Add(token.Replace('Ġ', ' '));
        }
        return string.Join("", tokens).Trim();
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

    private static List<string> PreTokenize(string text)
    {
        var words = new List<string>();
        var matches = PreTokenizePattern().Matches(text);
        foreach (Match match in matches)
            words.Add(match.Value);
        if (words.Count == 0 && !string.IsNullOrEmpty(text))
            words.Add(text);
        return words;
    }

    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+", RegexOptions.Compiled)]
    private static partial Regex PreTokenizePattern();

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
                // Format: "token1 token2"
                var mergeStr = mergeEntry.GetString();
                if (mergeStr is null) continue;
                var parts = mergeStr.Split(' ', 2);
                if (parts.Length == 2)
                    merges.Add((parts[0], parts[1]));
            }
            else if (mergeEntry.ValueKind == JsonValueKind.Array && mergeEntry.GetArrayLength() == 2)
            {
                // Format: ["token1", "token2"]
                var a = mergeEntry[0].GetString();
                var b = mergeEntry[1].GetString();
                if (a is not null && b is not null)
                    merges.Add((a, b));
            }
        }
        return merges;
    }

    private int ResolveSpecialToken(JsonElement root, string tokenName, params string[] candidates)
    {
        if (root.TryGetProperty("added_tokens", out var addedTokens))
        {
            foreach (var token in addedTokens.EnumerateArray())
            {
                if (token.TryGetProperty("content", out var content) &&
                    token.TryGetProperty("id", out var id))
                {
                    var contentStr = content.GetString();
                    if (contentStr == tokenName || candidates.Contains(contentStr))
                        return id.GetInt32();
                }
            }
        }
        foreach (var candidate in candidates)
        {
            if (_vocab.TryGetValue(candidate, out int id))
                return id;
        }
        return 0;
    }
}
