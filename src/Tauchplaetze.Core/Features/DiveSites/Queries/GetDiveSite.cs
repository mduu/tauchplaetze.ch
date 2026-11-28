using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites.Queries;

/// <summary>Ein öffentlicher Platz über seinen Slug, in der Sprache <paramref name="Culture"/>. Ein alter Slug ergibt eine Weiterleitung.</summary>
public record GetDiveSiteQuery(string Slug, string Culture);

internal class GetDiveSiteHandler(TauchplaetzeDbContext dbContext, IMediaStore media, TimeProvider time, ILogger<GetDiveSiteHandler> logger) : IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>
{
    public async Task<DiveSiteLookup> Handle(GetDiveSiteQuery query, CancellationToken ct = default)
    {
        var site = await dbContext.DiveSites
            .AsNoTracking()
            .Where(d => d.Slug == query.Slug)
            .Select(d => new
            {
                d.Id,
                d.Slug,
                d.Name,
                d.Status,
                d.Municipality,
                d.Canton,
                d.Location,
                d.ContentLevel,
                d.UpdatedAt,
                d.Sketch,
                d.HeroMediaId,
                d.OtherNames,
                d.HasDivingBan,
                d.MaxDepth,
                d.Features,
                Ratings = new SiteRatings(d.DifficultyRating, d.WallRating, d.MarineLifeRating, d.WrecksRating, d.CurrentRating, d.SurroundingsRating),
                WaterBody = new { d.WaterBody!.Slug, d.WaterBody.NameDe, d.WaterBody.NameEn, d.WaterBody.NameFr, d.WaterBody.NameIt, d.WaterBody.WaterBodyType, d.WaterBody.Elevation },
            })
            .SingleOrDefaultAsync(ct);

        var today = SwissTime.Today(time);
        var notices = site?.Status == DiveSiteStatus.Published ? await Notices(site.Id, query.Culture, today, ct) : [];
        var entryPoints = site?.Status == DiveSiteStatus.Published ? await EntryPoints(site.Id, query.Culture, ct) : [];
        var susvProjects = site?.Status == DiveSiteStatus.Published ? await SusvProjects(site.Id, query.Culture, ct) : [];
        return site?.Status switch
        {
            DiveSiteStatus.Published => new DiveSiteLookup.Found(new DiveSiteDetails(
                site.Id,
                site.Slug,
                site.Name,
                site.WaterBody.Slug,
                WaterBodyNames.In(query.Culture, site.WaterBody.NameDe, site.WaterBody.NameEn, site.WaterBody.NameFr, site.WaterBody.NameIt),
                site.WaterBody.WaterBodyType,
                site.Municipality,
                site.Canton,
                site.Location.Y,
                site.Location.X,
                site.ContentLevel,
                site.UpdatedAt,
                notices,
                entryPoints,
                site.Ratings,
                Sketch(site.Sketch, query.Culture),
                await Photos(site.Id, query.Culture, ct),
                site.HeroMediaId,
                OtherNameJson.Read(site.OtherNames, logger),
                await Texts(site.Id, query.Culture, ct),
                site.HasDivingBan,
                MountainLakes.Elevation(site.WaterBody.WaterBodyType, site.WaterBody.Elevation),
                ClosedDays(notices, today),
                susvProjects,
                SiteSymbolRules.Of(new SiteSymbolFacts(
                    notices.Where(n => n.Timing == NoticeTiming.Today).Select(n => (NoticeKind?)n.Kind).Max(),
                    site.Ratings.Difficulty, site.MaxDepth, site.Ratings.Wall, site.Ratings.Wrecks, site.Features,
                    site.WaterBody.WaterBodyType, site.WaterBody.Elevation,
                    susvProjects.Select(p => p.Topic).ToHashSet(), entryPoints.Select(e => e.Kind).ToHashSet())))),
            DiveSiteStatus.Archived => new DiveSiteLookup.Gone(),
            DiveSiteStatus.Draft => new DiveSiteLookup.NotFound(),
            _ => await FollowRedirect(query.Slug, ct),
        };
    }

