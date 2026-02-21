// =============================================================================
// VibeVoiceTokenizer — BPE Tokenizer for ONNX Inference
// =============================================================================
// Loads a HuggingFace-format tokenizer.json and performs Byte-Pair Encoding
// tokenization entirely in C#.
//
// NOTE: This is a simplified BPE implementation sufficient for inference.
// Production use should consider the full HuggingFace tokenizer specification,
// including pre-tokenization rules, normalizers, post-processors, and edge
// cases around Unicode handling. For a full-featured C# tokenizer, consider
// Microsoft.ML.Tokenizers or the SharpToken library.
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;

namespace VoiceLabs.OnnxNative.Pipeline;

/// <summary>
/// Byte-Pair Encoding tokenizer that loads from HuggingFace tokenizer.json format.
/// </summary>
public sealed partial class VibeVoiceTokenizer
{
    private readonly Dictionary<string, int> _vocab;
    private readonly Dictionary<int, string> _reverseVocab;
    private readonly List<(string, string)> _merges;
    private readonly Dictionary<(string, string), int> _mergeRanks;

    // Special token IDs
    private readonly int _bosTokenId;
    private readonly int _eosTokenId;
    private readonly int _padTokenId;

    /// <summary>Vocabulary size.</summary>
    public int VocabSize => _vocab.Count;

