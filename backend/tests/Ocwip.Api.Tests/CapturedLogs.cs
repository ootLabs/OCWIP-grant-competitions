using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Ocwip.Api.Tests;

/// <summary>
/// Everything the application logged during a test, as formatted text.
///
/// AGENTS.md security rule 4 says a password never reaches a log, and a rule
/// without an automatic check is a wish. Reading the response body proves
/// nothing about the log: the two are written by different code, and the log is
/// the one that ends up in an aggregator somebody else can read.
///
/// Registered as a provider on the test host, so it sees what a real deployment
/// would see, including whatever ASP.NET Core itself decides to write about a
/// request. Reusable by T-12.2 and T-12.4, which owe the same guarantee for
/// verification and reset links.
/// </summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public string Text => string.Join("\n", _messages);

    public ILogger CreateLogger(string categoryName) =>
        new QueueLogger(_messages, categoryName);

    public void Dispose()
    {
    }

    private sealed class QueueLogger(
        ConcurrentQueue<string> messages,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // The exception is folded in as well: a stack trace carrying the
            // request body would leak just as effectively as a message.
            messages.Enqueue(
                $"{logLevel} {category} {formatter(state, exception)} {exception}");
        }
    }
}
