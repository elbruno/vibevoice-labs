namespace ElBruno.VibeVoiceTTS;

/// <summary>
/// Describes a voice preset with its display name, internal identifier, language, and gender.
/// </summary>
/// <param name="Name">Friendly display name (e.g. "Carter"). Use this with GenerateAudioAsync.</param>
/// <param name="InternalName">Internal preset directory name (e.g. "en-Carter_man").</param>
/// <param name="Language">Language code (e.g. "en").</param>
/// <param name="Gender">Voice gender (e.g. "man", "woman").</param>
public sealed record VoiceInfo(string Name, string InternalName, string Language, string Gender);
