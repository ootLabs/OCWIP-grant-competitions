using Ocwip.Api.Services;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Registers the queue the account mails go through. One instance serves as
/// both the queue the services write to and the hosted service that reads it,
/// so it is registered once and exposed twice.
/// </summary>
public static class AccountMailConfiguration
{
    public static IServiceCollection AddAccountMailQueue(this IServiceCollection services)
    {
        services.AddSingleton<QueuedAccountMail>();
        services.AddSingleton<IAccountMailQueue>(provider => provider.GetRequiredService<QueuedAccountMail>());
        services.AddHostedService(provider => provider.GetRequiredService<QueuedAccountMail>());

        return services;
    }
}
