using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tauchplaetze.Core.CrossCutting.Hosting;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Content;
using Tauchplaetze.Core.Features.Contact;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.IndexNow;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Translations;

namespace Tauchplaetze.Core;

/// <summary>Meldet den Core bei einem Host an. Die Fassaden rufen nur diese Methode.</summary>
public static class CoreRegistration
{
    /// <summary>Name des Connection Strings der Datenbank (Aspire-Ressource).</summary>
    public const string DatabaseConnectionName = "tauchplaetze";

    /// <summary>Name des Connection Strings von Blob Storage (Aspire-Ressource).</summary>
    public const string BlobsConnectionName = "blobs";

    /// <summary>Name des Connection Strings der Storage Queues (Aspire-Ressource, Mail-Queue).</summary>
    public const string QueuesConnectionName = "queues";

    /// <summary>Meldet Datenbank, Handler und die Module aller Slices an.</summary>
    public static IHostApplicationBuilder AddTauchplaetzeCore(this IHostApplicationBuilder builder)
    {
        KeyVaultReferences.EnsureResolved(builder.Configuration);

        var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection String «{DatabaseConnectionName}» fehlt.");

        builder.Services.AddDbContext<TauchplaetzeDbContext>(options => options.UseTauchplaetzeSqlServer(connectionString));
        builder.EnrichSqlServerDbContext<TauchplaetzeDbContext>();
        builder.AddAzureBlobServiceClient(BlobsConnectionName);
        builder.AddAzureQueueServiceClient(QueuesConnectionName);

        builder.Services.AddOptions<MailOptions>()
            .Bind(builder.Configuration.GetSection(MailOptions.Section))
            .Validate(o => !o.SendsPasswordWithoutTls, "Mail:UseTls must be true when Mail:UserName is set.")
            .ValidateOnStart();
        builder.Services.Configure<AccountsOptions>(builder.Configuration.GetSection(AccountsOptions.Section));
        builder.Services.Configure<PublicSiteOptions>(builder.Configuration.GetSection(PublicSiteOptions.Section));
        builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection(MediaOptions.Section));
        builder.Services.Configure<ContactOptions>(builder.Configuration.GetSection(ContactOptions.Section));
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.Section));
        builder.Services.Configure<ContentOptions>(builder.Configuration.GetSection(ContentOptions.Section));
        builder.Services.Configure<TranslationOptions>(builder.Configuration.GetSection(TranslationOptions.Section));
        builder.Services.AddOptions<IndexNowOptions>()
            .Bind(builder.Configuration.GetSection(IndexNowOptions.Section))
            .Validate(o => o.HasValidKey, "IndexNow:Key must have 32-128 characters of letters, digits and '-'.")
            .ValidateOnStart();
        BafuForecastOptions.Register(builder.Services, builder.Configuration.GetSection(BafuForecastOptions.Section));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHandlers(typeof(CoreRegistration).Assembly);
        builder.Services.AddCoreModules(typeof(CoreRegistration).Assembly);

        return builder;
    }
}
