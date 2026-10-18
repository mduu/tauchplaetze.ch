using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Data.Entities;

internal class DiveSiteNotice
{
    public Guid Id { get; set; }

    public Guid DiveSiteId { get; set; }

    public NoticeKind Kind { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidUntil { get; set; }

    public int SortOrder { get; set; }

    public List<DiveSiteNoticeTranslation> Translations { get; set; } = [];
}

internal class DiveSiteNoticeTranslation
{
    public Guid NoticeId { get; set; }

    public required string Culture { get; set; }

    public required string Text { get; set; }
}
