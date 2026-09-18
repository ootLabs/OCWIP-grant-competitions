namespace Ocwip.Api.Contracts;

/// <summary>
/// The body an operator sends to create or to edit a competition (T-20).
///
/// One record for both, because the two are the same set of settings and a
/// separate update type would drift from this one field by field. What may
/// change and when is a question about the STATE of the competition, not about
/// the shape of the request, and it is answered in CompetitionService.
///
/// Deliberately not carrying the status: a competition never changes state by
/// having a different value sent to it, only through the transition table.
/// The status of a fresh competition is always Draft.
///
/// The parameters from steps 1.2 to 1.6 of the announcement wizard (limits,
/// percentages, cost categories, attachment requirements, contact people,
/// paper delivery) are not here. They are T-20a, see
/// docs/runbook/kolejka.md.
/// </summary>
public sealed record CompetitionRequest(
    string Number,
    string Title,
    string? Description,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    bool IsContinuousIntake,
    decimal MaxGrantAmount,
    Guid? FormDefinitionId);
