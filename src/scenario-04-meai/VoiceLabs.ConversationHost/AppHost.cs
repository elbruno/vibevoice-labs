var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddUvicornApp("backend", "../backend", "main:app")
    .WithHttpEndpoint(targetPort: 8000, name: "backend-http", env: "PORT");

builder.AddProject<Projects.VoiceLabs_ConversationWeb>("frontend")
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WaitFor(backend);

builder.Build().Run();
