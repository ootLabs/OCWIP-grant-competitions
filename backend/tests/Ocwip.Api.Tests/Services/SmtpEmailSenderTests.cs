using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Ocwip.Api.Configuration;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Xunit;

namespace Ocwip.Api.Tests.Services;

/// <summary>
/// T-43a: mail goes out over real SMTP, a refusal of the relay reaches the
/// caller, and without a relay nothing but the subject is logged outside
/// Development.
/// </summary>
public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task A_mail_reaches_the_relay_with_its_recipient_and_body()
    {
        using var relay = new FakeSmtpRelay(refuseRecipient: false);
        var sender = new SmtpEmailSender(Options.Create(relay.Options));

        await sender.SendAsync(new EmailMessage("anna@example.org", "Wynik konkursu", "Twój wniosek otrzymał dofinansowanie."));

        var transcript = await relay.TranscriptAsync();
        Assert.Contains("RCPT TO:<anna@example.org>", transcript);
        Assert.Contains("MAIL FROM:<konkursy@ocwip.example>", transcript);

        // UTF-8 body in base64, as the framework encodes non ASCII text.
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("Twój wniosek otrzymał dofinansowanie."));
        Assert.Contains(encoded, transcript.Replace("\r\n", string.Empty));
    }

    [Fact]
    public async Task A_refusal_of_the_relay_reaches_the_caller()
    {
        using var relay = new FakeSmtpRelay(refuseRecipient: true);
        var sender = new SmtpEmailSender(Options.Create(relay.Options));

        await Assert.ThrowsAnyAsync<SmtpException>(() =>
            sender.SendAsync(new EmailMessage("anna@example.org", "Wynik", "Treść")));
    }

    [Fact]
    public async Task Outside_development_the_stand_in_logs_the_subject_and_never_the_body()
    {
        var logger = new CapturingLogger();
        var sender = new EmailSenderService(logger, new Environment(Environments.Production));

        await sender.SendAsync(new EmailMessage("anna@example.org", "Zresetuj hasło", "https://ocwip.example/reset?token=SECRET"));

        var logged = Assert.Single(logger.Messages);
        Assert.Contains("Zresetuj hasło", logged);
        Assert.DoesNotContain("SECRET", logged);
        Assert.DoesNotContain("anna@example.org", logged);
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Ocwip.Api";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger : ILogger<EmailSenderService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    /// <summary>
    /// Just enough of RFC 5321 for one mail on a local port, no TLS, so the
    /// test speaks the protocol the real relay will be spoken to in.
    /// </summary>
    private sealed class FakeSmtpRelay : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Task<string> _session;

        public FakeSmtpRelay(bool refuseRecipient)
        {
            _listener.Start();
            _session = RunAsync(refuseRecipient);
        }

        public SmtpOptions Options => new()
        {
            Host = "127.0.0.1",
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
            EnableSsl = false,
            From = "konkursy@ocwip.example",
        };

        public async Task<string> TranscriptAsync() =>
            await _session.WaitAsync(TimeSpan.FromSeconds(10));

        private async Task<string> RunAsync(bool refuseRecipient)
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            var transcript = new StringBuilder();

            await writer.WriteLineAsync("220 test relay");
            while (await reader.ReadLineAsync() is { } line)
            {
                transcript.AppendLine(line);
                var command = line.ToUpperInvariant();

                if (command.StartsWith("EHLO") || command.StartsWith("HELO"))
                {
                    await writer.WriteLineAsync("250 test relay");
                }
                else if (command.StartsWith("RCPT") && refuseRecipient)
                {
                    await writer.WriteLineAsync("554 no such mailbox");
                }
                else if (command.StartsWith("DATA"))
                {
                    await writer.WriteLineAsync("354 go ahead");
                    while (await reader.ReadLineAsync() is { } data && data != ".")
                    {
                        transcript.AppendLine(data);
                    }

                    await writer.WriteLineAsync("250 queued");
                }
                else if (command.StartsWith("QUIT"))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }

            return transcript.ToString();
        }

        public void Dispose() => _listener.Stop();
    }
}
