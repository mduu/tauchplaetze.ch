using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Media.Queries;
using Tauchplaetze.Web.Features.Editorial;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Tests;

public class MediaUploadTests
{
    private readonly List<string> sent = [];

    [Fact]
    public async Task Mehr_Dateien_als_das_Limit_erlaubt_liest_keine_einzige()
    {
        // Arrange
        IReadOnlyList<IBrowserFile> files = [.. Enumerable.Range(1, 6).Select(i => new FakeFile($"{i}.jpg"))];

        // Act
        var outcome = await MediaUpload.Run(files, new UploadAllowance(false, 5), Upload, new KeyLocalizer());

        // Assert
        Assert.Empty(outcome.Uploaded);
        Assert.Equal(["Media_Remaining(5)"], outcome.Errors);
        Assert.All(files.Cast<FakeFile>(), f => Assert.False(f.Read));
    }

    [Fact]
    public async Task Erreicht_der_Upload_das_Limit_bleiben_die_uebrigen_Dateien_ungelesen()
    {
        // Arrange
        var files = new[] { new FakeFile("a.jpg"), new FakeFile("limit.jpg"), new FakeFile("c.jpg") };

        // Act
        var outcome = await MediaUpload.Run(files, new UploadAllowance(false, null), Upload, new KeyLocalizer());

        // Assert
        Assert.Single(outcome.Uploaded);
        Assert.Equal(["limit.jpg: Error_TooManyUploads"], outcome.Errors);
        Assert.False(files[2].Read);
    }

    [Fact]
    public async Task Bricht_die_Verbindung_beim_Lesen_ab_meldet_der_Upload_einen_Fehler_statt_einer_Ausnahme()
    {
        // Arrange
        var files = new[] { new FakeFile("a.jpg", aborts: true), new FakeFile("b.jpg") };

        // Act
        var outcome = await MediaUpload.Run(files, new UploadAllowance(false, null), Upload, new KeyLocalizer());

        // Assert
        Assert.Empty(outcome.Uploaded);
        Assert.Equal(["a.jpg: Media_UploadAborted"], outcome.Errors);
        Assert.Empty(sent);
    }

    private Task<UploadMediaResult> Upload(byte[] content, string contentType)
    {
        var name = System.Text.Encoding.UTF8.GetString(content);
        sent.Add(name);
        return Task.FromResult(name == "limit.jpg"
            ? new UploadMediaResult([EditorialError.TooManyUploads])
            : new UploadMediaResult([], Guid.NewGuid()));
    }

    private sealed class FakeFile(string name, bool aborts = false) : IBrowserFile
    {
        private readonly byte[] content = System.Text.Encoding.UTF8.GetBytes(name);

        public bool Read { get; private set; }

        public string Name => name;

        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;

        public long Size => content.Length;

        public string ContentType => "image/jpeg";

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            Read = true;
            return aborts ? throw new IOException("Verbindung weg") : new MemoryStream(content);
        }
    }

    private sealed class KeyLocalizer : IStringLocalizer<AdminStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, $"{name}({string.Join(",", arguments)})");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
