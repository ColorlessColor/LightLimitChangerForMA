using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;

namespace io.github.azukimochi;

internal class NdmfMessage : SimpleError
{
    public NdmfMessage(ErrorSeverity severity, string titleKey, string detailsKey = null, string hintKey = null)
    {
        TitleKey = titleKey;
        Severity = severity;
        DetailsKey = detailsKey;
        HintKey = hintKey;
    }

    public override Localizer Localizer => L10n.Localizer;
    
    public override ErrorSeverity Severity { get; }

    public override string TitleKey { get; }
    public override string[] TitleSubst => TitleSubstitutions;
    public string[] TitleSubstitutions { get => titleSubst; set => titleSubst = value; }
    private string[] titleSubst;

    public override string DetailsKey { get; }

    public override string[] DetailsSubst => detailsSubst;
    public string[] DetailsSubstitutions { get => detailsSubst; set => detailsSubst = value; }
    private string[] detailsSubst;

    public override string HintKey { get; }

    public override string[] HintSubst => HintSubstitutions;
    public string[] HintSubstitutions { get => hintSubst; set => hintSubst = value; }
    private string[] hintSubst;

    public static NdmfMessage Create(ErrorSeverity severity, string key)
    {
        return new(severity, $"{key}/title", $"{key}/details", $"{key}/hint");
    }
}