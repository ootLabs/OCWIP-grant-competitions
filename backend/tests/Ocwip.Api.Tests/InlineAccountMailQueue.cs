using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

namespace Ocwip.Api.Tests;

/// <summary>
/// Hands an account mail straight to IEmailSender on the caller's thread, so a
/// test can read what a request mailed as soon as the request has answered.
/// The factory registers it in place of the real queue; a test of the real
/// queue registers that back. The sender is looked up on use, not at
/// construction, because a host without a database has no IEmailSender and
/// still has to start.
/// </summary>
internal sealed class InlineAccountMailQueue(IServiceProvider services) : IAccountMailQueue
{
    public void Enqueue(EmailMessage message) => services.GetRequiredService<IEmailSender>().SendAsync(message).GetAwaiter().GetResult();
}