    private async Task<IReadOnlyList<PublicPhoto>> Photos(Guid siteId, string culture, CancellationToken ct)
    {
        var items = await dbContext.MediaItems
            .AsNoTracking()
            .Include(m => m.Translations)
            .Where(m => m.DiveSiteId == siteId && m.IsPublic && m.SortOrder != null)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        return [.. items.Select(m =>
        {
            var text = m.Translations.FirstOrDefault(t => t.Culture == culture);
            var german = m.Translations.FirstOrDefault(t => t.Culture == "de");
            return new PublicPhoto(
                m.Id, m.Section, m.EntryPointId, m.Width, m.Height, m.Depth, m.Photographer, m.PhotographerUrl,
                text?.Caption ?? german?.Caption,
                text?.AltText ?? german?.AltText,
                [.. MediaFiles.WidthsFor(m.Width).Select(w => new MediaRendition(w, media.PublicUri(m.Id, MediaFiles.Rendition(w)).ToString()))]);
        })];
    }

    private async Task<SiteTexts> Texts(Guid siteId, string culture, CancellationToken ct)
    {
        var translations = await dbContext.DiveSiteTranslations
            .AsNoTracking()
            .Where(t => t.DiveSiteId == siteId && (t.Culture == culture || t.Culture == "de"))
            .ToListAsync(ct);
        var own = translations.FirstOrDefault(t => t.Culture == culture);
        var german = translations.FirstOrDefault(t => t.Culture == "de");

        SiteText? Pick(Func<DiveSiteTranslation, string?> field)
        {
            if (own is not null && !string.IsNullOrWhiteSpace(field(own)))
            {
                return new SiteText(field(own)!, false, own.IsMachineTranslated);
            }

            return german is not null && !string.IsNullOrWhiteSpace(field(german)) ? new SiteText(field(german)!, culture != "de", false) : null;
        }

        return new SiteTexts(
            Pick(t => t.UnderwaterDescription),
            Pick(t => t.SurroundingsInfo),
            Pick(t => t.ApproachInfo),
            Pick(t => t.HazardsInfo),
            Pick(t => t.FacilitiesInfo),
            Pick(t => t.DivingBanNote));
    }

    private static IReadOnlyList<SiteSketchFeature> Sketch(string? json, string culture) =>
        [.. SketchJson.Read(json).Select(f => new SiteSketchFeature(
            f.Kind,
            [.. f.Points.Select(p => new SitePoint(p.Latitude, p.Longitude))],
            f.Depth,
            f.Texts.FirstOrDefault(t => t.Culture == culture)?.Label ?? f.Texts.FirstOrDefault(t => t.Culture == "de")?.Label))];

    /// <summary>
    /// Hinweise der Redaktion und bestätigte Einschränkungen des SUSV, die nicht vorbei sind (Story 003).
    /// Heute gültige zuerst (schwerste Art zuerst, bei gleicher Art die Redaktion vor dem SUSV), dann nach Beginn.
    /// </summary>
    private async Task<IReadOnlyList<SiteNotice>> Notices(Guid siteId, string culture, DateOnly today, CancellationToken ct)
    {
        var notices = await dbContext.DiveSiteNotices
            .AsNoTracking()
            .Where(n => n.DiveSiteId == siteId)
            .Include(n => n.Translations)
            .ToListAsync(ct);
        var susv = await dbContext.SusvEntries
            .AsNoTracking()
            .Where(SusvRules.ShownRestriction)
            .Where(e => e.Sites.Any(s => s.DiveSiteId == siteId))
            .Include(e => e.Translations)
            .ToListAsync(ct);

        var editorial = notices.Select(n =>
        {
            var text = n.Translations.FirstOrDefault(t => t.Culture == culture) ?? n.Translations.FirstOrDefault(t => t.Culture == "de") ?? n.Translations.First();
            return (Notice: Visible(n.Kind, text.Text, text.Culture != culture, new NoticePeriod(n.ValidFrom, n.ValidUntil, n.RecursYearly), today, null), Order: n.SortOrder);
        });
        var fromSusv = susv.Select(e =>
        {
            var text = e.Translations.FirstOrDefault(t => t.Culture == culture) ?? e.Translations.First(t => t.Culture == "de");
            var body = SusvRules.Shorten(text.Text ?? text.Teaser ?? "", SusvRules.BannerTextLength);
            var notice = Visible(e.NoticeKind!.Value, body, text.Culture != culture, new NoticePeriod(e.ValidFrom, e.ValidUntil, e.RecursYearly), today, new SiteNoticeSusv(text.Title, text.DetailUrl));
            return (Notice: notice is null ? null : notice with { MachineTranslated = text.IsMachineTranslated }, Order: int.MaxValue);
        });

        return editorial.Concat(fromSusv)
            .Where(x => x.Notice is not null)
            .OrderBy(x => x.Notice!.Timing)
            .ThenByDescending(x => x.Notice!.Timing == NoticeTiming.Today ? x.Notice.Kind : default)
            .ThenBy(x => x.Notice!.Start)
            .ThenBy(x => x.Order)
            .Select(x => x.Notice!)
            .ToList();
    }

