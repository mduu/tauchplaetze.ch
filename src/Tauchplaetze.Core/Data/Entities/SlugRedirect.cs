namespace Tauchplaetze.Core.Data.Entities;

internal class SlugRedirect
{
    public required string OldSlug { get; set; }

    public Guid DiveSiteId { get; set; }
}
