using System.Globalization;

namespace Tauchplaetze.Core.CrossCutting.Mail.Templates;

/// <summary>Eine Mail-Vorlage. Ihr Name steht im Umschlag der Queue, darum nie umbenennen.</summary>
internal interface IMailTemplate
{
    /// <summary>Stabiler Name, z.B. `ProposalApproved`.</summary>
    string Name { get; }

    /// <summary>Typ des Modells. Nur diese Typen liest der Worker aus der Queue (Whitelist).</summary>
    Type ModelType { get; }

    MailContent Render(object model, CultureInfo culture);
}

/// <summary>Vorlage mit festem Modell-Typ.</summary>
internal abstract class MailTemplate<TModel> : IMailTemplate
{
    public abstract string Name { get; }

    public Type ModelType => typeof(TModel);

    public MailContent Render(object model, CultureInfo culture) => Render((TModel)model, culture);

    protected abstract MailContent Render(TModel model, CultureInfo culture);

    protected static string T(CultureInfo culture, string key, params object[] values) => MailLayout.Text(culture, key, values);
}
