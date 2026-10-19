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
var queues = storage.AddQueues("queues");

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.27")
    .WithContainerName("tauchplaetze-mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "ui")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithLifetime(ContainerLifetime.Persistent);
var smtp = mailpit.GetEndpoint("smtp");

var web = builder.AddProject<Projects.Tauchplaetze_Web>("web")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(blobs)
    .WithReference(queues)
    .WithEnvironment("Database__PrepareOnStartup", "true")
    .WithEnvironment("Forecast__RunOnStartup", "true")
    .WithEnvironment("Accounts__RegistrationOpen", "true")
    .WithEnvironment("Mail__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Mail__Port", smtp.Property(EndpointProperty.Port))
    .WaitFor(mailpit)
    .WithExternalHttpEndpoints();
web.WithEnvironment("PublicSite__BaseUri", web.GetEndpoint("https"));

builder.AddProject<Projects.Tauchplaetze_Spike_DataSources>("spike-datasources")
    .WithExplicitStart();

builder.Build().Run();
