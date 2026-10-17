var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var database = sql.AddDatabase("tauchplaetze");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume().WithLifetime(ContainerLifetime.Persistent));
var blobs = storage.AddBlobs("blobs");

builder.AddProject<Projects.Tauchplaetze_Web>("web")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(blobs)
    .WithEnvironment("Database__MigrateOnStartup", "true")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Tauchplaetze_Spike_DataSources>("spike-datasources")
    .WithExplicitStart();

builder.Build().Run();