    /// <summary>
    /// Loads and parses a HuggingFace tokenizer.json file.
    /// </summary>
    /// <param name="tokenizerPath">Path to the tokenizer.json file.</param>
    /// <exception cref="FileNotFoundException">Thrown when tokenizer.json is missing.</exception>
    /// <exception cref="InvalidDataException">Thrown when tokenizer.json has unexpected format.</exception>
    public VibeVoiceTokenizer(string tokenizerPath)
    {
        if (!File.Exists(tokenizerPath))
            throw new FileNotFoundException("Tokenizer file not found.", tokenizerPath);

        var json = File.ReadAllText(tokenizerPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse vocabulary
        _vocab = ParseVocabulary(root);
        _reverseVocab = _vocab.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        // Parse BPE merges
        _merges = ParseMerges(root);
        _mergeRanks = new Dictionary<(string, string), int>();
        for (int i = 0; i < _merges.Count; i++)
        {
            _mergeRanks[_merges[i]] = i;
        }

        // Resolve special tokens
        _bosTokenId = ResolveSpecialToken(root, "bos_token", "<|startoftext|>", "<s>", "<bos>");
        _eosTokenId = ResolveSpecialToken(root, "eos_token", "<|endoftext|>", "</s>", "<eos>");
        _padTokenId = ResolveSpecialToken(root, "pad_token", "<|padding|>", "<pad>");
    }

    /// <summary>
    /// Encodes text into a sequence of token IDs with BOS/EOS wrapping.
    /// </summary>
    /// <param name="text">Input text to tokenize.</param>
    /// <returns>Array of token IDs including BOS and EOS tokens.</returns>
    public int[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
            return [_bosTokenId, _eosTokenId];

        // Pre-tokenize: split on whitespace and punctuation boundaries
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
                    // Fallback: encode unknown tokens byte-by-byte
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

    /// <summary>
    /// Decodes a sequence of token IDs back to text.
    /// </summary>
    /// <param name="ids">Array of token IDs.</param>
    /// <returns>Decoded text string.</returns>
    public string Decode(int[] ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var tokens = new List<string>();
        foreach (int id in ids)
        {
            // Skip special tokens in decoded output
            if (id == _bosTokenId || id == _eosTokenId || id == _padTokenId)
                continue;

            if (_reverseVocab.TryGetValue(id, out var token))
            {
                // BPE tokens often use Ġ (U+0120) to represent a leading space
                tokens.Add(token.Replace('Ġ', ' '));
            }
        }

        return string.Join("", tokens).Trim();
    }

    // =========================================================================
    // BPE Implementation
    // =========================================================================

    /// <summary>
    /// Applies Byte-Pair Encoding merges to a word.
    /// </summary>
    private List<string> ApplyBpe(string word)
    {
        if (word.Length <= 1)
            return [word];

        // Initialize: each character is its own token
        var symbols = word.Select(c => c.ToString()).ToList();

        while (symbols.Count > 1)
        {
            // Find the highest-priority (lowest-rank) merge pair
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

            if (bestIndex < 0)
                break; // No more merges apply

            // Apply the merge
            string merged = symbols[bestIndex] + symbols[bestIndex + 1];
            symbols[bestIndex] = merged;
            symbols.RemoveAt(bestIndex + 1);
        }

        return symbols;
    }

    /// <summary>
    /// Pre-tokenizes text by splitting on whitespace and punctuation boundaries.
    /// Prepends Ġ to tokens that follow whitespace (GPT-2 / BPE convention).
    /// </summary>
    private static List<string> PreTokenize(string text)
    {
        var words = new List<string>();

        // GPT-2 style pre-tokenization: split on whitespace boundaries
        // Each word that follows whitespace gets a leading Ġ (U+0120)
        var matches = PreTokenizePattern().Matches(text);
        foreach (Match match in matches)
        {
            words.Add(match.Value);
        }

        if (words.Count == 0 && !string.IsNullOrEmpty(text))
            words.Add(text);

        return words;
    }

    // GPT-2 style regex for pre-tokenization
    [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+", RegexOptions.Compiled)]
    private static partial Regex PreTokenizePattern();

    // =========================================================================
    // Tokenizer JSON Parsing
    // =========================================================================

    /// <summary>Extracts vocabulary from tokenizer.json.</summary>
    private static Dictionary<string, int> ParseVocabulary(JsonElement root)
    {
        var vocab = new Dictionary<string, int>();

        // Try standard HuggingFace tokenizer.json structure: model.vocab
        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("vocab", out var vocabElement))
        {
            foreach (var entry in vocabElement.EnumerateObject())
            {
                vocab[entry.Name] = entry.Value.GetInt32();
            }
            return vocab;
        }

        // Fallback: try top-level "vocab" key
        if (root.TryGetProperty("vocab", out var topVocab))
        {
            foreach (var entry in topVocab.EnumerateObject())
            {
                vocab[entry.Name] = entry.Value.GetInt32();
            }
            return vocab;
        }

        throw new InvalidDataException(
            "Could not find vocabulary in tokenizer.json. " +
            "Expected 'model.vocab' or 'vocab' key in the JSON structure.");
    }

    /// <summary>Extracts BPE merge rules from tokenizer.json.</summary>
    private static List<(string, string)> ParseMerges(JsonElement root)
    {
        var merges = new List<(string, string)>();

        JsonElement? mergesElement = null;

        if (root.TryGetProperty("model", out var model) &&
            model.TryGetProperty("merges", out var modelMerges))
        {
            mergesElement = modelMerges;
        }
        else if (root.TryGetProperty("merges", out var topMerges))
        {
            mergesElement = topMerges;
        }

        if (mergesElement is null)
            return merges; // No merges found — may be a non-BPE tokenizer

        foreach (var mergeEntry in mergesElement.Value.EnumerateArray())
        {
            var mergeStr = mergeEntry.GetString();
            if (mergeStr is null) continue;

            var parts = mergeStr.Split(' ', 2);
            if (parts.Length == 2)
            {
                merges.Add((parts[0], parts[1]));
            }
        }

        return merges;
    }

    /// <summary>
    /// Resolves a special token ID by checking added_tokens, then falling back to
    /// known token strings in the vocabulary.
    /// </summary>
    private int ResolveSpecialToken(JsonElement root, string tokenName, params string[] candidates)
    {
        // Check added_tokens section
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

        // Fall back to checking vocabulary directly
        foreach (var candidate in candidates)
        {
            if (_vocab.TryGetValue(candidate, out int id))
                return id;
        }

        // Last resort: use 0 (typically <pad> or <unk>)
        return 0;
    }
}
