using Microsoft.EntityFrameworkCore;

namespace Tauchplaetze.Core.Data;

internal static class SqlServerOptions
{
    public static DbContextOptionsBuilder UseTauchplaetzeSqlServer(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, sql => sql.UseNetTopologySuite());
}
