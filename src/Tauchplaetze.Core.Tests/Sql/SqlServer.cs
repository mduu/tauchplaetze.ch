using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http.Resilience;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using System.Runtime.CompilerServices;
using DotNet.Testcontainers.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.Features.Mail.Commands;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Core.Features.Translations;
using Tauchplaetze.Core.Features.IndexNow;
using Testcontainers.MsSql;

namespace Tauchplaetze.Core.Tests.Sql;

/// <summary>Ein SQL Server im Container für den ganzen Testlauf. Das Schema entsteht über die echten Migrationen.</summary>
public sealed class SqlServer : IAsyncLifetime
{
    public const string Collection = "SQL Server";

    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static readonly string[] SeededTables = ["AspNetRoles", "__EFMigrationsHistory"];

    internal const string IndexNowKey = "0123456789abcdef0123456789abcdef";

    private MsSqlContainer? container;

    private IHost? host;

    /// <summary>Der Inhalt der Versionen liegt in den Tests im Speicher. Die Datenbank ist echt.</summary>
    internal InMemoryRevisionStore Revisions { get; } = new();

    internal InMemoryRawDataStore RawData { get; } = new();

    internal InMemoryMediaStore Media { get; } = new();

    internal InMemoryRunLock RunLock { get; } = new();

    internal FixedTime Time { get; } = new(FixedTime.SpikeDay);

    internal InMemoryMailSender Mail { get; } = new();

    internal CollectingLogger Logs { get; } = new();

    internal InMemoryMailQueue MailQueue => mailQueue ??= new InMemoryMailQueue(Time);

    private InMemoryMailQueue? mailQueue;

    /// <summary>Kein Test ruft eine echte Quelle auf (architecture.md, Testing).</summary>
    internal FakeSources Sources { get; } = new();

    internal FakeTranslator Translator { get; } = new();

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
            ["Accounts:RegistrationOpen"] = "true",
            ["PublicSite:BaseUri"] = "https://tauchplaetze.test",
            ["Content:ImportEnabled"] = "true",
            ["Hydro:Forecast:Username"] = "test-user",
            ["Hydro:Forecast:Password"] = "test-password",
            ["Translation:ApiKey"] = "test-key",
            ["IndexNow:Enabled"] = "true",
            ["IndexNow:Key"] = IndexNowKey,
        });
        builder.AddTauchplaetzeCore();
        builder.Logging.AddProvider(Logs);
        builder.Services.Replace(ServiceDescriptor.Singleton<IRevisionStore>(Revisions));
        builder.Services.Replace(ServiceDescriptor.Singleton<IRawDataStore>(RawData));
        builder.Services.Replace(ServiceDescriptor.Singleton<IMediaStore>(Media));
        builder.Services.Replace(ServiceDescriptor.Singleton<IRunLock>(RunLock));
        builder.Services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time));
        builder.Services.Replace(ServiceDescriptor.Singleton<IMailSender>(Mail));
        builder.Services.Replace(ServiceDescriptor.Singleton<IMailQueue>(MailQueue));
        builder.Services.Replace(ServiceDescriptor.Singleton<ITextTranslator>(Translator));
        builder.Services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => Sources));
        builder.Services.PostConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);
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

    /// <summary>Sendet alle wartenden Mails der Queue, wie es der Worker tut.</summary>
    internal async Task DeliverMail()
    {
        while (true)
        {
            await using var scope = Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ProcessMailQueueCommand, ProcessMailQueueResult>>()
                .Handle(new ProcessMailQueueCommand(), TestContext.Current.CancellationToken);
            if (result.Idle)
            {
                return;
            }
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
        Media.Clear();
        Mail.Sent.Clear();
        MailQueue.Clear();
        Sources.Failing = null;
        Sources.Overrides.Clear();
        Sources.Requests.Clear();
        Sources.Posts.Clear();
        Sources.Statuses.Clear();
        Services.GetRequiredService<IndexNowOutbox>().Take();
        Sources.OnRequest = null;
        Translator.Reset();
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

    protected async Task<TResult> Command<TCommand, TResult>(TCommand command, CancellationToken? ct = null)
    {
        await using var scope = sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().Handle(command, ct ?? TestContext.Current.CancellationToken);
    }

    protected async Task<TResult> Query<TQuery, TResult>(TQuery query)
    {
        await using var scope = sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().Handle(query, TestContext.Current.CancellationToken);
    }

    /// <summary>Registriert ein Konto und bestätigt die E-Mail über den Link aus der Mail.</summary>
    protected async Task<string> RegisterConfirmed(string email, string displayName)
    {
        await Command<RegisterCommand, AccountResult>(new RegisterCommand(email, "Felswand-auf-25m", displayName, "de", "/c?user={user}&token={token}"));
        var link = new Uri(sql.Mail.LinkTo(email));
        var query = link.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        await Command<ConfirmEmailCommand, AccountResult>(new ConfirmEmailCommand(query["user"], query["token"]));
        return query["user"];
    }

    /// <summary>Legt Riff Williams am Zürichsee an, freigegeben von <paramref name="moderatorId"/>. Die Gewässer müssen geseedet sein.</summary>
    protected async Task<Guid> AddRiffWilliams(string moderatorId)
    {
        var waterBodies = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));
        var lake = waterBodies.Single(w => w.Slug == "zuerichsee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(
            new ProposeDiveSiteCommand(moderatorId, "Riff Williams", lake, 47.2849, 8.6085, "Herrliberg", "ZH", null));
        return result.DiveSiteId ?? throw new InvalidOperationException(string.Join(", ", result.Errors));
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
