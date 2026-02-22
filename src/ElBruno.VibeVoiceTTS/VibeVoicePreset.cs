namespace ElBruno.VibeVoiceTTS;

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
    private static readonly Dictionary<string, VibeVoicePreset> _internalNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en-Carter_man"] = VibeVoicePreset.Carter,
        ["en-Davis_man"] = VibeVoicePreset.Davis,
        ["en-Emma_woman"] = VibeVoicePreset.Emma,
        ["en-Frank_man"] = VibeVoicePreset.Frank,
        ["en-Grace_woman"] = VibeVoicePreset.Grace,
        ["en-Mike_man"] = VibeVoicePreset.Mike,
    };

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
    /// Returns a <see cref="VoiceInfo"/> with the name, internal name, language, and gender for this preset.
    /// </summary>
    public static VoiceInfo ToVoiceInfo(this VibeVoicePreset preset) => preset switch
    {
        VibeVoicePreset.Carter => new VoiceInfo("Carter", "en-Carter_man", "en", "man"),
        VibeVoicePreset.Davis => new VoiceInfo("Davis", "en-Davis_man", "en", "man"),
        VibeVoicePreset.Emma => new VoiceInfo("Emma", "en-Emma_woman", "en", "woman"),
        VibeVoicePreset.Frank => new VoiceInfo("Frank", "en-Frank_man", "en", "man"),
        VibeVoicePreset.Grace => new VoiceInfo("Grace", "en-Grace_woman", "en", "woman"),
        VibeVoicePreset.Mike => new VoiceInfo("Mike", "en-Mike_man", "en", "man"),
        _ => new VoiceInfo(preset.ToString(), preset.ToString(), "unknown", "unknown")
    };

    /// <summary>
    /// Tries to parse a voice name string (short name like "Carter" or internal name like "en-Carter_man")
    /// into a preset enum value.
    /// </summary>
    public static bool TryParseVoice(string name, out VibeVoicePreset preset)
    {
        // Try short enum name first ("Carter", "Emma")
        if (Enum.TryParse(name, ignoreCase: true, out preset))
            return true;

        // Try internal directory name ("en-Carter_man")
        return _internalNameMap.TryGetValue(name, out preset);
    }
}
