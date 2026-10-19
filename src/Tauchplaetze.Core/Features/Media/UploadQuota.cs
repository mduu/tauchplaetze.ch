using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>Wie viele Fotos ein Konto jetzt noch hochladen darf (<see cref="MediaRules.MaxPerHour"/>, <see cref="MediaRules.MaxPerDay"/>).</summary>
internal static class UploadQuota
{
    public static async Task<int> Remaining(TauchplaetzeDbContext dbContext, DateTimeOffset now, string uploaderId, CancellationToken ct)
    {
        var lastDay = await dbContext.MediaItems.Where(m => m.UploadedBy == uploaderId && m.UploadedAt > now.AddDays(-1)).Select(m => m.UploadedAt).ToListAsync(ct);
        var lastHour = lastDay.Count(at => at > now.AddHours(-1));
        return Math.Max(0, Math.Min(MediaRules.MaxPerDay - lastDay.Count, MediaRules.MaxPerHour - lastHour));
    }
}
