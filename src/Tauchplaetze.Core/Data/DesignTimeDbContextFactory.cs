using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Tauchplaetze.Core.Data;

/// <remarks>Nur für `dotnet ef migrations add`. Die Tools brauchen keine laufende Datenbank.</remarks>
internal class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TauchplaetzeDbContext>
{
    public TauchplaetzeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TauchplaetzeDbContext>();
        options.UseTauchplaetzeSqlServer("Server=localhost;Database=tauchplaetze;Trusted_Connection=True");
        return new TauchplaetzeDbContext(options.Options);
    }
}
