var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithContainerName("tauchplaetze-sqlserver")
    .WithDataVolume("tauchplaetze-sqlserver-data")
    .WithLifetime(ContainerLifetime.Persistent);
var database = sql.AddDatabase("tauchplaetze");

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator
        .WithContainerName("tauchplaetze-azurite")
        .WithDataVolume("tauchplaetze-azurite-data")
        .WithLifetime(ContainerLifetime.Persistent));
var blobs = storage.AddBlobs("blobs");

builder.AddProject<Projects.Tauchplaetze_Web>("web")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(blobs)
    .WithEnvironment("Database__PrepareOnStartup", "true")
    .WithEnvironment("Forecast__RunOnStartup", "true")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.Tauchplaetze_Spike_DataSources>("spike-datasources")
    .WithExplicitStart();

builder.Build().Run();
