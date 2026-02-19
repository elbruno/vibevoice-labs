var builder = DistributedApplication.CreateBuilder(args);

// Python FastAPI backend using VibeVoice TTS
// Uses the shared virtual environment at the repository root (.venv via junction link)
var backend = builder.AddUvicornApp("backend", "../backend", "main:app");

// Blazor frontend with reference to backend
builder.AddProject<Projects.VoiceLabs_Web>("frontend")
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WaitFor(backend);

builder.Build().Run();
