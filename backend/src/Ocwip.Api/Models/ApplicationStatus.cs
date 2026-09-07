namespace Ocwip.Api.Models
{
    /// <summary>
    /// The only two states an application has while the evaluation module does
    /// not exist. Accepted, rejected and everything else on a ranking list
    /// belongs to the review entity, which docs/model-danych.md deliberately
    /// does not build yet.
    /// </summary>
    public enum ApplicationStatus
    {
        Draft,
        Submitted
    }
}
