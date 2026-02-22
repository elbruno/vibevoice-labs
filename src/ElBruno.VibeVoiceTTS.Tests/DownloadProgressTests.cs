using ElBruno.VibeVoiceTTS;

namespace ElBruno.VibeVoiceTTS.Tests;

public class DownloadProgressTests
{
    [Fact]
    public void DefaultValues_AreSet()
    {
        var progress = new DownloadProgress();
        Assert.Equal(DownloadStage.Checking, progress.Stage);
        Assert.Equal(0, progress.PercentComplete);
        Assert.Equal(0, progress.BytesDownloaded);
        Assert.Equal(0, progress.TotalBytes);
        Assert.Null(progress.CurrentFile);
        Assert.Null(progress.Message);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var progress = new DownloadProgress
        {
            Stage = DownloadStage.Downloading,
            PercentComplete = 50.5,
            BytesDownloaded = 1024,
            TotalBytes = 2048,
            CurrentFile = "model.onnx",
            Message = "Downloading..."
        };

        Assert.Equal(DownloadStage.Downloading, progress.Stage);
        Assert.Equal(50.5, progress.PercentComplete);
        Assert.Equal(1024, progress.BytesDownloaded);
        Assert.Equal(2048, progress.TotalBytes);
        Assert.Equal("model.onnx", progress.CurrentFile);
        Assert.Equal("Downloading...", progress.Message);
    }

    [Fact]
    public void AllStages_AreDefined()
    {
        var stages = Enum.GetValues<DownloadStage>();
        Assert.Contains(DownloadStage.Checking, stages);
        Assert.Contains(DownloadStage.Downloading, stages);
        Assert.Contains(DownloadStage.Validating, stages);
        Assert.Contains(DownloadStage.Complete, stages);
    }
}
