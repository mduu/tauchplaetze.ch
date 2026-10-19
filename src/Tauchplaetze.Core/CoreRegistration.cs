using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Contact;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;

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
        var connectionString = builder.Configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection String «{DatabaseConnectionName}» fehlt.");

        builder.Services.AddDbContext<TauchplaetzeDbContext>(options => options.UseTauchplaetzeSqlServer(connectionString));
        builder.EnrichSqlServerDbContext<TauchplaetzeDbContext>();
        builder.AddAzureBlobServiceClient(BlobsConnectionName);
        builder.AddAzureQueueServiceClient(QueuesConnectionName);

        builder.Services.Configure<MailOptions>(builder.Configuration.GetSection(MailOptions.Section));
        builder.Services.Configure<AccountsOptions>(builder.Configuration.GetSection(AccountsOptions.Section));
        builder.Services.Configure<PublicSiteOptions>(builder.Configuration.GetSection(PublicSiteOptions.Section));
        builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection(MediaOptions.Section));
        builder.Services.Configure<ContactOptions>(builder.Configuration.GetSection(ContactOptions.Section));
        builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.Section));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHandlers(typeof(CoreRegistration).Assembly);
        builder.Services.AddCoreModules(typeof(CoreRegistration).Assembly);

        return builder;
    }
}
