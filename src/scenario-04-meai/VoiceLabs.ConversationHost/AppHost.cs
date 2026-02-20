var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddUvicornApp("backend", "../backend", "main:app")
    .WithHttpEndpoint(targetPort: 8000, name: "backend-http", env: "PORT")
    .WithEnvironment("OLLAMA_MODEL", builder.Configuration["Backend:OllamaModel"] ?? "llama3.2")
    .WithEnvironment("OLLAMA_BASE_URL", builder.Configuration["Backend:OllamaBaseUrl"] ?? "http://host.docker.internal:11434");

builder.AddProject<Projects.VoiceLabs_ConversationWeb>("frontend")
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WaitFor(backend);

builder.Build().Run();
