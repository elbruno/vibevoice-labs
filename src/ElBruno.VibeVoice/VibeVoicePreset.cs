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
    /// Voice presets use the format "en-{Name}_{gender}" matching the KV-cache directory names.
    /// </summary>
    public static string ToVoiceName(this VibeVoicePreset preset) => preset switch
    {
        VibeVoicePreset.Carter => "en-Carter_man",
        VibeVoicePreset.Davis => "en-Davis_man",
        VibeVoicePreset.Emma => "en-Emma_woman",
        VibeVoicePreset.Frank => "en-Frank_man",
        VibeVoicePreset.Grace => "en-Grace_woman",
        VibeVoicePreset.Mike => "en-Mike_man",
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
