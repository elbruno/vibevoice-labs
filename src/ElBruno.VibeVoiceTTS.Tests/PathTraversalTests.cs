using ElBruno.VibeVoiceTTS.Pipeline;

namespace ElBruno.VibeVoiceTTS.Tests;

public class PathTraversalTests
{
    [Fact]
    public void ValidatePathWithinDirectory_AcceptsValidPath()
    {
        var tempDir = Path.GetTempPath();
        var validPath = Path.Combine(tempDir, "subdir", "file.npy");
        // Should not throw
        VoicePresetLoader.ValidatePathWithinDirectory(validPath, tempDir);
    }

    [Fact]
    public void ValidatePathWithinDirectory_RejectsTraversal()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "voices");
        var maliciousPath = Path.Combine(baseDir, "..", "..", "etc", "passwd");

        Assert.Throws<UnauthorizedAccessException>(
            () => VoicePresetLoader.ValidatePathWithinDirectory(maliciousPath, baseDir));
    }

    [Fact]
    public void ValidatePathWithinDirectory_RejectsAbsoluteEscape()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "voices");
        var absolutePath = OperatingSystem.IsWindows() ? @"C:\Windows\System32\config" : "/etc/passwd";

        Assert.Throws<UnauthorizedAccessException>(
            () => VoicePresetLoader.ValidatePathWithinDirectory(absolutePath, baseDir));
    }

    [Fact]
    public void ReadNpyFile_RejectsNonNpyFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 });
            Assert.Throws<InvalidDataException>(() => VoicePresetLoader.ReadNpyFile(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
