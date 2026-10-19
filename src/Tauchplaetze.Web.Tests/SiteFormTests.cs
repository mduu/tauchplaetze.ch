using System.Text.Json;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Web.Features.Editorial;

namespace Tauchplaetze.Web.Tests;

public class SiteFormTests
{
    private static readonly Guid Entry = Guid.NewGuid();

    private static readonly Guid Parking = Guid.NewGuid();

    private static readonly Guid Hero = Guid.NewGuid();

    private static readonly Guid Detail = Guid.NewGuid();

    private static DiveSiteContent Content(IReadOnlyList<MediaUse> media, Guid? hero) => new(
        "Chappeli", "chappeli-oberaegeri", 1, 47.13, 8.62, "Oberägeri", "ZG", 30, 2, 4, 3, 1, 2, 5,
        DiveSiteAccess.Shore, DiveSiteFeatures.Plateau | DiveSiteFeatures.Training, true, "2099", 47.131, 8.621, true,
        [new DiveSiteContentTranslation("de", "Steilwand bis 30 m.", null, "Bus bis Chappeli.", null, null, "Im Sommer gesperrt.", true)],
        [
            new EntryPointContent(Entry, 47.1301, 8.6201, EntryPointKind.EntryAndExit, [new EntryPointText("de", "Treppe", "Beim Steg."), new EntryPointText("fr", "Escalier", null)]),
            new EntryPointContent(Parking, 47.1310, 8.6190, EntryPointKind.Parking, []),
        ],
        [new NoticeContent(Guid.NewGuid(), NoticeKind.Warning, new DateOnly(2026, 6, 1), null, [new NoticeText("de", "Baustelle")])],
        [
            new SketchFeature(Guid.NewGuid(), SketchKind.Wreck, [new SketchPoint(47.13, 8.62)], 22, [new SketchText("it", "Barca")]),
            new SketchFeature(Guid.NewGuid(), SketchKind.Route, [new SketchPoint(47.13, 8.62), new SketchPoint(47.131, 8.621)], null, []),
        ],
        media,
        hero,
        ["Chappelibucht"]);

    [Fact]
    public void Wer_im_Bearbeiten_Modus_nichts_aendert_speichert_Einstiege_Skizze_Fotos_und_Moderationsfelder_unveraendert()
    {
        // Arrange
        var content = Content([new MediaUse(Detail, MediaSection.EntryPoint, Entry), new MediaUse(Hero, MediaSection.Underwater)], Hero);

        // Act
        var saved = SiteForm.From(content).ToContent();

        // Assert
        Assert.Equal(JsonSerializer.Serialize(content), JsonSerializer.Serialize(saved));
    }

    [Fact]
    public void Ein_Foto_ausserhalb_des_Abschnitts_Einstieg_verliert_den_Einstieg_und_ein_entferntes_Titelbild_faellt_weg()
    {
        // Arrange
        var form = SiteForm.From(Content([new MediaUse(Detail, MediaSection.EntryPoint, Entry), new MediaUse(Hero, MediaSection.Underwater)], Hero));

        // Act
        form.Media[0].Section = MediaSection.Surroundings;
        form.Media.RemoveAt(1);
        var saved = form.ToContent();

        // Assert
        Assert.Equal([new MediaUse(Detail, MediaSection.Surroundings)], saved.MediaList);
        Assert.Null(saved.HeroMediaId);
    }
}
