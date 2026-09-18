using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// Asks for one move through the lifecycle (T-20).
///
/// One endpoint taking a target state rather than a verb per transition
/// (/publish, /close, /resolve, /archive). The table of allowed pairs is
/// already the authority on what may happen next, so a route per verb would be
/// a second, longer copy of it, and adding a state would mean adding a route
/// instead of adding a row. What the caller may ask for is whatever
/// CompetitionStatusTransitions allows, and the response says so out loud in
/// allowedTransitions.
/// </summary>
public sealed record CompetitionStatusChangeRequest(CompetitionStatus Status);
