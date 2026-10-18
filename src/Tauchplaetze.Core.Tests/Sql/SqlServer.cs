using System.Runtime.CompilerServices;
using DotNet.Testcontainers.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Operations.Commands;
using Testcontainers.MsSql;

namespace Tauchplaetze.Core.Tests.Sql;

/// <summary>Ein SQL Server im Container für den ganzen Testlauf. Das Schema entsteht über die echten Migrationen.</summary>
public sealed class SqlServer : IAsyncLifetime
{
    public const string Collection = "SQL Server";

    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static readonly string[] SeededTables = ["AspNetRoles", "__EFMigrationsHistory"];

    private MsSqlContainer? container;

    private IHost? host;

    /// <summary>Der Inhalt der Versionen liegt in den Tests im Speicher. Die Datenbank ist echt.</summary>
    internal InMemoryRevisionStore Revisions { get; } = new();

    internal InMemoryRawDataStore RawData { get; } = new();

    internal InMemoryRunLock RunLock { get; } = new();

    internal FixedTime Time { get; } = new(FixedTime.SpikeDay);

    /// <summary>Kein Test ruft eine echte Quelle auf (architecture.md, Testing).</summary>
    internal FakeSources Sources { get; } = new();

    public IServiceProvider Services => host?.Services ?? throw new InvalidOperationException("SQL Server läuft nicht. Test ohne [SqlFact]?");

    public async ValueTask InitializeAsync()
    {
        if (!SqlFactAttribute.Runs)
        {
            return;
        }

        container = new MsSqlBuilder(Image).WithName($"tauchplaetze-tests-sqlserver-{Guid.NewGuid().ToString("N")[..8]}").Build();
        await container.StartAsync();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{CoreRegistration.DatabaseConnectionName}"] = ConnectionString(),
            [$"ConnectionStrings:{CoreRegistration.BlobsConnectionName}"] = "UseDevelopmentStorage=true",
        });
        builder.AddTauchplaetzeCore();
        builder.Services.Replace(ServiceDescriptor.Singleton<IRevisionStore>(Revisions));
        builder.Services.Replace(ServiceDescriptor.Singleton<IRawDataStore>(RawData));
        builder.Services.Replace(ServiceDescriptor.Singleton<IRunLock>(RunLock));
        builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
        builder.Services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => Sources));
        host = builder.Build();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<MigrateDatabaseCommand, Unit>>().Handle(new MigrateDatabaseCommand());
    }

    public async ValueTask DisposeAsync()
    {
        host?.Dispose();

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    internal async Task Arrange(Func<TauchplaetzeDbContext, Task> arrange)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>();
        await arrange(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public async Task Reset()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>();
        var tables = dbContext.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .OfType<string>()
            .Distinct()
            .Except(SeededTables)
            .Select(t => $"[{t}]")
            .ToList();

        var sql = string.Concat(tables.Select(t => $"ALTER TABLE {t} NOCHECK CONSTRAINT ALL;"))
                  + string.Concat(tables.Select(t => $"DELETE FROM {t};"))
                  + string.Concat(tables.Select(t => $"ALTER TABLE {t} WITH CHECK CHECK CONSTRAINT ALL;"));
        await dbContext.Database.ExecuteSqlRawAsync(sql);
        Revisions.Clear();
        RawData.Files.Clear();
        Sources.Failing = null;
        Time.Now = FixedTime.SpikeDay;
    }

    private string ConnectionString() =>
        new SqlConnectionStringBuilder(container!.GetConnectionString()) { InitialCatalog = CoreRegistration.DatabaseConnectionName }.ConnectionString;
}

[CollectionDefinition(SqlServer.Collection)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServer>;

/// <summary>Basis für Tests gegen SQL Server. Vor jedem Test sind alle Tabellen leer, ausser den Rollen.</summary>
[Collection(SqlServer.Collection)]
public abstract class SqlTest(SqlServer sql) : IAsyncLifetime
{
    protected SqlServer Sql => sql;

    public async ValueTask InitializeAsync()
    {
        if (SqlFactAttribute.Runs)
        {
            await sql.Reset();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task<TResult> Command<TCommand, TResult>(TCommand command)
    {
        await using var scope = sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().Handle(command, TestContext.Current.CancellationToken);
    }

    protected async Task<TResult> Query<TQuery, TResult>(TQuery query)
    {
        await using var scope = sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().Handle(query, TestContext.Current.CancellationToken);
    }
}

/// <summary>Ein Test gegen SQL Server. Ohne Docker lokal sichtbar übersprungen, in der CI (`CI=true`) nie.</summary>
public sealed class SqlFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(() =>
    {
        try
        {
            return TestcontainersSettings.OS.DockerEndpointAuthConfig is not null;
        }
        catch (Exception)
        {
            return false;
        }
    });

    public SqlFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Runs)
        {
            Skip = "Kein Docker gefunden. SQL-Test übersprungen. In der CI ist das ein Fehler.";
        }
    }

    public static bool InCi => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    public static bool Runs => InCi || DockerAvailable.Value;
}

/// <summary>Eine Theory gegen SQL Server. Gleiche Regeln wie <see cref="SqlFactAttribute"/>.</summary>
public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!SqlFactAttribute.Runs)
        {
            Skip = "Kein Docker gefunden. SQL-Test übersprungen. In der CI ist das ein Fehler.";
        }
    }
}
