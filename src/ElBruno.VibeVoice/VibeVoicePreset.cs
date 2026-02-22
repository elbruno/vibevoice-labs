namespace ElBruno.VibeVoice;

/// <summary>
/// Built-in voice presets for VibeVoice TTS.
/// </summary>
public enum VibeVoicePreset
{
    Carter,
    Davis,
    Emma,
    Frank,
    Grace,
    Mike
}

/// <summary>
/// Extension methods for <see cref="VibeVoicePreset"/>.
/// </summary>
public static class VibeVoicePresetExtensions
{
    /// <summary>
    /// Converts the preset enum to its directory/file name string.
    /// </summary>
    public static string ToVoiceName(this VibeVoicePreset preset) => preset switch
    {
        VibeVoicePreset.Carter => "Carter",
        VibeVoicePreset.Davis => "Davis",
        VibeVoicePreset.Emma => "Emma",
        VibeVoicePreset.Frank => "Frank",
        VibeVoicePreset.Grace => "Grace",
        VibeVoicePreset.Mike => "Mike",
        _ => preset.ToString()
    };

    /// <summary>
    /// Tries to parse a voice name string into a preset enum value.
    /// </summary>
    public static bool TryParseVoice(string name, out VibeVoicePreset preset)
    {
        return Enum.TryParse(name, ignoreCase: true, out preset);
    }
}
