namespace Ocwip.Api.Authorization;

/// <summary>
/// A resource that belongs to one Podmiot (T-13.2).
///
/// The marker exists so that "whose is this" is asked of the RESOURCE rather
/// than rediscovered at every call site: a handler can then be written once,
/// against this interface, instead of once per model class.
///
/// Implemented by Models/Application.cs (it already carries EntityId) and by
/// Models/Entity.cs, which is its own owner. Every later applicant-facing
/// model, an attachment for instance, joins by implementing this and nothing
/// else.
/// </summary>
public interface IEntityScoped
{
    Guid EntityId { get; }
}
