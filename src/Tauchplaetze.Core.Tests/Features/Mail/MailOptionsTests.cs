using Tauchplaetze.Core.CrossCutting.Mail;

namespace Tauchplaetze.Core.Tests.Features.Mail;

public class MailOptionsTests
{
    [Theory]
    [InlineData("noreply@tauchplaetze.ch", false, true)]
    [InlineData("noreply@tauchplaetze.ch", true, false)]
    [InlineData(null, false, false)]
    [InlineData("", false, false)]
    public void Mit_Anmeldung_ohne_TLS_ginge_das_Passwort_im_Klartext_hinaus(string? userName, bool useTls, bool plain)
    {
        // Arrange
        var options = new MailOptions { UserName = userName, UseTls = useTls, Password = "geheim" };

        // Act
        var result = options.SendsPasswordWithoutTls;

        // Assert
        Assert.Equal(plain, result);
    }
}
