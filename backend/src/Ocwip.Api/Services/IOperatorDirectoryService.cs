using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// Who a competition may name as a contact (T-22, step 1.6). One read, kept
/// apart from IAccountService because that one is about registering an
/// applicant and this one never writes anything.
/// </summary>
public interface IOperatorDirectoryService
{
    Task<IReadOnlyList<OperatorAccountResponse>> ListOperatorsAsync(
        CancellationToken cancellationToken);
}
