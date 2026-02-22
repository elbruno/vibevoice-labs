using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Manages model file downloading from HuggingFace, validation, and caching.
/// </summary>
internal sealed class ModelManager
{
    private static readonly Lazy<HttpClient> LazyClient = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        // Support HF_TOKEN env var for private/gated repos
        var token = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    });

    private static HttpClient SharedClient => LazyClient.Value;

    // Files required for inference (autoregressive pipeline with KV-cache)
    private static readonly string[] RequiredFiles =
    [
        "lm_with_kv.onnx",
        "lm_with_kv.onnx.data",
        "tts_lm_prefill.onnx",
        "tts_lm_prefill.onnx.data",
        "tts_lm_step.onnx",
        "tts_lm_step.onnx.data",
        "prediction_head.onnx",
        "prediction_head.onnx.data",
        "acoustic_decoder.onnx",
        "acoustic_decoder.onnx.data",
        "acoustic_connector.onnx",
        "acoustic_connector.onnx.data",
        "eos_classifier.onnx",
        "eos_classifier.onnx.data",
        "type_embeddings.npy",
        "tokenizer.json",
        "model_config.json"
    ];

    // Additional files to download (optional but recommended)
    private static readonly string[] OptionalFiles =
    [
        "config.json"
    ];

    // Default voice presets with KV-cache data (downloaded by EnsureModelAvailableAsync)
    private static readonly string[] DefaultVoiceNames = ["en-Carter_man", "en-Emma_woman"];

    // All known voice presets (can be downloaded on demand)
    internal static readonly string[] AllVoiceNames =
    [
        "en-Carter_man", "en-Davis_man", "en-Emma_woman",
        "en-Frank_man", "en-Grace_woman", "en-Mike_man"
    ];

    /// <summary>
    /// Checks whether all required model files exist in the specified directory.
    /// </summary>
    public static bool IsModelAvailable(string modelPath)
    {
        if (!Directory.Exists(modelPath))
            return false;

        return RequiredFiles.All(f => File.Exists(Path.Combine(modelPath, f)));
    }

    /// <summary>
    /// Downloads all model files from HuggingFace if not already present.
    /// </summary>
    public static async Task EnsureModelAvailableAsync(
        string modelPath,
        string huggingFaceRepo,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(modelPath);

        // Build list of files to download
        var filesToDownload = new List<string>();
        filesToDownload.AddRange(RequiredFiles);
        filesToDownload.AddRange(OptionalFiles);

        // Add voice preset KV-cache files
        foreach (var voice in DefaultVoiceNames)
            filesToDownload.AddRange(GetVoiceFiles(voice));

        // Filter out already-downloaded files
        var missingFiles = filesToDownload
            .Where(f => !File.Exists(Path.Combine(modelPath, f)))
            .ToList();

        if (missingFiles.Count == 0)
        {
            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = "All model files already present."
            });
            return;
        }

        // Resolve total size
        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Checking,
            Message = $"Checking {missingFiles.Count} files from {huggingFaceRepo}..."
        });

        long totalBytes = 0;
        var fileSizes = new Dictionary<string, long>();

        foreach (var file in missingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = GetHuggingFaceUrl(huggingFaceRepo, file);
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await SharedClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var fileSize = headResponse.Content.Headers.ContentLength ?? 0;
                fileSizes[file] = fileSize;
                totalBytes += fileSize;
            }
            catch
            {
                fileSizes[file] = 0;
            }
        }

        // Download each file
        long downloadedBytes = 0;
        int fileIndex = 0;

        foreach (var file in missingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileIndex++;

            var localPath = Path.Combine(modelPath, file);
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir))
                Directory.CreateDirectory(localDir);

            var url = GetHuggingFaceUrl(huggingFaceRepo, file);

            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Downloading,
                PercentComplete = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
                BytesDownloaded = downloadedBytes,
                TotalBytes = totalBytes,
                CurrentFile = file,
                Message = $"[{fileIndex}/{missingFiles.Count}] Downloading {file}..."
            });

            try
            {
                using var response = await SharedClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (!RequiredFiles.Contains(file))
                        continue; // Optional file — skip

                    var statusCode = (int)response.StatusCode;
                    var reason = response.StatusCode.ToString();

                    if (statusCode == 401 || statusCode == 403)
                    {
                        throw new InvalidOperationException(
                            $"Access denied ({statusCode} {reason}) downloading '{file}' from " +
                            $"https://huggingface.co/{huggingFaceRepo}. " +
                            "The repository may be private, gated, or not yet created. " +
                            "Options:\n" +
                            "  1. Set the HF_TOKEN environment variable with a valid HuggingFace token\n" +
                            "  2. Export models locally: cd src/scenario-08-onnx-native/export && python export_model.py --output ../models\n" +
                            "  3. Set VibeVoiceOptions.ModelPath to an existing local models directory");
                    }

                    if (statusCode == 404)
                    {
                        throw new InvalidOperationException(
                            $"File '{file}' not found (404) at https://huggingface.co/{huggingFaceRepo}. " +
                            "The model may not be uploaded yet. " +
                            "Export models locally: cd src/scenario-08-onnx-native/export && python export_model.py --output ../models");
                    }

                    throw new HttpRequestException(
                        $"Failed to download '{file}': {statusCode} {reason}");
                }

                var fileSize = response.Content.Headers.ContentLength ?? fileSizes.GetValueOrDefault(file);
                long fileDownloaded = 0;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    fileDownloaded += bytesRead;
                    downloadedBytes += bytesRead;

                    progress?.Report(new DownloadProgress
                    {
                        Stage = DownloadStage.Downloading,
                        PercentComplete = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
                        BytesDownloaded = downloadedBytes,
                        TotalBytes = totalBytes,
                        CurrentFile = file,
                        Message = $"[{fileIndex}/{missingFiles.Count}] {file} — {FormatBytes(fileDownloaded)}/{FormatBytes(fileSize)}"
                    });
                }
            }
            catch (HttpRequestException) when (!RequiredFiles.Contains(file))
            {
                // Optional file network error — skip
                continue;
            }
            catch (InvalidOperationException)
            {
                // Re-throw our custom error messages
                throw;
            }
            catch (HttpRequestException ex) when (RequiredFiles.Contains(file))
            {
                throw new InvalidOperationException(
                    $"Failed to download required file '{file}' from https://huggingface.co/{huggingFaceRepo}: {ex.Message}. " +
                    "Export models locally: cd src/scenario-08-onnx-native/export && python export_model.py --output ../models",
                    ex);
            }
        }

        // Validate
        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Validating,
            PercentComplete = 99,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes,
            Message = "Validating downloaded files..."
        });

        var stillMissing = RequiredFiles.Where(f => !File.Exists(Path.Combine(modelPath, f))).ToList();
        if (stillMissing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Download incomplete. Missing required files: {string.Join(", ", stillMissing)}");
        }

        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Complete,
            PercentComplete = 100,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes,
            Message = "All model files downloaded and validated."
        });
    }

    /// <summary>
    /// Checks whether a specific voice preset is downloaded.
    /// </summary>
    internal static bool IsVoiceAvailable(string modelPath, string voiceInternalName)
    {
        var voiceDir = Path.Combine(modelPath, "voices", voiceInternalName);
        return Directory.Exists(voiceDir) && File.Exists(Path.Combine(voiceDir, "metadata.json"));
    }

    /// <summary>
    /// Downloads a single voice preset from HuggingFace if not already present.
    /// </summary>
    internal static async Task EnsureVoiceAvailableAsync(
        string modelPath,
        string huggingFaceRepo,
        string voiceInternalName,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsVoiceAvailable(modelPath, voiceInternalName))
        {
            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = $"Voice '{voiceInternalName}' already available."
            });
            return;
        }

        var filesToDownload = GetVoiceFiles(voiceInternalName);
        var missingFiles = filesToDownload
            .Where(f => !File.Exists(Path.Combine(modelPath, f)))
            .ToList();

        if (missingFiles.Count == 0)
        {
            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = $"Voice '{voiceInternalName}' already available."
            });
            return;
        }

        // Resolve total size via HEAD requests
        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Checking,
            Message = $"Checking voice '{voiceInternalName}' ({missingFiles.Count} files)..."
        });

        long totalBytes = 0;
        var fileSizes = new Dictionary<string, long>();

        foreach (var file in missingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = GetHuggingFaceUrl(huggingFaceRepo, file);
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await SharedClient.SendAsync(headRequest, cancellationToken);
                if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength is > 0)
                {
                    var size = headResponse.Content.Headers.ContentLength.Value;
                    fileSizes[file] = size;
                    totalBytes += size;
                }
            }
            catch
            {
                // HEAD failed — continue without size info for this file
            }
        }

        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Downloading,
            TotalBytes = totalBytes,
            Message = $"Downloading voice '{voiceInternalName}' ({missingFiles.Count} files, {FormatBytes(totalBytes)})..."
        });

        int fileIndex = 0;
        long downloadedBytes = 0;

        foreach (var file in missingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fileIndex++;

            var localPath = Path.Combine(modelPath, file);
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir))
                Directory.CreateDirectory(localDir);

            var url = GetHuggingFaceUrl(huggingFaceRepo, file);

            progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Downloading,
                PercentComplete = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
                BytesDownloaded = downloadedBytes,
                TotalBytes = totalBytes,
                CurrentFile = file,
                Message = $"[{fileIndex}/{missingFiles.Count}] Downloading {file}..."
            });

            using var response = await SharedClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Failed to download voice file '{file}' from https://huggingface.co/{huggingFaceRepo}: {(int)response.StatusCode} {response.StatusCode}");

            var fileSize = response.Content.Headers.ContentLength ?? fileSizes.GetValueOrDefault(file);
            long fileDownloaded = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                fileDownloaded += bytesRead;
                downloadedBytes += bytesRead;

                progress?.Report(new DownloadProgress
                {
                    Stage = DownloadStage.Downloading,
                    PercentComplete = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
                    BytesDownloaded = downloadedBytes,
                    TotalBytes = totalBytes,
                    CurrentFile = file,
                    Message = $"[{fileIndex}/{missingFiles.Count}] {file} — {FormatBytes(fileDownloaded)}/{FormatBytes(fileSize)}"
                });
            }
        }

        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Complete,
            PercentComplete = 100,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes,
            Message = $"Voice '{voiceInternalName}' downloaded successfully ({FormatBytes(downloadedBytes)})."
        });
    }

    /// <summary>
    /// Returns the list of HuggingFace file paths for a voice preset.
    /// </summary>
    internal static List<string> GetVoiceFiles(string voiceInternalName)
    {
        var files = new List<string>();
        files.Add($"voices/{voiceInternalName}/metadata.json");
        // TTS-LM KV-cache (20 layers)
        for (int i = 0; i < 20; i++)
        {
            files.Add($"voices/{voiceInternalName}/tts_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/tts_kv_value_{i}.npy");
        }
        // LM KV-cache (4 layers)
        for (int i = 0; i < 4; i++)
        {
            files.Add($"voices/{voiceInternalName}/lm_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/lm_kv_value_{i}.npy");
        }
        // Negative path
        files.Add($"voices/{voiceInternalName}/negative/tts_lm_hidden.npy");
        for (int i = 0; i < 20; i++)
        {
            files.Add($"voices/{voiceInternalName}/negative/tts_kv_key_{i}.npy");
            files.Add($"voices/{voiceInternalName}/negative/tts_kv_value_{i}.npy");
        }
        // Hidden states
        files.Add($"voices/{voiceInternalName}/tts_lm_hidden.npy");
        files.Add($"voices/{voiceInternalName}/lm_hidden.npy");
        return files;
    }

    private static string GetHuggingFaceUrl(string repo, string file)
    {
        return $"https://huggingface.co/{repo}/resolve/main/{file}";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
