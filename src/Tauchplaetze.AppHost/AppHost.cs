var builder = DistributedApplication.CreateBuilder(args);

// Spike Phase 0.5: checks the external data sources for one dive site.
// SQL Server and Azurite follow in Phase 0 (specs/roadmap.md).
builder.AddProject<Projects.Tauchplaetze_Spike_DataSources>("spike-datasources")
    .WithExternalHttpEndpoints();

builder.Build().Run();
