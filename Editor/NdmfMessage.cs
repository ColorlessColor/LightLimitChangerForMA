using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;

namespace io.github.azukimochi;

internal class NdmfMessage : SimpleError
{
    public NdmfMessage(string titleKey, ErrorSeverity severity, string detailsKey = null, string[] detailsSubst = null)
    {
        TitleKey = titleKey;
        Severity = severity;
        DetailsKey = detailsKey;
        DetailsSubst = detailsSubst;
    }

    public override Localizer Localizer => L10n.Localizer;

    public override string TitleKey { get; }

    public override ErrorSeverity Severity { get; }

    public override string DetailsKey { get; }
    public override string[] DetailsSubst { get; }

    public static void Throw(string titleKey, ErrorSeverity severity = ErrorSeverity.NonFatal) 
        => ErrorReport.ReportError(new NdmfMessage(titleKey, severity));
}