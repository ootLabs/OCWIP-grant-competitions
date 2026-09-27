using Ocwip.Api.Models;
using Ocwip.Api.Services.Documents;

namespace Ocwip.Api.Contracts;

/// <summary>Publishing the next version of the contract template (T-45).</summary>
public sealed record DocumentTemplateRequest(string? Body);

/// <summary>
/// A template version with its placeholders, so the screen can say which
/// blanks the system fills and which the operator will type in.
/// </summary>
public sealed record DocumentTemplateResponse(
    Guid Id,
    Guid CompetitionId,
    int VersionNumber,
    string Body,
    IReadOnlyList<TemplatePlaceholder> Placeholders,
    DateTimeOffset CreatedAt);

/// <summary>One placeholder of a contract with the value it prints; empty prints as a dotted blank.</summary>
public sealed record ContractField(string Name, string Label, bool System, string? Value);

/// <summary>A contract of a funded application (T-45).</summary>
public sealed record ContractResponse(
    Guid Id,
    Guid ApplicationId,
    string? ApplicationNumber,
    string EntityName,
    int TemplateVersion,
    ContractStatus Status,
    DateOnly? SignedOn,
    IReadOnlyList<ContractField> Fields);

/// <summary>The values the operator types in, by placeholder name; the whole set at once.</summary>
public sealed record ContractValuesRequest(IReadOnlyDictionary<string, string?> Values);

/// <summary>Recording the signing: the day, as written on the paper.</summary>
public sealed record SignContractRequest(DateOnly? SignedOn);
