using Ocwip.Api.Contracts;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// What the service does without reaching Identity at all. Everything the
/// registration path does WITH Identity is asserted over real HTTP against a
/// real database in Endpoints/RegistrationEndpointTests.cs, because the service
/// deliberately cannot report the difference between a created account and a
/// taken address and only a caller can see what came back.
/// </summary>
public sealed class AccountServiceTests
{
    [Fact]
    public async Task A_cancelled_caller_gets_no_account_created_behind_its_back()
    {
        // UserManager offers no overload that takes a token, so the token this
        // contract accepts used to be accepted and ignored: a caller that had
        // already given up still got a row written and a password hashed. The
        // UserManager here is null on purpose, which is the assertion: nothing
        // may touch it after the token is already cancelled.
        var service = new AccountService(userManager: null!);
        var request = new RegisterRequest(
            "adam@example.org", "Tajne-Haslo1", "Adam", "Testowy");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.RegisterAsync(request, new CancellationToken(canceled: true)));
    }
}
