using ElBruno.VibeVoice.Pipeline;

namespace ElBruno.VibeVoice;

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

        var pipeline = await GetOrCreatePipelineAsync();

        // Run inference on a thread pool thread to avoid blocking
        return await Task.Run(() => pipeline.GenerateAudio(text, voiceName), cancellationToken);
    }

    /// <inheritdoc/>
    public void SaveWav(string path, float[] audioSamples)
    {
        Utils.AudioWriter.SaveWav(path, audioSamples, _options.SampleRate);
    }

    /// <inheritdoc/>
    public string[] GetAvailableVoices()
    {
        if (_pipeline is not null)
            return _pipeline.GetAvailableVoices();

        // If pipeline not yet loaded, return the default preset names
        return Enum.GetNames<VibeVoicePreset>();
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
                _options.Seed);

            return _pipeline;
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
