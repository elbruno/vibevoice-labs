var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.VoiceLabs_Api>("backend");

builder.AddProject<Projects.VoiceLabs_ConversationWeb>("frontend")
    .WithReference(backend)
    .WithExternalHttpEndpoints()
    .WaitFor(backend);

builder.Build().Run();
