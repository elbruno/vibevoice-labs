// =============================================================================
// VibeVoice Labs — Scenario 4: Microsoft.Extensions.AI + VibeVoice
// =============================================================================
// An AI Agent that Speaks!
//
// Pattern: Ask a question → AI generates text → VibeVoice speaks it
//
// Prerequisites:
//   1. Set the OPENAI_API_KEY environment variable
//   2. Start the VibeVoice Python backend (default: http://localhost:5100)
//   3. dotnet run
// =============================================================================

using Microsoft.Extensions.AI;
using OpenAI;
using VoiceLabs.MEAI.Plugins;

// =============================================================================
// STEP 1: Configure Microsoft.Extensions.AI Chat Client with OpenAI
// =============================================================================
// We use the IChatClient abstraction from Microsoft.Extensions.AI.
// The API key is read from the OPENAI_API_KEY environment variable.

Console.WriteLine("🧠 VibeVoice Labs — Scenario 4: An AI Agent that Speaks!");
Console.WriteLine("=========================================================\n");

var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException(
        "❌ Please set the OPENAI_API_KEY environment variable.");

// --- Default: OpenAI gpt-4o-mini (fast & affordable) ---
var chatClient = new OpenAIClient(openAiKey)
    .GetChatClient("gpt-4o-mini")
    .AsIChatClient();

// --- Alternative: OpenAI gpt-4o (more capable) ---
// var chatClient = new OpenAIClient(openAiKey)
//     .GetChatClient("gpt-4o")
//     .AsIChatClient();

Console.WriteLine("✅ MEAI IChatClient configured with OpenAI (gpt-4o-mini)\n");

// =============================================================================
// STEP 2: Create the VibeVoice Speech Plugin
// =============================================================================
// The SpeechPlugin calls the VibeVoice Python backend HTTP API.
// Change the base URL if your backend runs on a different port.

var backendUrl = Environment.GetEnvironmentVariable("VIBEVOICE_BACKEND_URL")
    ?? "http://localhost:5100";

Console.WriteLine($"🔌 Backend URL: {backendUrl}");

var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
var speechPlugin = new SpeechPlugin(httpClient, backendUrl);

Console.WriteLine("✅ SpeechPlugin configured\n");

// =============================================================================
// STEP 3: Get a User Prompt
// =============================================================================
// Type your own question or press Enter to use the default.

const string defaultPrompt = "Explain in 2 sentences why the sky is blue.";

Console.Write($"💬 Enter your question (or press Enter for default):\n   > ");
var userPrompt = Console.ReadLine();
if (string.IsNullOrWhiteSpace(userPrompt))
{
    userPrompt = defaultPrompt;
    Console.WriteLine($"   Using default: \"{userPrompt}\"");
}

// =============================================================================
// STEP 4: Generate a Text Response with Microsoft.Extensions.AI
// =============================================================================
// We invoke the chat client with a system message and user message.
// The system message tells the model to keep answers short (ideal for TTS).

Console.WriteLine("\n🤖 Asking the AI...\n");

var systemMessage = "You are a friendly, concise assistant. " +
                    "Keep answers under 3 sentences so they sound great when read aloud.";

var messages = new List<ChatMessage>
{
    new(ChatRole.System, systemMessage),
    new(ChatRole.User, userPrompt)
};

var response = await chatClient.GetResponseAsync(messages);

var aiResponse = response.Text ?? "I have no response.";

Console.WriteLine("📝 AI Response:");
Console.WriteLine($"   \"{aiResponse}\"\n");

// =============================================================================
// STEP 5: Convert the AI Response to Speech via VibeVoice
// =============================================================================
// We call the SpeechPlugin directly to convert text to audio.

// Pick a voice — change this to any voice returned by GET /api/voices
var voiceId = "en-US-Aria";

// --- Alternative voices ---
// var voiceId = "de-DE-Katja";   // German
// var voiceId = "fr-FR-Denise";  // French
// var voiceId = "en-US-Guy";     // Male English

string audioPath;
try
{
    audioPath = await speechPlugin.SpeakAsync(aiResponse, voiceId);
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"\n❌ Could not reach the VibeVoice backend at {backendUrl}");
    Console.WriteLine($"   Error: {ex.Message}");
    Console.WriteLine("   Make sure the Python backend is running (see README).");
    return;
}

// =============================================================================
// STEP 6: Play the Audio (optional)
// =============================================================================
// On Windows we can launch the default player. On other OSes, adjust the command.

Console.WriteLine("\n🔊 Attempting to play audio...");

try
{
    // Windows
    if (OperatingSystem.IsWindows())
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(audioPath) { UseShellExecute = true });
    }
    // macOS
    else if (OperatingSystem.IsMacOS())
    {
        System.Diagnostics.Process.Start("afplay", audioPath);
    }
    // Linux
    else
    {
        System.Diagnostics.Process.Start("aplay", audioPath);
    }

    Console.WriteLine("   ▶️  Playing...");
}
catch
{
    Console.WriteLine($"   ⚠️  Auto-play not available. Open the file manually:");
    Console.WriteLine($"       {audioPath}");
}

Console.WriteLine("\n🎉 Done! Your AI agent just spoke.");
