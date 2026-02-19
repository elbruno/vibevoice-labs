var builder = DistributedApplication.CreateBuilder(args);

// Python FastAPI backend using VibeVoice TTS
var backend = builder.AddUvicornApp("backend", "../backend", "main:app")
    .WithHttpEndpoint(port: 5100, env: "PORT")
    .WithExternalHttpEndpoints();

// Blazor frontend with reference to backend
builder.AddProject<Projects.VoiceLabs_Web>("frontend")
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WaitFor(backend);

builder.Build().Run();
