using Npgsql;

namespace Ocwip.Api.Data;

/// <summary>
/// The one data source /health/db asks (T-111), built once from the
/// connection string instead of once per probe: a probe every few seconds
/// from the container healthcheck would otherwise build and throw away a
/// connection pool each time. Null when no database is configured.
/// </summary>
public sealed class DatabaseProbe(IConfiguration configuration) : IAsyncDisposable
{
    private readonly Lazy<NpgsqlDataSource?> _source = new(() =>
        configuration.GetConnectionString("Postgres") is { Length: > 0 } connectionString
            ? NpgsqlDataSource.Create(connectionString)
            : null);

    public NpgsqlDataSource? Source => _source.Value;

    public async ValueTask DisposeAsync()
    {
        if (_source.IsValueCreated && _source.Value is { } source)
        {
            await source.DisposeAsync();
        }
    }
}