    private async Task<IReadOnlyList<SiteSusvProject>> SusvProjects(Guid siteId, string culture, CancellationToken ct)
    {
        var entries = await dbContext.SusvEntries
            .AsNoTracking()
            .Where(SusvRules.ShownProject)
            .Where(e => e.Sites.Any(s => s.DiveSiteId == siteId))
            .Select(e => new { e.Topic, e.SourceId, Urls = e.Translations.Select(t => new { t.Culture, t.DetailUrl }).ToList() })
            .ToListAsync(ct);

        return entries
            .OrderBy(e => e.Topic)
            .ThenBy(e => e.SourceId)
            .DistinctBy(e => e.Topic)
            .Select(e => new SiteSusvProject(e.Topic, (e.Urls.FirstOrDefault(u => u.Culture == culture) ?? e.Urls.First(u => u.Culture == "de")).DetailUrl))
            .ToList();
    }

    private static SiteNotice? Visible(NoticeKind kind, string text, bool germanOnly, NoticePeriod period, DateOnly today, SiteNoticeSusv? susv) =>
        period.Timing(today) is { } timing && period.Next(today) is { } next
            ? new SiteNotice(kind, text, next.End, germanOnly, timing, next.Start, period.Yearly && period.From is not null && period.Until is not null, susv)
            : null;

    /// <summary>Tage der Prognose ab heute, an denen eine Sperrung der Redaktion oder des SUSV gilt.</summary>
    private static IReadOnlyList<DateOnly> ClosedDays(IReadOnlyList<SiteNotice> notices, DateOnly today) =>
        [.. Enumerable.Range(0, ForecastSources.ForecastDays)
            .Select(today.AddDays)
            .Where(day => notices.Any(n => n.Kind == NoticeKind.Closure && (n.Start is null || n.Start <= day) && (n.ValidUntil is null || n.ValidUntil >= day)))];

    private async Task<IReadOnlyList<SiteEntryPoint>> EntryPoints(Guid siteId, string culture, CancellationToken ct)
    {
        var entryPoints = await dbContext.DiveSiteEntryPoints
            .AsNoTracking()
            .Where(e => e.DiveSiteId == siteId)
            .OrderBy(e => e.SortOrder)
            .Include(e => e.Translations)
            .ToListAsync(ct);

        return entryPoints
            .Select(e => new SiteEntryPoint(
                e.Kind,
                e.Location.Y,
                e.Location.X,
                e.Translations.FirstOrDefault(t => t.Culture == culture && !string.IsNullOrWhiteSpace(t.Name))?.Name
                    ?? e.Translations.FirstOrDefault(t => t.Culture == "de" && !string.IsNullOrWhiteSpace(t.Name))?.Name))
            .ToList();
    }

    private async Task<DiveSiteLookup> FollowRedirect(string slug, CancellationToken ct)
    {
        var current = await dbContext.SlugRedirects
            .AsNoTracking()
            .Where(r => r.OldSlug == slug)
            .Join(dbContext.DiveSites, r => r.DiveSiteId, d => d.Id, (_, d) => new { d.Slug, d.Status })
            .SingleOrDefaultAsync(ct);

        return current?.Status == DiveSiteStatus.Published ? new DiveSiteLookup.Moved(current.Slug) : new DiveSiteLookup.NotFound();
    }
}
