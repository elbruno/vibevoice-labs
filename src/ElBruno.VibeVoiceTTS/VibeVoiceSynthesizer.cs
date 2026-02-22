using ElBruno.VibeVoiceTTS.Pipeline;

namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// VibeVoice text-to-speech synthesizer using ONNX Runtime.
/// Handles model management (auto-download) and inference.
/// </summary>
public sealed class VibeVoiceSynthesizer : IVibeVoiceSynthesizer
{
    private readonly VibeVoiceOptions _options;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private OnnxInferencePipeline? _pipeline;
    private bool _disposed;

    /// <summary>
    /// Creates a synthesizer with default options (models stored in shared OS cache).
    /// </summary>
    public VibeVoiceSynthesizer() : this(new VibeVoiceOptions()) { }

    /// <summary>
    /// Creates a synthesizer with the specified options.
    /// </summary>
    public VibeVoiceSynthesizer(VibeVoiceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string ModelPath => _options.GetEffectiveModelPath();

    /// <inheritdoc/>
    public bool IsModelAvailable => ModelManager.IsModelAvailable(ModelPath);

    /// <inheritdoc/>
    public async Task EnsureModelAvailableAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsModelAvailable)
        {
            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = "Model files already available."
            });
            return;
        }

        await ModelManager.EnsureModelAvailableAsync(
            ModelPath,
            _options.HuggingFaceRepo,
            progress,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<float[]> GenerateAudioAsync(
        string text,
        VibeVoicePreset voice,
        CancellationToken cancellationToken = default)
    {
        return GenerateAudioAsync(text, voice.ToVoiceName(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<float[]> GenerateAudioAsync(
        string text,
        string voiceName,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceName);

        // Resolve short preset names (e.g. "Carter") to internal names (e.g. "en-Carter_man")
        var resolvedName = ResolveVoiceName(voiceName);

        // Auto-download voice if not available on disk
        if (!ModelManager.IsVoiceAvailable(ModelPath, resolvedName))
        {
            await ModelManager.EnsureVoiceAvailableAsync(
                ModelPath, _options.HuggingFaceRepo, resolvedName, null, cancellationToken);

            // Reload pipeline to pick up newly downloaded voice
            await ReloadPipelineAsync();
        }

        var pipeline = await GetOrCreatePipelineAsync();

        // Run inference on a thread pool thread to avoid blocking
        return await Task.Run(() => pipeline.GenerateAudio(text, resolvedName), cancellationToken);
    }

    /// <inheritdoc/>
    public void SaveWav(string path, float[] audioSamples)
    {
        Utils.AudioWriter.SaveWav(path, audioSamples, _options.SampleRate);
    }

    /// <inheritdoc/>
    public string[] GetAvailableVoices()
    {
        var pipeline = _pipeline;
        if (pipeline is not null)
        {
            return pipeline.GetAvailableVoices()
                .Select(internalName =>
                {
                    if (VibeVoicePresetExtensions.TryParseVoice(internalName, out var preset))
                        return preset.ToString();
                    return internalName;
                })
                .ToArray();
        }

        // No pipeline loaded — check disk for downloaded voices
        return Enum.GetValues<VibeVoicePreset>()
            .Where(p => ModelManager.IsVoiceAvailable(ModelPath, p.ToVoiceName()))
            .Select(p => p.ToString())
            .ToArray();
    }

    /// <inheritdoc/>
    public VoiceInfo[] GetAvailableVoiceDetails()
    {
        var pipeline = _pipeline;
        if (pipeline is not null)
        {
            return pipeline.GetAvailableVoices()
                .Select(internalName =>
                {
                    if (VibeVoicePresetExtensions.TryParseVoice(internalName, out var preset))
                        return preset.ToVoiceInfo();

                    var parts = internalName.Split('-', 2);
                    var lang = parts.Length > 1 ? parts[0] : "unknown";
                    var rest = parts.Length > 1 ? parts[1] : internalName;
                    var nameParts = rest.Split('_', 2);
                    var name = nameParts[0];
                    var gender = nameParts.Length > 1 ? nameParts[1] : "unknown";
                    return new VoiceInfo(name, internalName, lang, gender);
                })
                .ToArray();
        }

        // No pipeline loaded — check disk for downloaded voices
        return Enum.GetValues<VibeVoicePreset>()
            .Where(p => ModelManager.IsVoiceAvailable(ModelPath, p.ToVoiceName()))
            .Select(p => p.ToVoiceInfo())
            .ToArray();
    }

    /// <inheritdoc/>
    public string[] GetSupportedVoices()
    {
        return Enum.GetNames<VibeVoicePreset>();
    }

    /// <inheritdoc/>
    public VoiceInfo[] GetSupportedVoiceDetails()
    {
        return Enum.GetValues<VibeVoicePreset>()
            .Select(p => p.ToVoiceInfo())
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task EnsureVoiceAvailableAsync(
        string voiceName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceName);

        var resolvedName = ResolveVoiceName(voiceName);

        await ModelManager.EnsureVoiceAvailableAsync(
            ModelPath, _options.HuggingFaceRepo, resolvedName, progress, cancellationToken);

        // Reload pipeline to pick up newly downloaded voice
        await ReloadPipelineAsync();
    }

    /// <summary>
    /// Resolves a voice name, mapping short enum names (e.g. "Carter") to internal preset names (e.g. "en-Carter_man").
    /// If the name is already an internal name, returns it unchanged.
    /// </summary>
    internal static string ResolveVoiceName(string voiceName)
    {
        // If it matches a preset enum name, convert to internal name
        if (VibeVoicePresetExtensions.TryParseVoice(voiceName, out var preset))
            return preset.ToVoiceName();

        // Otherwise assume it's already an internal name (e.g. "en-Carter_man")
        return voiceName;
    }

    private async Task<OnnxInferencePipeline> GetOrCreatePipelineAsync()
    {
        if (_pipeline is not null)
            return _pipeline;

        await _initLock.WaitAsync();
        try
        {
            if (_pipeline is not null)
                return _pipeline;

            if (!IsModelAvailable)
                throw new InvalidOperationException(
                    $"Model files not found at '{ModelPath}'. Call EnsureModelAvailableAsync() first or provide a valid model path.");

            _pipeline = new OnnxInferencePipeline(
                ModelPath,
                _options.DiffusionSteps,
                _options.CfgScale,
                _options.Seed,
                _options.ExecutionProvider,
                _options.GpuDeviceId);

            return _pipeline;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ReloadPipelineAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            _pipeline?.Dispose();
            _pipeline = null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipeline?.Dispose();
        _initLock.Dispose();
    }
}
