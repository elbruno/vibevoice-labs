using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ElBruno.VibeVoice;

/// <summary>
/// Manages model file downloading from HuggingFace, validation, and caching.
/// </summary>
internal sealed class ModelManager
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    // Files required for inference
    private static readonly string[] RequiredFiles =
    [
        "text_encoder.onnx",
        "diffusion_step.onnx",
        "acoustic_decoder.onnx",
        "tokenizer.json"
    ];

    // Additional files to download (optional but recommended)
    private static readonly string[] OptionalFiles =
    [
        "config.json",
        "voices/manifest.json"
    ];

    // Default voice presets
    private static readonly string[] VoiceNames =
    [
        "Carter", "Davis", "Emma", "Frank", "Grace", "Mike"
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

        // Add voice preset files
        foreach (var voice in VoiceNames)
        {
            filesToDownload.Add($"voices/{voice}/speaker_embedding.npy");
        }

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
                response.EnsureSuccessStatusCode();

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
                // Optional file not found — skip silently
                continue;
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
