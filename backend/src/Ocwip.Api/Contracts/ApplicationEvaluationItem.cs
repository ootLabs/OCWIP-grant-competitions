namespace Ocwip.Api.Contracts;

/// <summary>
/// One card of an application as the operator opens it from the ranking list
/// (T-41a): the card with its result, and who filled it in by name. The name
/// is for the operator only; an applicant who later sees the card (T-41b)
/// sees no evaluator.
/// </summary>
public sealed record ApplicationEvaluationItem(EvaluationResponse Evaluation, string Author);
