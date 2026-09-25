namespace Ocwip.Api.Models
{
    /// <summary>
    /// The wording an expert accepts before evaluating (T-40a). A WORKING
    /// TEXT (ZR-05 in docs/runbook/zalozenia-robocze.md): the real one is
    /// annex 1 to the 2026 committee regulations, which was not in the
    /// published documents. This one says what § 2 of those regulations
    /// requires and nothing more.
    /// </summary>
    public static class ImpartialityDeclaration
    {
        public const string WorkingText =
            "Oświadczam, że nie jestem związany z wnioskodawcami ani realizatorami projektów "
            + "złożonych w tym konkursie stosunkiem osobistym lub służbowym, który mógłby wywołać "
            + "wątpliwości co do mojej bezstronności w ocenie wniosków. Jeżeli taki związek ujawni "
            + "się w trakcie oceny, niezwłocznie poinformuję o tym operatora i nie będę oceniać "
            + "wniosku, którego dotyczy.";
    }
}
